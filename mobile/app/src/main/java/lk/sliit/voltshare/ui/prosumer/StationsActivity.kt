// -----------------------------------------------------------------------------
// File        : StationsActivity.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Lists the microgrid nodes. When the user allows location
//               access the list is fetched from the nearby endpoint, so the
//               service orders the nodes by distance and reports how far each
//               one is. Results are cached in SQLite so the screen still shows
//               the last known nodes when the service cannot be reached.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui.prosumer

import android.Manifest
import android.content.Intent
import android.os.Bundle
import android.view.View
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import androidx.recyclerview.widget.LinearLayoutManager
import kotlinx.coroutines.launch
import lk.sliit.voltshare.AppServices
import lk.sliit.voltshare.data.remote.ApiClient
import lk.sliit.voltshare.data.remote.ApiException
import lk.sliit.voltshare.data.remote.StationDto
import lk.sliit.voltshare.databinding.ActivityStationsBinding
import lk.sliit.voltshare.util.LocationHelper
import lk.sliit.voltshare.util.SystemBars

class StationsActivity : AppCompatActivity() {

    private lateinit var binding: ActivityStationsBinding
    private lateinit var adapter: StationAdapter

    /**
     * Asks for location access. Whatever the user decides, the list is loaded
     * afterwards: granting it adds distances, refusing it simply shows the
     * nodes without them.
     */
    private val locationPermission = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { loadStations(showSpinner = true) }

    /**
     * Builds the screen and requests the list.
     */
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        binding = ActivityStationsBinding.inflate(layoutInflater)
        setContentView(binding.root)

        SystemBars.applyInsets(binding.headerBar)

        adapter = StationAdapter { station -> openMapAt(station) }
        binding.recyclerStations.layoutManager = LinearLayoutManager(this)
        binding.recyclerStations.adapter = adapter

        binding.buttonMap.setOnClickListener { openMapAt(null) }
        binding.swipeRefresh.setOnRefreshListener { loadStations(showSpinner = false) }

        // Ask for the permission the first time; if it is already granted the
        // system returns immediately and the list loads straight away.
        if (LocationHelper.hasPermission(this)) {
            loadStations(showSpinner = true)
        } else {
            locationPermission.launch(
                arrayOf(
                    Manifest.permission.ACCESS_FINE_LOCATION,
                    Manifest.permission.ACCESS_COARSE_LOCATION
                )
            )
        }
    }

    /**
     * Loads the nodes, preferring the nearby search when a position is known.
     */
    private fun loadStations(showSpinner: Boolean) {
        if (showSpinner) binding.progress.visibility = View.VISIBLE
        binding.textNotice.visibility = View.GONE

        lifecycleScope.launch {
            try {
                val position = LocationHelper.currentPosition(this@StationsActivity)

                val rows = if (position == null) {
                    // Without a position there is nothing to measure from, so
                    // the plain list is requested instead.
                    val stations = ApiClient.call { AppServices.api.listStations() }
                    AppServices.store.replaceStations(stations)
                    stations.map { StationRow(it, null) }
                } else {
                    // The service runs the geographic query and returns the
                    // nodes already ordered, each with its distance.
                    val nearby = ApiClient.call {
                        AppServices.api.nearbyStations(position.latitude, position.longitude)
                    }
                    AppServices.store.replaceStations(nearby.map { it.station })
                    nearby.map { StationRow(it.station, it.distanceMeters) }
                }

                showRows(rows)
            } catch (error: ApiException) {
                // The cached nodes are shown rather than an empty screen, which
                // is what the local database is kept for.
                showCachedFallback(error)
            } finally {
                binding.progress.visibility = View.GONE
                binding.swipeRefresh.isRefreshing = false
            }
        }
    }

    /**
     * Falls back to the nodes stored locally when the service is unreachable,
     * and says plainly that the data may be out of date.
     */
    private fun showCachedFallback(error: ApiException) {
        val cached = AppServices.store.getCachedStations()

        if (cached.isEmpty()) {
            binding.textNotice.text = error.message
            binding.textNotice.visibility = View.VISIBLE
            showRows(emptyList())
            return
        }

        binding.textNotice.text =
            "Showing saved nodes from your last visit. ${error.message}"
        binding.textNotice.visibility = View.VISIBLE

        showRows(cached.map { StationRow(it, null) })
    }

    /** Puts the rows on screen, or shows the empty message. */
    private fun showRows(rows: List<StationRow>) {
        adapter.submit(rows)

        binding.textEmpty.visibility = if (rows.isEmpty()) View.VISIBLE else View.GONE
    }

    /**
     * Opens the map, centred on one node when the user tapped a row.
     */
    private fun openMapAt(station: StationDto?) {
        val intent = Intent(this, StationMapActivity::class.java)

        if (station != null) {
            intent.putExtra(StationMapActivity.EXTRA_FOCUS_STATION_ID, station.id)
        }

        startActivity(intent)
    }
}
