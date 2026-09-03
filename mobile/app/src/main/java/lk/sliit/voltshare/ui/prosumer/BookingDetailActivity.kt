// -----------------------------------------------------------------------------
// File        : BookingDetailActivity.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : One booking in full. Displays the transaction QR code once the
//               booking has been approved, and offers the modify and cancel
//               actions only when the service says they are still available.
//
//               The twelve hour notice rule is never evaluated here: the
//               canBeModified and canBeCancelled flags are read from the
//               service, and the service enforces the rule again on the
//               request itself.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui.prosumer

import android.content.Intent
import android.os.Bundle
import android.view.View
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.launch
import lk.sliit.voltshare.AppServices
import lk.sliit.voltshare.data.remote.ApiClient
import lk.sliit.voltshare.data.remote.ApiConstants
import lk.sliit.voltshare.data.remote.ApiException
import lk.sliit.voltshare.data.remote.ReservationDto
import lk.sliit.voltshare.databinding.ActivityBookingDetailBinding
import lk.sliit.voltshare.util.Formatters
import lk.sliit.voltshare.util.QrGenerator
import lk.sliit.voltshare.util.StatusStyles
import lk.sliit.voltshare.util.SystemBars

class BookingDetailActivity : AppCompatActivity() {

    private lateinit var binding: ActivityBookingDetailBinding
    private lateinit var reservationId: String

    private var reservation: ReservationDto? = null

    /**
     * Builds the screen and wires the two actions.
     */
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        binding = ActivityBookingDetailBinding.inflate(layoutInflater)
        setContentView(binding.root)

        SystemBars.applyInsets(binding.headerBar, binding.progress)

        reservationId = intent.getStringExtra(EXTRA_RESERVATION_ID).orEmpty()

        binding.buttonCancel.setOnClickListener { confirmCancel() }

        binding.buttonModify.setOnClickListener {
            val intent = Intent(this, CreateBookingActivity::class.java)
            intent.putExtra(CreateBookingActivity.EXTRA_EDIT_RESERVATION_ID, reservationId)
            startActivity(intent)
        }
    }

    /**
     * Reloads each time the screen is shown, so a change made elsewhere, such
     * as an approval, appears without the user having to refresh.
     */
    override fun onResume() {
        super.onResume()
        loadBooking()
    }

    /**
     * Fetches the booking and, when it has been approved, its QR token.
     */
    private fun loadBooking() {
        binding.progress.visibility = View.VISIBLE
        binding.textError.visibility = View.GONE

        lifecycleScope.launch {
            try {
                val loaded = ApiClient.call { AppServices.api.getReservation(reservationId) }
                reservation = loaded

                show(loaded)

                // The token only exists for an approved booking, so it is only
                // requested when the service says one has been issued.
                if (loaded.hasQrCode) {
                    loadQrCode()
                } else {
                    binding.cardQr.visibility = View.GONE
                }
            } catch (error: ApiException) {
                binding.textError.text = error.message
                binding.textError.visibility = View.VISIBLE
            } finally {
                binding.progress.visibility = View.GONE
            }
        }
    }

    /**
     * Fetches the signed token and draws it as a QR code.
     */
    private suspend fun loadQrCode() {
        try {
            val qr = ApiClient.call { AppServices.api.getQrCode(reservationId) }
            val bitmap = QrGenerator.render(qr.token)

            if (bitmap == null) {
                binding.cardQr.visibility = View.GONE
                return
            }

            binding.imageQr.setImageBitmap(bitmap)
            binding.cardQr.visibility = View.VISIBLE
        } catch (error: ApiException) {
            // Failing to draw the code must not hide the rest of the booking,
            // so the panel is simply left out.
            binding.cardQr.visibility = View.GONE
        }
    }

    /**
     * Puts the booking on screen and shows only the actions still available.
     */
    private fun show(booking: ReservationDto) {
        binding.textRef.text = booking.reservationNo
        binding.textStatus.text = booking.status
        StatusStyles.apply(binding.textStatus, booking.status)

        binding.textStation.text = "Station: ${booking.stationName ?: "—"}"
        binding.textWindow.text = "Window: " +
            Formatters.window(booking.reservationStartUtc, booking.reservationEndUtc)
        binding.textType.text = "Transfer: ${booking.type}"
        binding.textEnergy.text = "Energy: ${Formatters.energy(booking.energyKwh)}"
        binding.textCreated.text = "Requested ${Formatters.dateTime(booking.createdAtUtc)}"

        binding.buttonModify.visibility = if (booking.canBeModified) View.VISIBLE else View.GONE
        binding.buttonCancel.visibility = if (booking.canBeCancelled) View.VISIBLE else View.GONE

        // Explain why the actions are missing, rather than leaving the user to
        // wonder where the buttons went.
        val isOpen = booking.status == ApiConstants.STATUS_PENDING ||
            booking.status == ApiConstants.STATUS_APPROVED

        if (!booking.canBeCancelled && !booking.canBeModified) {
            binding.textLocked.visibility = View.VISIBLE
            binding.textLocked.text = if (isOpen) {
                "This booking starts soon, so it can no longer be changed or cancelled. " +
                    "The service requires at least 12 hours notice."
            } else {
                "This booking is ${booking.status.lowercase()} and no further action is available."
            }
        } else {
            binding.textLocked.visibility = View.GONE
        }
    }

    /**
     * Asks before cancelling, because the action cannot be undone.
     */
    private fun confirmCancel() {
        val booking = reservation ?: return

        AlertDialog.Builder(this)
            .setTitle("Cancel this booking?")
            .setMessage(
                "Booking ${booking.reservationNo} will be withdrawn and its place " +
                    "returned to the window. This cannot be undone."
            )
            .setNegativeButton("Keep booking", null)
            .setPositiveButton("Cancel booking") { _, _ -> cancelBooking() }
            .show()
    }

    /**
     * Cancels the booking and shows the summary the service returned.
     */
    private fun cancelBooking() {
        binding.progress.visibility = View.VISIBLE
        binding.textError.visibility = View.GONE

        lifecycleScope.launch {
            try {
                val summary = ApiClient.call {
                    AppServices.api.cancelReservation(reservationId)
                }

                // Every booking action ends on the summary screen, which is
                // what the specification asks for.
                BookingSummaryActivity.start(this@BookingDetailActivity, summary)
                finish()
            } catch (error: ApiException) {
                // A cancellation inside the twelve hour window is refused here
                // with the service's own explanation.
                binding.textError.text = error.message
                binding.textError.visibility = View.VISIBLE
            } finally {
                binding.progress.visibility = View.GONE
            }
        }
    }

    companion object {
        const val EXTRA_RESERVATION_ID = "reservation_id"
    }
}
