// -----------------------------------------------------------------------------
// File        : ProsumerHomeActivity.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Prosumer home screen. Shows the booking counts and the next
//               upcoming reservation, every one of which is computed by the
//               Web API and simply displayed here.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui.prosumer

import android.content.Intent
import android.os.Bundle
import android.view.View
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.launch
import lk.sliit.voltshare.AppServices
import lk.sliit.voltshare.R
import lk.sliit.voltshare.data.remote.ApiClient
import lk.sliit.voltshare.data.remote.ApiException
import lk.sliit.voltshare.data.remote.ReservationDto
import lk.sliit.voltshare.databinding.ActivityProsumerHomeBinding
import lk.sliit.voltshare.ui.LoginActivity
import lk.sliit.voltshare.util.Formatters
import lk.sliit.voltshare.util.SystemBars
import lk.sliit.voltshare.util.StatusStyles

class ProsumerHomeActivity : AppCompatActivity() {

    private lateinit var binding: ActivityProsumerHomeBinding

    /**
     * Builds the screen, labels the tiles and loads the dashboard.
     */
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        binding = ActivityProsumerHomeBinding.inflate(layoutInflater)
        setContentView(binding.root)

        val session = AppServices.store.getSession()

        // The greeting comes from the session held in SQLite, so the screen has
        // something to show immediately without waiting for the network.
        binding.textGreeting.text = getString(
            R.string.greeting_format,
            session?.fullName?.substringBefore(' ') ?: "there"
        )
        binding.textNic.text = getString(R.string.nic_format, session?.userId.orEmpty())

        // The tile labels never change, so they are set once here and only the
        // values are refreshed when the dashboard loads.
        labelTiles()

        binding.buttonSignOut.setOnClickListener { signOut() }

        binding.buttonBookEnergy.setOnClickListener {
            startActivity(Intent(this, CreateBookingActivity::class.java))
        }

        binding.buttonMyBookings.setOnClickListener {
            startActivity(Intent(this, BookingListActivity::class.java))
        }

        binding.buttonFindStations.setOnClickListener {
            startActivity(Intent(this, StationsActivity::class.java))
        }
        binding.swipeRefresh.setOnRefreshListener { loadDashboard(showSpinner = false) }

        // The header would otherwise be drawn underneath the status bar.
        SystemBars.applyInsets(binding.headerBar, binding.progress)

        // The first load is left to onResume, which always runs after
        // onCreate; calling it here as well fetched the dashboard twice.
    }

    /**
     * Reloads whenever the screen is returned to, so a booking made or
     * cancelled on another screen is reflected straight away.
     */
    override fun onResume() {
        super.onResume()
        loadDashboard(showSpinner = false)
    }

    /** Sets the fixed caption and hint on each tile. */
    private fun labelTiles() {
        binding.tilePending.textTileLabel.text = getString(R.string.tile_awaiting_approval)
        binding.tilePending.textTileHint.text = getString(R.string.tile_awaiting_approval_hint)

        binding.tileApproved.textTileLabel.text = getString(R.string.tile_approved_upcoming)
        binding.tileApproved.textTileHint.text = getString(R.string.tile_approved_upcoming_hint)

        binding.tileCompleted.textTileLabel.text = getString(R.string.tile_completed)
        binding.tileCompleted.textTileHint.text = getString(R.string.tile_completed_hint)

        binding.tileCancelled.textTileLabel.text = getString(R.string.tile_cancelled)
        binding.tileCancelled.textTileHint.text = getString(R.string.tile_cancelled_hint)
    }

    /**
     * Fetches the dashboard figures from the service.
     */
    private fun loadDashboard(showSpinner: Boolean) {
        if (showSpinner) binding.progress.visibility = View.VISIBLE
        binding.textError.visibility = View.GONE

        lifecycleScope.launch {
            try {
                val dashboard = ApiClient.call { AppServices.api.prosumerDashboard() }

                binding.tilePending.textTileValue.text = dashboard.pendingCount.toString()
                binding.tileApproved.textTileValue.text = dashboard.approvedFutureCount.toString()
                binding.tileCompleted.textTileValue.text = dashboard.completedCount.toString()
                binding.tileCancelled.textTileValue.text = dashboard.cancelledCount.toString()

                showNextBooking(dashboard.nextReservation)
            } catch (error: ApiException) {
                // An expired session sends the user back to sign in; anything
                // else is reported without throwing the screen away, so the
                // figures already on display stay readable.
                if (error.isUnauthorised) {
                    signOut()
                    return@launch
                }

                binding.textError.text = error.message
                binding.textError.visibility = View.VISIBLE
            } finally {
                binding.progress.visibility = View.GONE
                binding.swipeRefresh.isRefreshing = false
            }
        }
    }

    /**
     * Shows the next upcoming booking, or an empty message when there is none.
     */
    private fun showNextBooking(reservation: ReservationDto?) {
        if (reservation == null) {
            binding.cardNextBooking.visibility = View.GONE
            binding.textNoBooking.visibility = View.VISIBLE
            return
        }

        binding.cardNextBooking.visibility = View.VISIBLE
        binding.textNoBooking.visibility = View.GONE

        binding.textNextRef.text = reservation.reservationNo
        binding.textNextStation.text = reservation.stationName ?: "—"
        binding.textNextWindow.text = Formatters.window(
            reservation.reservationStartUtc, reservation.reservationEndUtc
        )

        binding.textNextStatus.text = reservation.status
        StatusStyles.apply(binding.textNextStatus, reservation.status)
    }

    /**
     * Clears the local session and returns to the sign in screen.
     */
    private fun signOut() {
        AppServices.signOut()

        val intent = Intent(this, LoginActivity::class.java)

        // Clearing the task stops the back button returning to a signed in
        // screen after the session has been discarded.
        intent.flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
        startActivity(intent)
        finish()
    }
}
