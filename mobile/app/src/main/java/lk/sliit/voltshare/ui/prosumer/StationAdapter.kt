// -----------------------------------------------------------------------------
// File        : StationAdapter.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : RecyclerView adapter for the microgrid node list. Displays the
//               values supplied by the Web API, including the distance it
//               calculated, and reports taps back to the activity.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui.prosumer

import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.recyclerview.widget.RecyclerView
import lk.sliit.voltshare.data.remote.StationDto
import lk.sliit.voltshare.databinding.ItemStationBinding
import lk.sliit.voltshare.util.Formatters

/**
 * A station as the list needs it: the node itself, and how far away it is when
 * the service was able to work that out.
 */
data class StationRow(
    val station: StationDto,
    val distanceMeters: Double?
)

class StationAdapter(
    private val onClick: (StationDto) -> Unit
) : RecyclerView.Adapter<StationAdapter.StationViewHolder>() {

    private val rows = mutableListOf<StationRow>()

    /**
     * Replaces the whole list with a freshly loaded one.
     */
    fun submit(newRows: List<StationRow>) {
        rows.clear()
        rows.addAll(newRows)

        // The list is replaced wholesale on every load rather than diffed,
        // because it is short and always arrives complete from the service.
        notifyDataSetChanged()
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): StationViewHolder {
        val binding = ItemStationBinding.inflate(
            LayoutInflater.from(parent.context), parent, false
        )
        return StationViewHolder(binding)
    }

    override fun onBindViewHolder(holder: StationViewHolder, position: Int) {
        holder.bind(rows[position])
    }

    override fun getItemCount(): Int = rows.size

    inner class StationViewHolder(
        private val binding: ItemStationBinding
    ) : RecyclerView.ViewHolder(binding.root) {

        /**
         * Fills one card with the details of a node.
         */
        fun bind(row: StationRow) {
            val station = row.station

            binding.textName.text = station.name
            binding.textAddress.text = "${station.addressLine}, ${station.city}"
            binding.textCapacity.text = Formatters.energy(station.capacityKwh)
            binding.textBattery.text =
                "Battery ${station.availableBatterySlots} / ${station.totalBatterySlots}"
            binding.textHours.text =
                "${station.operatingHours.openTime}-${station.operatingHours.closeTime}"

            // The distance is only shown when the service supplied one, which
            // it does only when the request included the user's position.
            if (row.distanceMeters == null) {
                binding.textDistance.visibility = View.GONE
            } else {
                binding.textDistance.visibility = View.VISIBLE
                binding.textDistance.text = Formatters.distance(row.distanceMeters)
            }

            binding.root.setOnClickListener { onClick(station) }
        }
    }
}
