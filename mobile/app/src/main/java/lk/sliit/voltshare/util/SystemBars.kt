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

import android.content.Context
import android.content.res.Configuration
import android.view.View
import android.view.Window
import androidx.core.content.ContextCompat
import androidx.core.view.ViewCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout
import lk.sliit.voltshare.R

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
     * True when the device is currently using its dark theme.
     */
    fun isNightMode(context: Context): Boolean {
        val nightFlags = context.resources.configuration.uiMode and
            Configuration.UI_MODE_NIGHT_MASK

        return nightFlags == Configuration.UI_MODE_NIGHT_YES
    }

    /**
     * Chooses status bar icons that suit the theme currently in use.
     *
     * Screens that draw their own dark header always want the light icons and
     * ask for them directly. This is for the few screens that sit on the page
     * background instead, where the correct choice depends on whether the
     * light or the dark theme is running.
     */
    fun applyThemeStatusBarIcons(window: Window, context: Context) {
        useDarkStatusBarIcons(window, !isNightMode(context))
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

    /**
     * Colours a pull to refresh spinner from the palette.
     *
     * The default spinner is a fixed white disc, which sits on the page like a
     * hole once the dark theme is running, so both its ring and its plate are
     * taken from the theme instead.
     */
    fun styleRefreshSpinner(refreshLayout: SwipeRefreshLayout) {
        val context = refreshLayout.context

        refreshLayout.setColorSchemeColors(
            ContextCompat.getColor(context, R.color.brand_500)
        )

        refreshLayout.setProgressBackgroundColorSchemeColor(
            ContextCompat.getColor(context, R.color.surface)
        )
    }
}
