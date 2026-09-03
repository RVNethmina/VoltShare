// -----------------------------------------------------------------------------
// File        : SystemBars.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Keeps screen content clear of the status and navigation bars.
//
//               Recent Android versions draw applications edge to edge, so a
//               header laid out at the top of the screen ends up underneath the
//               clock unless the inset is applied as padding. This helper does
//               that in one place rather than every layout guessing a height.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.util

import android.view.View
import android.view.Window
import androidx.core.view.ViewCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat

object SystemBars {

    /**
     * Chooses dark or light icons for the status bar.
     *
     * The clock and indicators are drawn by the system over whatever the app
     * puts at the top of the screen. Screens with a dark header need the light
     * icons; a screen that is light at the top needs dark ones, or the clock is
     * white on a pale background and cannot be read.
     */
    fun useDarkStatusBarIcons(window: Window, useDark: Boolean) {
        WindowCompat.getInsetsController(window, window.decorView)
            .isAppearanceLightStatusBars = useDark
    }

    /**
     * Adds the height of the status bar to the top padding of a view, and the
     * navigation bar to the bottom padding of the screen.
     *
     * The padding the layout already declares is captured first, so the inset
     * is added to it rather than replacing it, and repeated calls cannot make
     * the padding grow each time the listener runs.
     */
    fun applyInsets(header: View, bottomView: View? = null) {
        val headerPaddingTop = header.paddingTop
        val bottomPadding = bottomView?.paddingBottom ?: 0

        ViewCompat.setOnApplyWindowInsetsListener(header) { view, windowInsets ->
            val bars = windowInsets.getInsets(WindowInsetsCompat.Type.systemBars())

            view.setPadding(
                view.paddingLeft,
                headerPaddingTop + bars.top,
                view.paddingRight,
                view.paddingBottom
            )

            bottomView?.setPadding(
                bottomView.paddingLeft,
                bottomView.paddingTop,
                bottomView.paddingRight,
                bottomPadding + bars.bottom
            )

            // The insets are returned unchanged so any other view that also
            // needs them still receives them.
            windowInsets
        }
    }
}
