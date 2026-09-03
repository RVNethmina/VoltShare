// -----------------------------------------------------------------------------
// File        : OperatorHomeActivity.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Grid operator home screen. Shows the live workload counts
//               produced by the Web API: bookings awaiting approval, approved
//               transfers still to come, transfers completed today and how many
//               microgrid nodes are in service.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui.operator

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
import lk.sliit.voltshare.databinding.ActivityOperatorHomeBinding
import lk.sliit.voltshare.ui.LoginActivity
import lk.sliit.voltshare.util.SystemBars

class OperatorHomeActivity : AppCompatActivity() {

    private lateinit var binding: ActivityOperatorHomeBinding

    /**
     * Builds the screen, labels the tiles and loads the dashboard.
     */
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        binding = ActivityOperatorHomeBinding.inflate(layoutInflater)
        setContentView(binding.root)

        val session = AppServices.store.getSession()
        binding.textGreeting.text = getString(
            R.string.greeting_format,
            session?.fullName?.substringBefore(' ') ?: "there"
        )

        labelTiles()

        binding.buttonSignOut.setOnClickListener { signOut() }
        binding.swipeRefresh.setOnRefreshListener { loadDashboard(showSpinner = false) }

        // The header would otherwise be drawn underneath the status bar.
        SystemBars.applyInsets(binding.headerBar, binding.progress)

        // The first load is left to onResume, which always runs after
        // onCreate; calling it here as well fetched the dashboard twice.
    }

    /** Refreshes when the operator returns from finalising a transfer. */
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

        binding.tileCompletedToday.textTileLabel.text = getString(R.string.tile_completed_today)
        binding.tileCompletedToday.textTileHint.text = getString(R.string.tile_completed_today_hint)

        binding.tileStations.textTileLabel.text = getString(R.string.tile_active_nodes)
        binding.tileStations.textTileHint.text = getString(R.string.tile_active_nodes_hint)
    }

    /**
     * Fetches the operator figures from the service.
     */
    private fun loadDashboard(showSpinner: Boolean) {
        if (showSpinner) binding.progress.visibility = View.VISIBLE
        binding.textError.visibility = View.GONE

        lifecycleScope.launch {
            try {
                val dashboard = ApiClient.call { AppServices.api.operatorDashboard() }

                binding.tilePending.textTileValue.text = dashboard.pendingCount.toString()
                binding.tileApproved.textTileValue.text = dashboard.approvedFutureCount.toString()
                binding.tileCompletedToday.textTileValue.text =
                    dashboard.completedTodayCount.toString()
                binding.tileStations.textTileValue.text = dashboard.activeStationCount.toString()
            } catch (error: ApiException) {
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
     * Clears the local session and returns to the sign in screen.
     */
    private fun signOut() {
        AppServices.signOut()

        val intent = Intent(this, LoginActivity::class.java)
        intent.flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
        startActivity(intent)
        finish()
    }
}
