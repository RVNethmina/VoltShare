// -----------------------------------------------------------------------------
// File        : BookingSummaryActivity.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Confirmation screen shown after a booking is created, changed
//               or cancelled, as the specification requires after each action.
//
//               The heading and message are the ones the Web API returned, so
//               the user is told exactly what the service recorded rather than
//               a message this application composed for itself.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui.prosumer

import android.app.Activity
import android.content.Context
import android.content.Intent
import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
import androidx.core.content.ContextCompat
import lk.sliit.voltshare.R
import lk.sliit.voltshare.data.remote.ApiConstants
import lk.sliit.voltshare.data.remote.ReservationSummaryDto
import lk.sliit.voltshare.databinding.ActivityBookingSummaryBinding
import lk.sliit.voltshare.util.Formatters
import lk.sliit.voltshare.util.StatusStyles
import lk.sliit.voltshare.util.SystemBars

class BookingSummaryActivity : AppCompatActivity() {

    private lateinit var binding: ActivityBookingSummaryBinding

    /**
     * Displays the summary the service produced.
     */
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        binding = ActivityBookingSummaryBinding.inflate(layoutInflater)
        setContentView(binding.root)

        // This screen is light at the top, so the status bar needs dark
        // icons or the clock is unreadable, and the content needs the
        // status bar inset so it does not sit underneath it.
        SystemBars.useDarkStatusBarIcons(window, true)
        SystemBars.applyInsets(binding.textIcon)

        val action = intent.getStringExtra(EXTRA_ACTION).orEmpty()
        val message = intent.getStringExtra(EXTRA_MESSAGE).orEmpty()
        val reservationId = intent.getStringExtra(EXTRA_RESERVATION_ID).orEmpty()

        binding.textAction.text = "Booking ${action.lowercase()}"
        binding.textMessage.text = message

        binding.textRef.text = intent.getStringExtra(EXTRA_REF).orEmpty()

        val status = intent.getStringExtra(EXTRA_STATUS).orEmpty()
        binding.textStatus.text = status
        StatusStyles.apply(binding.textStatus, status)

        binding.textStation.text = "Station: ${intent.getStringExtra(EXTRA_STATION) ?: "—"}"
        binding.textWindow.text = "Window: " + Formatters.window(
            intent.getStringExtra(EXTRA_START), intent.getStringExtra(EXTRA_END)
        )
        binding.textEnergy.text = "Energy: " +
            Formatters.energy(intent.getDoubleExtra(EXTRA_ENERGY, 0.0)) +
            " (${intent.getStringExtra(EXTRA_TYPE)})"

        // A cancelled booking is a normal outcome rather than a success, so the
        // marker is toned down instead of showing a green tick.
        if (status == ApiConstants.STATUS_CANCELLED) {
            binding.textIcon.text = "✕"
            binding.textIcon.setBackgroundColor(
                ContextCompat.getColor(this, R.color.status_cancelled_bg)
            )
            binding.textIcon.setTextColor(
                ContextCompat.getColor(this, R.color.status_cancelled_fg)
            )

            // There is nothing useful left to open for a cancelled booking.
            binding.buttonViewBooking.visibility = android.view.View.GONE
        }

        binding.buttonViewBooking.setOnClickListener {
            val intentDetail = Intent(this, BookingDetailActivity::class.java)
            intentDetail.putExtra(BookingDetailActivity.EXTRA_RESERVATION_ID, reservationId)
            startActivity(intentDetail)
            finish()
        }

        // Returns to the home screen, clearing the booking screens behind it so
        // the back button cannot walk into a form that has already been sent.
        binding.buttonDone.setOnClickListener {
            val home = Intent(this, ProsumerHomeActivity::class.java)
            home.flags = Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_SINGLE_TOP
            startActivity(home)
            finish()
        }
    }

    companion object {
        private const val EXTRA_ACTION = "action"
        private const val EXTRA_MESSAGE = "message"
        private const val EXTRA_RESERVATION_ID = "reservation_id"
        private const val EXTRA_REF = "reservation_no"
        private const val EXTRA_STATUS = "status"
        private const val EXTRA_STATION = "station_name"
        private const val EXTRA_START = "start_utc"
        private const val EXTRA_END = "end_utc"
        private const val EXTRA_ENERGY = "energy"
        private const val EXTRA_TYPE = "type"

        /**
         * Opens the summary for a completed booking action.
         *
         * The values are passed as individual extras rather than a serialised
         * object, which keeps the screen independent of how the response type
         * happens to be defined.
         */
        fun start(context: Context, summary: ReservationSummaryDto) {
            val reservation = summary.reservation

            val intent = Intent(context, BookingSummaryActivity::class.java).apply {
                putExtra(EXTRA_ACTION, summary.action)
                putExtra(EXTRA_MESSAGE, summary.message)
                putExtra(EXTRA_RESERVATION_ID, reservation.id)
                putExtra(EXTRA_REF, reservation.reservationNo)
                putExtra(EXTRA_STATUS, reservation.status)
                putExtra(EXTRA_STATION, reservation.stationName)
                putExtra(EXTRA_START, reservation.reservationStartUtc)
                putExtra(EXTRA_END, reservation.reservationEndUtc)
                putExtra(EXTRA_ENERGY, reservation.energyKwh)
                putExtra(EXTRA_TYPE, reservation.type)
            }

            context.startActivity(intent)

            // Finishing the caller stops the back button returning to a form
            // whose request has already been carried out.
            if (context is Activity) {
                context.finish()
            }
        }
    }
}
