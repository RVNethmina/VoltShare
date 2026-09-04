// -----------------------------------------------------------------------------
// File        : ScanResultActivity.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Shows the booking a scanned QR code identified, and lets the
//               grid operator finalise the energy transfer.
//
//               The booking details were returned by the Web API after it
//               checked the token's signature, so the operator is shown what
//               the service holds rather than anything read out of the code.
//               Completing is refused by the service if the booking has
//               already been finished, which is what makes a code single use.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui.operator

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.view.View
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.core.widget.TextViewCompat
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.launch
import lk.sliit.voltshare.AppServices
import lk.sliit.voltshare.R
import lk.sliit.voltshare.data.remote.ApiClient
import lk.sliit.voltshare.data.remote.ApiException
import lk.sliit.voltshare.data.remote.ReservationDto
import lk.sliit.voltshare.databinding.ActivityScanResultBinding
import lk.sliit.voltshare.util.Formatters
import lk.sliit.voltshare.util.StatusStyles
import lk.sliit.voltshare.util.SystemBars

class ScanResultActivity : AppCompatActivity() {

    private lateinit var binding: ActivityScanResultBinding
    private lateinit var reservationId: String

    /**
     * Displays the verified booking.
     */
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        binding = ActivityScanResultBinding.inflate(layoutInflater)
        setContentView(binding.root)

        SystemBars.applyInsets(binding.headerBar, binding.progress)

        reservationId = intent.getStringExtra(EXTRA_RESERVATION_ID).orEmpty()

        binding.textRef.text = intent.getStringExtra(EXTRA_REF).orEmpty()

        val status = intent.getStringExtra(EXTRA_STATUS).orEmpty()
        binding.textStatus.text = status
        StatusStyles.apply(binding.textStatus, status)

        val name = intent.getStringExtra(EXTRA_PROSUMER_NAME)
        val nic = intent.getStringExtra(EXTRA_PROSUMER_NIC).orEmpty()
        // The row labels are in the layout, so only the values are set here.
        binding.textProsumer.text = "${name ?: "—"} ($nic)"

        binding.textStation.text = intent.getStringExtra(EXTRA_STATION) ?: "—"
        binding.textWindow.text = Formatters.window(
            intent.getStringExtra(EXTRA_START), intent.getStringExtra(EXTRA_END)
        )
        binding.textEnergy.text =
            Formatters.energy(intent.getDoubleExtra(EXTRA_ENERGY, 0.0)) +
                " (${intent.getStringExtra(EXTRA_TYPE)})"

        binding.buttonBack.setOnClickListener { finish() }
        binding.buttonComplete.setOnClickListener { complete() }
        binding.buttonDone.setOnClickListener { finish() }
    }

    /**
     * Finalises the energy transfer.
     */
    private fun complete() {
        binding.progress.visibility = View.VISIBLE
        binding.textError.visibility = View.GONE
        binding.buttonComplete.isEnabled = false

        lifecycleScope.launch {
            try {
                val summary = ApiClient.call {
                    AppServices.api.completeReservation(reservationId)
                }

                // The confirmation wording comes from the service.
                binding.textStatus.text = summary.reservation.status
                StatusStyles.apply(binding.textStatus, summary.reservation.status)

                binding.buttonComplete.visibility = View.GONE

                // The same panel is reused for the confirmation, so it is
                // switched from the refusal style to the confirming one.
                binding.textError.setBackgroundResource(R.drawable.bg_notice_success)
                binding.textError.setCompoundDrawablesRelativeWithIntrinsicBounds(
                    R.drawable.ic_check_circle, 0, 0, 0
                )
                TextViewCompat.setCompoundDrawableTintList(
                    binding.textError,
                    ContextCompat.getColorStateList(this@ScanResultActivity, R.color.success_fg)
                )
                binding.textError.setTextColor(
                    ContextCompat.getColor(this@ScanResultActivity, R.color.success_fg)
                )
                binding.textError.text = summary.message
                binding.textError.visibility = View.VISIBLE
            } catch (error: ApiException) {
                // A second attempt on the same booking is refused here, with
                // the service's own explanation.
                binding.textError.text = error.message
                binding.textError.visibility = View.VISIBLE
                binding.buttonComplete.isEnabled = true
            } finally {
                binding.progress.visibility = View.GONE
            }
        }
    }

    companion object {
        private const val EXTRA_RESERVATION_ID = "reservation_id"
        private const val EXTRA_REF = "reservation_no"
        private const val EXTRA_STATUS = "status"
        private const val EXTRA_PROSUMER_NAME = "prosumer_name"
        private const val EXTRA_PROSUMER_NIC = "prosumer_nic"
        private const val EXTRA_STATION = "station_name"
        private const val EXTRA_START = "start_utc"
        private const val EXTRA_END = "end_utc"
        private const val EXTRA_ENERGY = "energy"
        private const val EXTRA_TYPE = "type"

        /**
         * Opens the screen for a booking the service has just verified.
         */
        fun start(context: Context, reservation: ReservationDto) {
            val intent = Intent(context, ScanResultActivity::class.java).apply {
                putExtra(EXTRA_RESERVATION_ID, reservation.id)
                putExtra(EXTRA_REF, reservation.reservationNo)
                putExtra(EXTRA_STATUS, reservation.status)
                putExtra(EXTRA_PROSUMER_NAME, reservation.prosumerName)
                putExtra(EXTRA_PROSUMER_NIC, reservation.prosumerNic)
                putExtra(EXTRA_STATION, reservation.stationName)
                putExtra(EXTRA_START, reservation.reservationStartUtc)
                putExtra(EXTRA_END, reservation.reservationEndUtc)
                putExtra(EXTRA_ENERGY, reservation.energyKwh)
                putExtra(EXTRA_TYPE, reservation.type)
            }

            if (context is Activity) {
                context.startActivity(intent)
            }
        }
    }
}
