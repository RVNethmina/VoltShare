// -----------------------------------------------------------------------------
// File        : LocationHelper.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Reads the handset's last known position so the nearby stations
//               request can be centred on the prosumer.
//
//               The position is only ever sent to the service, which performs
//               the geographic search. Nothing here calculates a distance.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.util

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import androidx.core.content.ContextCompat
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import com.google.android.gms.tasks.CancellationTokenSource
import kotlin.coroutines.resume
import kotlinx.coroutines.suspendCancellableCoroutine

/** A position on the earth, as latitude and longitude. */
data class LatLngPoint(val latitude: Double, val longitude: Double)

object LocationHelper {

    /** Colombo, used when the handset has no position of its own to offer. */
    val FALLBACK = LatLngPoint(6.9271, 79.8612)

    /** True when the user has granted either location permission. */
    fun hasPermission(context: Context): Boolean {
        val fine = ContextCompat.checkSelfPermission(
            context, Manifest.permission.ACCESS_FINE_LOCATION
        ) == PackageManager.PERMISSION_GRANTED

        val coarse = ContextCompat.checkSelfPermission(
            context, Manifest.permission.ACCESS_COARSE_LOCATION
        ) == PackageManager.PERMISSION_GRANTED

        return fine || coarse
    }

    /**
     * Returns the current position, or null when it cannot be determined.
     *
     * Suspends until the location service answers, so the caller can await it
     * like any other asynchronous step rather than nesting callbacks.
     */
    @SuppressLint("MissingPermission")
    suspend fun currentPosition(context: Context): LatLngPoint? {
        // The permission is checked here rather than relying on the caller, so
        // this can never throw a SecurityException.
        if (!hasPermission(context)) return null

        val client = LocationServices.getFusedLocationProviderClient(context)
        val cancellation = CancellationTokenSource()

        return suspendCancellableCoroutine { continuation ->
            // If the coroutine is cancelled, for example because the screen
            // closed, the location request is cancelled with it.
            continuation.invokeOnCancellation { cancellation.cancel() }

            client.getCurrentLocation(Priority.PRIORITY_BALANCED_POWER_ACCURACY, cancellation.token)
                .addOnSuccessListener { location ->
                    val point = location?.let { LatLngPoint(it.latitude, it.longitude) }

                    if (continuation.isActive) continuation.resume(point)
                }
                .addOnFailureListener {
                    // A failure is reported as "no position" so the caller can
                    // fall back rather than the whole screen failing.
                    if (continuation.isActive) continuation.resume(null)
                }
        }
    }
}
