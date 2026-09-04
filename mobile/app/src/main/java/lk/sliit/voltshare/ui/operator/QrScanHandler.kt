// -----------------------------------------------------------------------------
// File        : QrScanHandler.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Shared behaviour for the grid operator's QR workflow: launch
//               the camera scanner, and send whatever was scanned to the Web
//               API for verification.
//
//               A scanned code is treated purely as text. Its validity, and
//               whether the booking it names may still be processed, are
//               decided entirely by the service.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui.operator

import android.view.LayoutInflater
import android.widget.EditText
import android.widget.FrameLayout
import com.google.android.material.dialog.MaterialAlertDialogBuilder
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import com.journeyapps.barcodescanner.ScanOptions
import kotlinx.coroutines.launch
import lk.sliit.voltshare.AppServices
import lk.sliit.voltshare.R
import lk.sliit.voltshare.data.remote.ApiClient
import lk.sliit.voltshare.data.remote.ApiException
import lk.sliit.voltshare.data.remote.VerifyQrRequest

object QrScanHandler {

    /**
     * Settings for the camera scanner: QR codes only, with the torch offered
     * because station equipment is often in a dim plant room.
     */
    fun scanOptions(prompt: String): ScanOptions {
        return ScanOptions().apply {
            setDesiredBarcodeFormats(ScanOptions.QR_CODE)
            setPrompt(prompt)
            setBeepEnabled(true)
            setOrientationLocked(false)
            setTorchEnabled(false)
        }
    }

    /**
     * Sends a scanned token to the service and, when it is accepted, opens the
     * result screen. Any refusal is reported with the service's own wording.
     */
    fun verifyAndShow(
        activity: AppCompatActivity,
        token: String,
        onBusy: (Boolean) -> Unit,
        onError: (String) -> Unit
    ) {
        if (token.isBlank()) {
            onError("Nothing was scanned. Try again.")
            return
        }

        onBusy(true)

        activity.lifecycleScope.launch {
            try {
                val reservation = ApiClient.call {
                    AppServices.api.verifyQr(VerifyQrRequest(token.trim()))
                }

                ScanResultActivity.start(activity, reservation)
            } catch (error: ApiException) {
                // Covers a forged or altered code, one superseded by a change
                // to the booking, and one that has already been used.
                onError(error.message)
            } finally {
                onBusy(false)
            }
        }
    }

    /**
     * Offers a text box for the token.
     *
     * This exists because an emulator has no usable camera, so the operator
     * flow can still be demonstrated and tested without a physical handset.
     */
    fun promptForToken(activity: AppCompatActivity, onEntered: (String) -> Unit) {
        val container = FrameLayout(activity)
        val input = EditText(activity).apply {
            hint = activity.getString(R.string.enter_token_manually)
            setSingleLine(false)
            maxLines = 4
        }

        // A little padding so the field is not flush against the dialog edge.
        val padding = (16 * activity.resources.displayMetrics.density).toInt()
        container.setPadding(padding, padding / 2, padding, 0)
        container.addView(input)

        MaterialAlertDialogBuilder(activity)
            .setTitle(R.string.enter_token_manually)
            .setView(container)
            .setNegativeButton(R.string.cancel, null)
            .setPositiveButton(R.string.verify) { _, _ ->
                onEntered(input.text?.toString().orEmpty())
            }
            .show()
    }
}
