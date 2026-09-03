// -----------------------------------------------------------------------------
// File        : BookingListActivity.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : The prosumer's booking history, with the status filter and the
//               search the specification requires. The service applies both,
//               and restricts the results to the signed in prosumer whatever
//               this screen asks for. Results are cached in SQLite so the
//               history is still readable without a connection.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui.prosumer

import android.content.Intent
import android.os.Bundle
import android.view.View
import android.view.inputmethod.EditorInfo
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import androidx.recyclerview.widget.LinearLayoutManager
import kotlinx.coroutines.launch
import lk.sliit.voltshare.AppServices
import lk.sliit.voltshare.R
import lk.sliit.voltshare.data.remote.ApiClient
import lk.sliit.voltshare.data.remote.ApiConstants
import lk.sliit.voltshare.data.remote.ApiException
import lk.sliit.voltshare.databinding.ActivityBookingListBinding
import lk.sliit.voltshare.util.SystemBars

class BookingListActivity : AppCompatActivity() {

    private lateinit var binding: ActivityBookingListBinding
    private lateinit var adapter: BookingAdapter

    // Null means every status, which is what the "All" chip selects.
    private var statusFilter: String? = null
    private var searchTerm: String? = null

    /**
     * Builds the screen and wires the filter chips and the search box.
     */
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        binding = ActivityBookingListBinding.inflate(layoutInflater)
        setContentView(binding.root)

        SystemBars.applyInsets(binding.headerBar)

        adapter = BookingAdapter { reservation ->
            val intent = Intent(this, BookingDetailActivity::class.java)
            intent.putExtra(BookingDetailActivity.EXTRA_RESERVATION_ID, reservation.id)
            startActivity(intent)
        }

        binding.recyclerBookings.layoutManager = LinearLayoutManager(this)
        binding.recyclerBookings.adapter = adapter

        binding.chipGroupStatus.setOnCheckedStateChangeListener { _, checkedIds ->
            statusFilter = when (checkedIds.firstOrNull()) {
                R.id.chipPending -> ApiConstants.STATUS_PENDING
                R.id.chipApproved -> ApiConstants.STATUS_APPROVED
                R.id.chipCompleted -> ApiConstants.STATUS_COMPLETED
                R.id.chipCancelled -> ApiConstants.STATUS_CANCELLED
                else -> null
            }

            loadBookings(showSpinner = true)
        }

        // Searching on the keyboard action rather than on every keystroke, so
        // typing a reference does not fire a request per character.
        binding.inputSearch.setOnEditorActionListener { _, actionId, _ ->
            if (actionId == EditorInfo.IME_ACTION_SEARCH) {
                searchTerm = binding.inputSearch.text?.toString()?.trim()?.ifEmpty { null }
                loadBookings(showSpinner = true)
                true
            } else {
                false
            }
        }

        binding.swipeRefresh.setOnRefreshListener { loadBookings(showSpinner = false) }
    }

    /**
     * Reloads on return, so a booking cancelled on the detail screen is
     * reflected immediately.
     */
    override fun onResume() {
        super.onResume()
        loadBookings(showSpinner = false)
    }

    /**
     * Fetches the bookings matching the current filter and search term.
     */
    private fun loadBookings(showSpinner: Boolean) {
        if (showSpinner) binding.progress.visibility = View.VISIBLE
        binding.textNotice.visibility = View.GONE

        lifecycleScope.launch {
            try {
                val bookings = ApiClient.call {
                    AppServices.api.listReservations(status = statusFilter, search = searchTerm)
                }

                // The unfiltered list is what gets cached, so the local copy is
                // a complete history rather than whatever filter happened to be
                // selected when it was last refreshed.
                if (statusFilter == null && searchTerm == null) {
                    AppServices.store.replaceBookings(bookings)
                }

                showBookings(bookings)
            } catch (error: ApiException) {
                showCachedFallback(error)
            } finally {
                binding.progress.visibility = View.GONE
                binding.swipeRefresh.isRefreshing = false
            }
        }
    }

    /**
     * Falls back to the locally stored history when the service cannot be
     * reached, applying the same filter and search to the cached rows.
     */
    private fun showCachedFallback(error: ApiException) {
        val cached = AppServices.store.getCachedBookings(statusFilter, searchTerm)

        if (cached.isEmpty()) {
            binding.textNotice.text = error.message
            binding.textNotice.visibility = View.VISIBLE
            showBookings(emptyList())
            return
        }

        binding.textNotice.text = "Showing your saved bookings. ${error.message}"
        binding.textNotice.visibility = View.VISIBLE

        showBookings(cached)
    }

    /** Puts the rows on screen, or shows the empty message. */
    private fun showBookings(bookings: List<lk.sliit.voltshare.data.remote.ReservationDto>) {
        adapter.submit(bookings)
        binding.textEmpty.visibility = if (bookings.isEmpty()) View.VISIBLE else View.GONE
    }
}
