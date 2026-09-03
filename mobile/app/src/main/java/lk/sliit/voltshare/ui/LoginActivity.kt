// -----------------------------------------------------------------------------
// File        : LoginActivity.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Sign in screen. The Web API verifies the credentials, decides
//               whether the account is active, and reports the role. This
//               screen stores the resulting session in SQLite and routes the
//               user to the home screen for that role.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui

import android.content.Intent
import android.os.Bundle
import android.view.View
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.launch
import lk.sliit.voltshare.AppServices
import lk.sliit.voltshare.data.remote.ApiClient
import lk.sliit.voltshare.data.remote.ApiConstants
import lk.sliit.voltshare.data.remote.ApiException
import lk.sliit.voltshare.data.remote.LoginRequest
import lk.sliit.voltshare.databinding.ActivityLoginBinding
import lk.sliit.voltshare.ui.operator.OperatorHomeActivity
import lk.sliit.voltshare.ui.prosumer.ProsumerHomeActivity

class LoginActivity : AppCompatActivity() {

    private lateinit var binding: ActivityLoginBinding

    /**
     * Builds the screen and wires the two buttons.
     */
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        binding = ActivityLoginBinding.inflate(layoutInflater)
        setContentView(binding.root)

        binding.buttonSignIn.setOnClickListener { attemptSignIn() }

        binding.buttonRegister.setOnClickListener {
            startActivity(Intent(this, RegisterActivity::class.java))
        }
    }

    /**
     * Validates that both fields are filled, then asks the service to sign the
     * user in. Everything else about the credentials is decided by the service.
     */
    private fun attemptSignIn() {
        val email = binding.inputEmail.text?.toString()?.trim().orEmpty()
        val password = binding.inputPassword.text?.toString().orEmpty()

        // Only emptiness is checked here. Whether the account exists, whether
        // the password matches and whether the account is active are all
        // decisions for the Web API.
        if (email.isEmpty()) {
            binding.layoutEmail.error = "Enter your email address"
            return
        }
        binding.layoutEmail.error = null

        if (password.isEmpty()) {
            binding.layoutPassword.error = "Enter your password"
            return
        }
        binding.layoutPassword.error = null

        showError(null)
        setBusy(true)

        // lifecycleScope cancels the call automatically if the screen closes
        // while the request is still running.
        lifecycleScope.launch {
            try {
                val result = ApiClient.call {
                    AppServices.api.login(LoginRequest(email, password))
                }

                // The session goes into SQLite so the user stays signed in
                // between launches without entering credentials again.
                AppServices.store.saveSession(
                    user = result.user,
                    accessToken = result.accessToken,
                    expiresAtUtc = result.expiresAtUtc
                )

                goToHomeFor(result.user.role)
            } catch (error: ApiException) {
                // An inactive account awaiting back-office approval is reported
                // here with the service's own explanation.
                setBusy(false)
                showError(error.message)
            }
        }
    }

    /**
     * Sends the signed in user to the home screen matching their role, and
     * clears the back stack so pressing back does not return to sign in.
     */
    private fun goToHomeFor(role: String) {
        val destination = when (role) {
            ApiConstants.ROLE_PROSUMER -> ProsumerHomeActivity::class.java
            ApiConstants.ROLE_GRID_OPERATOR -> OperatorHomeActivity::class.java

            // A back-office account belongs to the web application. Signing it
            // out again is clearer than opening a screen where every request
            // would be refused.
            else -> {
                AppServices.signOut()
                setBusy(false)
                showError(
                    "Back-office accounts are managed in the VoltShare web application. " +
                        "Please sign in there instead."
                )
                return
            }
        }

        startActivity(Intent(this, destination))
        finish()
    }

    /**
     * Shows or hides the progress indicator and disables the form while a
     * request is in flight, so it cannot be submitted twice.
     */
    private fun setBusy(isBusy: Boolean) {
        binding.progress.visibility = if (isBusy) View.VISIBLE else View.GONE
        binding.buttonSignIn.isEnabled = !isBusy
        binding.buttonRegister.isEnabled = !isBusy
    }

    /** Displays a message from the service, or hides the banner when null. */
    private fun showError(message: String?) {
        if (message == null) {
            binding.textError.visibility = View.GONE
        } else {
            binding.textError.text = message
            binding.textError.visibility = View.VISIBLE
        }
    }
}
