// -----------------------------------------------------------------------------
// File        : ProfileActivity.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : The prosumer's own account. Allows the editable details to be
//               changed, and raises a request for the account to be closed.
//
//               The request is only a flag: the specification requires that
//               deactivation and reactivation are carried out by a back-office
//               officer, so this screen never deactivates anything itself.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui.prosumer

import android.os.Bundle
import android.view.View
import com.google.android.material.dialog.MaterialAlertDialogBuilder
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.launch
import lk.sliit.voltshare.AppServices
import lk.sliit.voltshare.R
import lk.sliit.voltshare.data.remote.ApiClient
import lk.sliit.voltshare.data.remote.ApiException
import lk.sliit.voltshare.data.remote.UpdateProfileRequest
import lk.sliit.voltshare.data.remote.UserDto
import lk.sliit.voltshare.databinding.ActivityProfileBinding
import lk.sliit.voltshare.util.SystemBars

class ProfileActivity : AppCompatActivity() {

    private lateinit var binding: ActivityProfileBinding
    private var nic: String = ""

    /**
     * Builds the screen from the stored session, then refreshes from the API.
     */
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        binding = ActivityProfileBinding.inflate(layoutInflater)
        setContentView(binding.root)

        SystemBars.applyInsets(binding.headerBar, binding.progress)

        // The session held in SQLite fills the form immediately, so the screen
        // is usable before the network call returns.
        val session = AppServices.store.getSession()
        nic = session?.userId.orEmpty()

        binding.textNic.text = getString(R.string.nic_format, nic)
        binding.inputEmail.setText(session?.email.orEmpty())
        binding.inputFullName.setText(session?.fullName.orEmpty())
        binding.inputPhone.setText(session?.phone.orEmpty())
        binding.inputAddress.setText(session?.address.orEmpty())

        binding.buttonBack.setOnClickListener { finish() }
        binding.buttonSave.setOnClickListener { save() }
        binding.buttonRequestDeactivation.setOnClickListener { confirmDeactivationRequest() }

        loadProfile()
    }

    /**
     * Refreshes the profile from the service, which is the authority on it.
     */
    private fun loadProfile() {
        lifecycleScope.launch {
            try {
                val profile = ApiClient.call { AppServices.api.me() }
                applyProfile(profile)
            } catch (error: ApiException) {
                // The form is already populated from the session, so a failure
                // here only means the values may be slightly stale.
                showError(error.message)
            }
        }
    }

    /** Puts a freshly loaded profile into the form. */
    private fun applyProfile(profile: UserDto) {
        binding.inputEmail.setText(profile.email)
        binding.inputFullName.setText(profile.fullName)
        binding.inputPhone.setText(profile.phone.orEmpty())
        binding.inputAddress.setText(profile.address.orEmpty())

        // Once a closure has been requested the button would do nothing more,
        // so it is replaced with an explanation of what happens next.
        if (profile.deactivationRequested) {
            binding.textDeactivationPending.visibility = View.VISIBLE
            binding.buttonRequestDeactivation.visibility = View.GONE
        } else {
            binding.textDeactivationPending.visibility = View.GONE
            binding.buttonRequestDeactivation.visibility = View.VISIBLE
        }

        // Keep the local copy in step so the home screen greeting matches.
        AppServices.store.updateStoredProfile(profile)
    }

    /**
     * Saves the editable details.
     */
    private fun save() {
        val fullName = binding.inputFullName.text?.toString()?.trim().orEmpty()

        if (fullName.isEmpty()) {
            showError("Enter your full name.")
            return
        }

        setBusy(true)
        binding.textError.visibility = View.GONE
        binding.textNotice.visibility = View.GONE

        lifecycleScope.launch {
            try {
                val updated = ApiClient.call {
                    AppServices.api.updateProsumer(
                        nic,
                        UpdateProfileRequest(
                            fullName = fullName,
                            phone = binding.inputPhone.text?.toString()?.trim()?.ifEmpty { null },
                            address = binding.inputAddress.text?.toString()?.trim()?.ifEmpty { null }
                        )
                    )
                }

                applyProfile(updated)

                binding.textNotice.text = getString(R.string.profile_saved)
                binding.textNotice.visibility = View.VISIBLE
            } catch (error: ApiException) {
                showError(error.message)
            } finally {
                setBusy(false)
            }
        }
    }

    /**
     * Explains what a closure request means before raising it.
     */
    private fun confirmDeactivationRequest() {
        MaterialAlertDialogBuilder(this)
            .setTitle("Request account closure?")
            .setMessage(
                "The back-office team will be asked to close your VoltShare account. " +
                    "Your account stays usable until they act on the request, and only " +
                    "they can reopen it afterwards."
            )
            .setNegativeButton("Keep my account", null)
            .setPositiveButton("Send request") { _, _ -> requestDeactivation() }
            .show()
    }

    /**
     * Raises the closure request with the service.
     */
    private fun requestDeactivation() {
        setBusy(true)
        binding.textError.visibility = View.GONE
        binding.textNotice.visibility = View.GONE

        lifecycleScope.launch {
            try {
                val updated = ApiClient.call { AppServices.api.requestDeactivation(nic) }
                applyProfile(updated)

                binding.textNotice.text = getString(R.string.deactivation_sent)
                binding.textNotice.visibility = View.VISIBLE
            } catch (error: ApiException) {
                showError(error.message)
            } finally {
                setBusy(false)
            }
        }
    }

    /** Disables the form while a request is running. */
    private fun setBusy(isBusy: Boolean) {
        binding.progress.visibility = if (isBusy) View.VISIBLE else View.GONE
        binding.buttonSave.isEnabled = !isBusy
        binding.buttonRequestDeactivation.isEnabled = !isBusy
    }

    /** Displays a message from the service. */
    private fun showError(message: String) {
        binding.textError.text = message
        binding.textError.visibility = View.VISIBLE
    }
}
