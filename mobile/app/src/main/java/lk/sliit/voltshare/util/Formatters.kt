// -----------------------------------------------------------------------------
// File        : Formatters.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Conversion between the UTC timestamps the Web API works in and
//               the local times shown to the user, plus small display helpers.
//
//               The service stores and returns every time in UTC; this file is
//               the only place in the application where a conversion happens.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.util

import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone

object Formatters {

    // The service sends timestamps with or without fractional seconds
    // depending on the value, so both shapes are attempted in turn.
    private val UTC_PATTERNS = listOf(
        "yyyy-MM-dd'T'HH:mm:ss.SSSSSSS'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.SSS'Z'",
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.SSSXXX",
        "yyyy-MM-dd'T'HH:mm:ssXXX"
    )

    /**
     * Parses a UTC timestamp from the API into a Date, or null when the value
     * is missing or in a shape that is not recognised.
     */
    fun parseUtc(value: String?): Date? {
        if (value.isNullOrBlank()) return null

        for (pattern in UTC_PATTERNS) {
            try {
                val format = SimpleDateFormat(pattern, Locale.UK)

                // The patterns ending in 'Z' treat the marker as a literal, so
                // the zone has to be set explicitly or the device's own zone
                // would be assumed and every time would be wrong.
                format.timeZone = TimeZone.getTimeZone("UTC")

                return format.parse(value)
            } catch (ignored: Exception) {
                // Try the next pattern.
            }
        }

        return null
    }

    /** Formats a UTC timestamp as a local date and time, for example 06 Sep 2026, 19:30. */
    fun dateTime(utc: String?): String {
        val date = parseUtc(utc) ?: return "—"
        return SimpleDateFormat("dd MMM yyyy, HH:mm", Locale.UK).format(date)
    }

    /** Formats a UTC timestamp as a local date only. */
    fun date(utc: String?): String {
        val date = parseUtc(utc) ?: return "—"
        return SimpleDateFormat("dd MMM yyyy", Locale.UK).format(date)
    }

    /** Formats a UTC timestamp as a local time only. */
    fun time(utc: String?): String {
        val date = parseUtc(utc) ?: return "—"
        return SimpleDateFormat("HH:mm", Locale.UK).format(date)
    }

    /**
     * Describes a booking window as a date with a start and end time, which is
     * how the booking screens present it.
     */
    fun window(startUtc: String?, endUtc: String?): String {
        val start = parseUtc(startUtc) ?: return "—"
        val end = parseUtc(endUtc)

        val dayFormat = SimpleDateFormat("dd MMM yyyy", Locale.UK)
        val timeFormat = SimpleDateFormat("HH:mm", Locale.UK)

        return if (end == null) {
            "${dayFormat.format(start)}, ${timeFormat.format(start)}"
        } else {
            "${dayFormat.format(start)}, ${timeFormat.format(start)} – ${timeFormat.format(end)}"
        }
    }

    /**
     * Presents a distance in the unit that reads most naturally: metres up to
     * a kilometre, kilometres beyond that.
     */
    fun distance(metres: Double): String {
        return if (metres < 1000) {
            "${metres.toInt()} m away"
        } else {
            String.format(Locale.UK, "%.1f km away", metres / 1000.0)
        }
    }

    /** Formats an energy volume with its unit. */
    fun energy(kwh: Double): String = String.format(Locale.UK, "%.1f kWh", kwh)
}
