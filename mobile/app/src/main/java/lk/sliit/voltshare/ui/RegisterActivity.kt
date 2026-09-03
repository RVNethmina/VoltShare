// -----------------------------------------------------------------------------
// File        : RegisterActivity.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Prosumer self registration, using the NIC as the primary key.
//               The Web API creates the account inactive, so the user is told
//               to wait for back-office approval rather than being signed in.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui

import android.os.Bundle
import android.view.View
import com.google.android.material.dialog.MaterialAlertDialogBuilder
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.launch
import lk.sliit.voltshare.AppServices
import lk.sliit.voltshare.data.remote.ApiClient
import lk.sliit.voltshare.data.remote.ApiException
import lk.sliit.voltshare.data.remote.RegisterProsumerRequest
import lk.sliit.voltshare.databinding.ActivityRegisterBinding
import lk.sliit.voltshare.util.SystemBars

class RegisterActivity : AppCompatActivity() {

    private lateinit var binding: ActivityRegisterBinding

    /**
     * Builds the screen and wires the register and back buttons.
     */
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        binding = ActivityRegisterBinding.inflate(layoutInflater)
        setContentView(binding.root)

        SystemBars.applyInsets(binding.headerBar)

        binding.buttonRegister.setOnClickListener { attemptRegister() }

        // Both of these return to the sign in screen this activity was opened
        // from: the arrow in the header, and the link under the form.
        binding.buttonBack.setOnClickListener { finish() }
        binding.buttonBackToLogin.setOnClickListener { finish() }
    }

    /**
     * Checks the required fields are present, then submits the registration.
     * Uniqueness of the NIC and the email address is decided by the service.
     */
    private fun attemptRegister() {
        val nic = binding.inputNic.text?.toString()?.trim()?.uppercase().orEmpty()
        val fullName = binding.inputFullName.text?.toString()?.trim().orEmpty()
        val email = binding.inputEmail.text?.toString()?.trim().orEmpty()
        val phone = binding.inputPhone.text?.toString()?.trim().orEmpty()
        val address = binding.inputAddress.text?.toString()?.trim().orEmpty()
        val password = binding.inputPassword.text?.toString().orEmpty()

        // Only presence and obvious length problems are checked on the device.
        // Everything else is validated by the Web API, so the two clients
        // cannot end up enforcing different rules.
        val missing = when {
            nic.isEmpty() -> "Enter your NIC number"
            fullName.isEmpty() -> "Enter your full name"
            email.isEmpty() -> "Enter your email address"
            password.length < 6 -> "Choose a password of at least 6 characters"
            else -> null
        }

        if (missing != null) {
            showError(missing)
            return
        }

        showError(null)
        setBusy(true)

        lifecycleScope.launch {
            try {
                ApiClient.call {
                    AppServices.api.registerProsumer(
                        RegisterProsumerRequest(
                            nic = nic,
                            fullName = fullName,
                            email = email,
                            phone = phone.ifEmpty { null },
                            address = address.ifEmpty { null },
                            password = password
                        )
                    )
                }

                setBusy(false)
                showPendingApprovalDialog()
            } catch (error: ApiException) {
                // A NIC or email already registered is reported here with the
                // service's own wording.
                setBusy(false)
                showError(error.message)
            }
        }
    }

    /**
     * Explains that the account exists but cannot be used until the back-office
     * team activates it, which is the rule the service applies.
     */
    private fun showPendingApprovalDialog() {
        MaterialAlertDialogBuilder(this)
            .setTitle("Registration received")
            .setMessage(
                "Your prosumer account has been created and is waiting for the back-office " +
                    "team to activate it. You will be able to sign in once it is approved."
            )
            .setCancelable(false)
            .setPositiveButton("Back to sign in") { _, _ -> finish() }
            .show()
    }

    /** Disables the form while the request is running. */
    private fun setBusy(isBusy: Boolean) {
        binding.progress.visibility = if (isBusy) View.VISIBLE else View.GONE
        binding.buttonRegister.isEnabled = !isBusy
        binding.buttonBackToLogin.isEnabled = !isBusy
    }

    /** Displays a message, or hides the banner when null. */
    private fun showError(message: String?) {
        if (message == null) {
            binding.textError.visibility = View.GONE
        } else {
            binding.textError.text = message
            binding.textError.visibility = View.VISIBLE
        }
    }
}
