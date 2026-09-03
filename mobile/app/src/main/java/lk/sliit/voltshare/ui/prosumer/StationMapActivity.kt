// -----------------------------------------------------------------------------
// File        : StationMapActivity.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Plots the microgrid nodes on Google Maps from the coordinates
//               stored against each one, and shows the details of the node the
//               user taps.
//
//               The nodes and their distances come from the Web API, so this
//               screen only places the markers it is given.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui.prosumer

import android.annotation.SuppressLint
import android.os.Bundle
import android.view.View
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import com.google.android.gms.maps.CameraUpdateFactory
import com.google.android.gms.maps.GoogleMap
import com.google.android.gms.maps.OnMapReadyCallback
import com.google.android.gms.maps.SupportMapFragment
import com.google.android.gms.maps.model.BitmapDescriptorFactory
import com.google.android.gms.maps.model.LatLng
import com.google.android.gms.maps.model.LatLngBounds
import com.google.android.gms.maps.model.MarkerOptions
import kotlinx.coroutines.launch
import lk.sliit.voltshare.AppServices
import lk.sliit.voltshare.R
import lk.sliit.voltshare.data.remote.ApiClient
import lk.sliit.voltshare.data.remote.ApiException
import lk.sliit.voltshare.data.remote.StationDto
import lk.sliit.voltshare.databinding.ActivityStationMapBinding
import lk.sliit.voltshare.util.Formatters
import lk.sliit.voltshare.util.LocationHelper
import android.widget.Toast

class StationMapActivity : AppCompatActivity(), OnMapReadyCallback {

    private lateinit var binding: ActivityStationMapBinding
    private var map: GoogleMap? = null

    // Loaded nodes, keyed by the marker that represents each one so a tap can
    // be resolved back to the station without searching the list.
    private val stationsByMarkerId = mutableMapOf<String, StationDto>()

    // Set when the user arrived by tapping a particular node in the list.
    private var focusStationId: String? = null

    /**
     * Builds the screen and asks for the map to be prepared.
     */
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        binding = ActivityStationMapBinding.inflate(layoutInflater)
        setContentView(binding.root)

        focusStationId = intent.getStringExtra(EXTRA_FOCUS_STATION_ID)

        // The map arrives asynchronously; markers are added in onMapReady.
        //
        // The fragment is looked up by its resource id rather than through the
        // binding, because view binding does not generate a property for a
        // <fragment> tag: the fragment is owned by the fragment manager, not
        // by the view hierarchy the binding describes.
        val mapFragment = supportFragmentManager
            .findFragmentById(R.id.mapFragment) as SupportMapFragment

        mapFragment.getMapAsync(this)
    }

    /**
     * Called once the map is ready to use.
     */
    @SuppressLint("MissingPermission")
    override fun onMapReady(googleMap: GoogleMap) {
        map = googleMap

        googleMap.uiSettings.isZoomControlsEnabled = true
        googleMap.uiSettings.isMapToolbarEnabled = false

        // The blue dot is only enabled once the user has actually granted the
        // permission, otherwise the call throws.
        if (LocationHelper.hasPermission(this)) {
            googleMap.isMyLocationEnabled = true
        }

        googleMap.setOnMarkerClickListener { marker ->
            stationsByMarkerId[marker.id]?.let { showDetails(it) }

            // false lets the map keep its default behaviour of centring on the
            // marker and showing its title.
            false
        }

        loadStations()
    }

    /**
     * Fetches the nodes to plot, falling back to the ones cached locally.
     */
    private fun loadStations() {
        binding.progress.visibility = View.VISIBLE

        lifecycleScope.launch {
            try {
                val position = LocationHelper.currentPosition(this@StationMapActivity)

                val stations = if (position == null) {
                    ApiClient.call { AppServices.api.listStations() }
                } else {
                    ApiClient.call {
                        AppServices.api.nearbyStations(position.latitude, position.longitude)
                    }.map { it.station }
                }

                AppServices.store.replaceStations(stations)
                plot(stations)
            } catch (error: ApiException) {
                val cached = AppServices.store.getCachedStations()

                if (cached.isEmpty()) {
                    Toast.makeText(this@StationMapActivity, error.message, Toast.LENGTH_LONG).show()
                } else {
                    Toast.makeText(
                        this@StationMapActivity,
                        "Showing saved nodes; the service could not be reached.",
                        Toast.LENGTH_LONG
                    ).show()
                    plot(cached)
                }
            } finally {
                binding.progress.visibility = View.GONE
            }
        }
    }

    /**
     * Places a marker for every node and frames them all on screen.
     */
    private fun plot(stations: List<StationDto>) {
        val googleMap = map ?: return

        googleMap.clear()
        stationsByMarkerId.clear()

        if (stations.isEmpty()) {
            // With nothing to show, centre on the fallback position so the map
            // is not left staring at the middle of the ocean.
            googleMap.moveCamera(
                CameraUpdateFactory.newLatLngZoom(
                    LatLng(LocationHelper.FALLBACK.latitude, LocationHelper.FALLBACK.longitude),
                    11f
                )
            )
            return
        }

        val boundsBuilder = LatLngBounds.Builder()
        var focusPoint: LatLng? = null

        for (station in stations) {
            val point = LatLng(station.latitude, station.longitude)

            val marker = googleMap.addMarker(
                MarkerOptions()
                    .position(point)
                    .title(station.name)
                    .snippet("${station.code} - ${station.city}")
                    .icon(BitmapDescriptorFactory.defaultMarker(BitmapDescriptorFactory.HUE_ORANGE))
            )

            if (marker != null) {
                stationsByMarkerId[marker.id] = station
            }

            boundsBuilder.include(point)

            if (station.id == focusStationId) {
                focusPoint = point
                showDetails(station)
            }
        }

        if (focusPoint != null) {
            // The user asked for one particular node, so open on it.
            googleMap.moveCamera(CameraUpdateFactory.newLatLngZoom(focusPoint, 15f))
        } else {
            // Otherwise frame every node, with a margin so none sits under the
            // edge of the screen.
            googleMap.moveCamera(
                CameraUpdateFactory.newLatLngBounds(boundsBuilder.build(), MAP_PADDING_PX)
            )
        }
    }

    /** Shows the details panel for one node. */
    private fun showDetails(station: StationDto) {
        binding.cardDetails.visibility = View.VISIBLE
        binding.textMapName.text = station.name
        binding.textMapAddress.text = "${station.addressLine}, ${station.city}"
        binding.textMapDetails.text = buildString {
            append(Formatters.energy(station.capacityKwh))
            append(", battery ")
            append(station.availableBatterySlots)
            append(" / ")
            append(station.totalBatterySlots)
            append(", open ")
            append(station.operatingHours.openTime)
            append(" to ")
            append(station.operatingHours.closeTime)
        }
    }

    companion object {
        /** Identifier of a node to open the map on, when one was tapped. */
        const val EXTRA_FOCUS_STATION_ID = "focus_station_id"

        // Margin left around the markers when framing them all.
        private const val MAP_PADDING_PX = 120
    }
}
