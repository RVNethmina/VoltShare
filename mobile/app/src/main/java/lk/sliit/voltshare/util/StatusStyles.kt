// -----------------------------------------------------------------------------
// File        : StatusStyles.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Applies the colour of a reservation status to a label. Kept in
//               one place so a status looks the same on every screen, and the
//               same as it does in the web application.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.util

import android.widget.TextView
import androidx.core.content.ContextCompat
import lk.sliit.voltshare.R
import lk.sliit.voltshare.data.remote.ApiConstants

object StatusStyles {

    /**
     * Colours a label according to the reservation status it displays.
     */
    fun apply(label: TextView, status: String) {
        val context = label.context

        // Paired background and text colours per status. An unrecognised value
        // falls back to the neutral pair rather than being left unstyled.
        val (backgroundRes, textRes) = when (status) {
            ApiConstants.STATUS_PENDING ->
                R.color.status_pending_bg to R.color.status_pending_fg

            ApiConstants.STATUS_APPROVED ->
                R.color.status_approved_bg to R.color.status_approved_fg

            ApiConstants.STATUS_COMPLETED ->
                R.color.status_completed_bg to R.color.status_completed_fg

            ApiConstants.STATUS_REJECTED ->
                R.color.status_rejected_bg to R.color.status_rejected_fg

            else ->
                R.color.status_cancelled_bg to R.color.status_cancelled_fg
        }

        label.setBackgroundColor(ContextCompat.getColor(context, backgroundRes))
        label.setTextColor(ContextCompat.getColor(context, textRes))
    }
}
