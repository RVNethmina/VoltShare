// -----------------------------------------------------------------------------
// File        : BookingAdapter.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : RecyclerView adapter for the booking list. Shows the values the
//               Web API returned, including the status, and reports taps back
//               to the activity.
// Author      : IT22129376 - Wijesinghe W.A.C.S.
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui.prosumer

import android.view.LayoutInflater
import android.view.ViewGroup
import androidx.recyclerview.widget.RecyclerView
import lk.sliit.voltshare.data.remote.ReservationDto
import lk.sliit.voltshare.databinding.ItemBookingBinding
import lk.sliit.voltshare.util.Formatters
import lk.sliit.voltshare.util.StatusStyles

class BookingAdapter(
    private val onClick: (ReservationDto) -> Unit
) : RecyclerView.Adapter<BookingAdapter.BookingViewHolder>() {

    private val bookings = mutableListOf<ReservationDto>()

    /**
     * Replaces the whole list with a freshly loaded one.
     */
    fun submit(newBookings: List<ReservationDto>) {
        bookings.clear()
        bookings.addAll(newBookings)
        notifyDataSetChanged()
    }

    /** Inflates one booking row for the RecyclerView. */
    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): BookingViewHolder {
        val binding = ItemBookingBinding.inflate(
            LayoutInflater.from(parent.context), parent, false
        )
        return BookingViewHolder(binding)
    }

    /** Fills a recycled row with the booking at this position. */
    override fun onBindViewHolder(holder: BookingViewHolder, position: Int) {
        holder.bind(bookings[position])
    }

    /** Number of bookings currently shown. */
    override fun getItemCount(): Int = bookings.size

    inner class BookingViewHolder(
        private val binding: ItemBookingBinding
    ) : RecyclerView.ViewHolder(binding.root) {

        /**
         * Fills one card with the details of a booking.
         */
        fun bind(reservation: ReservationDto) {
            binding.textRef.text = reservation.reservationNo
            binding.textStation.text = reservation.stationName ?: "—"
            binding.textWindow.text = Formatters.window(
                reservation.reservationStartUtc, reservation.reservationEndUtc
            )
            binding.textType.text = reservation.type
            binding.textEnergy.text = Formatters.energy(reservation.energyKwh)

            binding.textStatus.text = reservation.status
            StatusStyles.apply(binding.textStatus, reservation.status)

            binding.root.setOnClickListener { onClick(reservation) }
        }
    }
}
