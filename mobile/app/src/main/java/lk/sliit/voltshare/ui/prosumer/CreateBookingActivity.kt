// -----------------------------------------------------------------------------
// File        : CreateBookingActivity.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Requests a new energy transfer booking, or moves an existing
//               one to a different window when opened from a booking that the
//               service says may still be modified.
//
//               No booking rule is applied here. Whether a window is within
//               the seven day horizon, whether it still has room, and whether
//               a change is still permitted are all decided by the Web API,
//               and its refusal is shown to the user unchanged.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui.prosumer

import android.os.Bundle
import android.view.View
import android.widget.ArrayAdapter
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import androidx.recyclerview.widget.LinearLayoutManager
import kotlinx.coroutines.launch
import lk.sliit.voltshare.AppServices
import lk.sliit.voltshare.R
import lk.sliit.voltshare.data.remote.ApiClient
import lk.sliit.voltshare.data.remote.ApiConstants
import lk.sliit.voltshare.data.remote.ApiException
import lk.sliit.voltshare.data.remote.CreateReservationRequest
import lk.sliit.voltshare.data.remote.SlotDto
import lk.sliit.voltshare.data.remote.StationDto
import lk.sliit.voltshare.data.remote.UpdateReservationRequest
import lk.sliit.voltshare.databinding.ActivityCreateBookingBinding
import lk.sliit.voltshare.util.SystemBars

class CreateBookingActivity : AppCompatActivity() {

    private lateinit var binding: ActivityCreateBookingBinding
    private lateinit var slotAdapter: SlotAdapter

    private var stations = listOf<StationDto>()
    private var selectedStation: StationDto? = null
    private var selectedSlot: SlotDto? = null

    // Set when the screen was opened to change an existing booking.
    private var editingReservationId: String? = null

    /**
     * Builds the screen, loads the nodes and wires the controls.
     */
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        binding = ActivityCreateBookingBinding.inflate(layoutInflater)
        setContentView(binding.root)

        SystemBars.applyInsets(binding.headerBar, binding.buttonConfirm)

        binding.buttonBack.setOnClickListener { finish() }

        editingReservationId = intent.getStringExtra(EXTRA_EDIT_RESERVATION_ID)

        if (editingReservationId != null) {
            binding.textTitle.setText(R.string.change_booking)
            binding.buttonConfirm.setText(R.string.save_changes)
        }

        slotAdapter = SlotAdapter { slot ->
            selectedSlot = slot
            updateConfirmState()
        }

        binding.recyclerSlots.layoutManager = LinearLayoutManager(this)
        binding.recyclerSlots.adapter = slotAdapter

        // Injection is the common case for a solar prosumer, so it starts
        // selected rather than leaving the user with no direction chosen.
        binding.toggleType.check(R.id.buttonInjection)
        binding.toggleType.addOnButtonCheckedListener { _, _, _ -> updateConfirmState() }

        binding.buttonConfirm.setOnClickListener { submit() }

        loadStations()
    }

    /**
     * Loads the nodes into the dropdown, then the windows for the first one.
     */
    private fun loadStations() {
        binding.progress.visibility = View.VISIBLE
        binding.textError.visibility = View.GONE

        lifecycleScope.launch {
            try {
                stations = ApiClient.call { AppServices.api.listStations() }

                if (stations.isEmpty()) {
                    showError("No microgrid nodes are currently in service.")
                    return@launch
                }

                val labels = stations.map { "${it.name} (${it.code})" }
                val adapter = ArrayAdapter(
                    this@CreateBookingActivity,
                    android.R.layout.simple_list_item_1,
                    labels
                )
                binding.inputStation.setAdapter(adapter)

                binding.inputStation.setOnItemClickListener { _, _, position, _ ->
                    selectStation(stations[position])
                }

                // Open on the first node so the user sees windows immediately
                // rather than an empty list.
                binding.inputStation.setText(labels.first(), false)
                selectStation(stations.first())
            } catch (error: ApiException) {
                showError(error.message)
            } finally {
                binding.progress.visibility = View.GONE
            }
        }
    }

    /**
     * Remembers the chosen node and loads the windows it offers.
     */
    private fun selectStation(station: StationDto) {
        selectedStation = station
        selectedSlot = null
        updateConfirmState()

        loadSlots(station.id)
    }

    /**
     * Fetches the bookable windows for a node.
     */
    private fun loadSlots(stationId: String) {
        binding.progress.visibility = View.VISIBLE
        binding.textError.visibility = View.GONE

        lifecycleScope.launch {
            try {
                val slots = ApiClient.call { AppServices.api.listSlots(stationId) }

                // Windows already in the past are dropped, because the service
                // would refuse them and there is no point offering them.
                val bookable = slots.filter { it.isActive }

                slotAdapter.submit(bookable)
                binding.textNoSlots.visibility =
                    if (bookable.isEmpty()) View.VISIBLE else View.GONE
            } catch (error: ApiException) {
                slotAdapter.submit(emptyList())
                showError(error.message)
            } finally {
                binding.progress.visibility = View.GONE
                updateConfirmState()
            }
        }
    }

    /** The confirm button is only usable once a window has been chosen. */
    private fun updateConfirmState() {
        binding.buttonConfirm.isEnabled = selectedSlot != null && selectedStation != null
    }

    /** The direction currently selected in the toggle. */
    private fun selectedType(): String =
        if (binding.toggleType.checkedButtonId == R.id.buttonWithdrawal) {
            ApiConstants.TYPE_WITHDRAWAL
        } else {
            ApiConstants.TYPE_INJECTION
        }

    /**
     * Sends the booking, then hands over to the summary screen.
     */
    private fun submit() {
        val slot = selectedSlot ?: return

        binding.progress.visibility = View.VISIBLE
        binding.textError.visibility = View.GONE
        binding.buttonConfirm.isEnabled = false

        lifecycleScope.launch {
            try {
                val editingId = editingReservationId

                val summary = if (editingId == null) {
                    ApiClient.call {
                        AppServices.api.createReservation(
                            CreateReservationRequest(slotId = slot.id, type = selectedType())
                        )
                    }
                } else {
                    ApiClient.call {
                        AppServices.api.updateReservation(
                            editingId,
                            UpdateReservationRequest(slotId = slot.id, type = selectedType())
                        )
                    }
                }

                // Every booking action ends on the summary screen, showing the
                // wording the service returned.
                BookingSummaryActivity.start(this@CreateBookingActivity, summary)
            } catch (error: ApiException) {
                // Covers the seven day horizon, a window that filled up, the
                // twelve hour notice rule and an inactive account, each with
                // the service's own explanation.
                showError(error.message)
                binding.buttonConfirm.isEnabled = true
            } finally {
                binding.progress.visibility = View.GONE
            }
        }
    }

    /** Displays a message from the service. */
    private fun showError(message: String) {
        binding.textError.text = message
        binding.textError.visibility = View.VISIBLE
    }

    companion object {
        /** Identifier of a booking being changed, when there is one. */
        const val EXTRA_EDIT_RESERVATION_ID = "edit_reservation_id"
    }
}
