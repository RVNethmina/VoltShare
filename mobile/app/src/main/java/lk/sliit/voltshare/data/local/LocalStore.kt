// -----------------------------------------------------------------------------
// File        : LocalStore.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Read and write access to the local SQLite database: the signed
//               in session, the cached stations and the cached bookings.
//
//               Everything here is either the session or a copy of data owned
//               by the Web API. No booking is ever created or changed locally.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.data.local

import android.content.ContentValues
import android.content.Context
import android.database.Cursor
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_CAN_CANCEL
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_CAN_MODIFY
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_CREATED
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_END
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_ENERGY
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_HAS_QR
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_ID
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_NIC
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_NO
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_SLOT_ID
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_START
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_STATION_ID
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_STATION_NAME
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_STATUS
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_BOOKING_TYPE
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_SESSION_ADDRESS
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_SESSION_EMAIL
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_SESSION_EXPIRES_AT
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_SESSION_FULL_NAME
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_SESSION_PHONE
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_SESSION_ROLE
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_SESSION_TOKEN
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_SESSION_USER_ID
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_STATION_ADDRESS
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_STATION_CAPACITY
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_STATION_CITY
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_STATION_CLOSE_TIME
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_STATION_CODE
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_STATION_FREE_SLOTS
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_STATION_ID
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_STATION_LAT
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_STATION_LNG
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_STATION_NAME
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_STATION_OPEN_TIME
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.COL_STATION_TOTAL_SLOTS
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.TABLE_BOOKING_CACHE
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.TABLE_SESSION
import lk.sliit.voltshare.data.local.VoltShareDbHelper.Companion.TABLE_STATION_CACHE
import lk.sliit.voltshare.data.remote.OperatingHoursDto
import lk.sliit.voltshare.data.remote.ReservationDto
import lk.sliit.voltshare.data.remote.StationDto
import lk.sliit.voltshare.data.remote.UserDto

/** The signed in account, as held on the device. */
data class StoredSession(
    val userId: String,
    val fullName: String,
    val email: String,
    val phone: String?,
    val address: String?,
    val role: String,
    val accessToken: String,
    val expiresAtUtc: String
)

/**
 * Single entry point to the local database.
 */
class LocalStore(context: Context) {

    private val helper = VoltShareDbHelper(context)

    // ---------------------------------------------------------------------
    // Session
    // ---------------------------------------------------------------------

    /**
     * Replaces any stored session with this one. The table holds at most one
     * row, so the previous session is removed first.
     */
    fun saveSession(user: UserDto, accessToken: String, expiresAtUtc: String) {
        val values = ContentValues().apply {
            put(COL_SESSION_USER_ID, user.id)
            put(COL_SESSION_FULL_NAME, user.fullName)
            put(COL_SESSION_EMAIL, user.email)
            put(COL_SESSION_PHONE, user.phone)
            put(COL_SESSION_ADDRESS, user.address)
            put(COL_SESSION_ROLE, user.role)
            put(COL_SESSION_TOKEN, accessToken)
            put(COL_SESSION_EXPIRES_AT, expiresAtUtc)
        }

        helper.writableDatabase.use { db ->
            db.beginTransaction()
            try {
                db.delete(TABLE_SESSION, null, null)
                db.insert(TABLE_SESSION, null, values)
                db.setTransactionSuccessful()
            } finally {
                db.endTransaction()
            }
        }
    }

    /** Updates the stored profile fields after the user edits them. */
    fun updateStoredProfile(user: UserDto) {
        val values = ContentValues().apply {
            put(COL_SESSION_FULL_NAME, user.fullName)
            put(COL_SESSION_PHONE, user.phone)
            put(COL_SESSION_ADDRESS, user.address)
        }

        helper.writableDatabase.use { db ->
            db.update(TABLE_SESSION, values, null, null)
        }
    }

    /** Returns the stored session, or null when nobody is signed in. */
    fun getSession(): StoredSession? {
        helper.readableDatabase.use { db ->
            db.query(TABLE_SESSION, null, null, null, null, null, null, "1").use { cursor ->
                if (!cursor.moveToFirst()) return null

                return StoredSession(
                    userId = cursor.getStringOrEmpty(COL_SESSION_USER_ID),
                    fullName = cursor.getStringOrEmpty(COL_SESSION_FULL_NAME),
                    email = cursor.getStringOrEmpty(COL_SESSION_EMAIL),
                    phone = cursor.getStringOrNull(COL_SESSION_PHONE),
                    address = cursor.getStringOrNull(COL_SESSION_ADDRESS),
                    role = cursor.getStringOrEmpty(COL_SESSION_ROLE),
                    accessToken = cursor.getStringOrEmpty(COL_SESSION_TOKEN),
                    expiresAtUtc = cursor.getStringOrEmpty(COL_SESSION_EXPIRES_AT)
                )
            }
        }
    }

    /**
     * Signs out by discarding the session and everything cached for it, so no
     * personal booking data is left behind for the next user of the device.
     */
    fun clearSession() {
        helper.writableDatabase.use { db ->
            db.delete(TABLE_SESSION, null, null)
            db.delete(TABLE_BOOKING_CACHE, null, null)
        }
    }

    // ---------------------------------------------------------------------
    // Station cache
    // ---------------------------------------------------------------------

    /**
     * Replaces the cached stations with the list just fetched from the API.
     * Done in one transaction so a failure part way through cannot leave the
     * cache half written.
     */
    fun replaceStations(stations: List<StationDto>) {
        helper.writableDatabase.use { db ->
            db.beginTransaction()
            try {
                db.delete(TABLE_STATION_CACHE, null, null)

                for (station in stations) {
                    val values = ContentValues().apply {
                        put(COL_STATION_ID, station.id)
                        put(COL_STATION_CODE, station.code)
                        put(COL_STATION_NAME, station.name)
                        put(COL_STATION_ADDRESS, station.addressLine)
                        put(COL_STATION_CITY, station.city)
                        put(COL_STATION_LAT, station.latitude)
                        put(COL_STATION_LNG, station.longitude)
                        put(COL_STATION_CAPACITY, station.capacityKwh)
                        put(COL_STATION_TOTAL_SLOTS, station.totalBatterySlots)
                        put(COL_STATION_FREE_SLOTS, station.availableBatterySlots)
                        put(COL_STATION_OPEN_TIME, station.operatingHours.openTime)
                        put(COL_STATION_CLOSE_TIME, station.operatingHours.closeTime)
                    }
                    db.insert(TABLE_STATION_CACHE, null, values)
                }

                db.setTransactionSuccessful()
            } finally {
                db.endTransaction()
            }
        }
    }

    /** Returns the cached stations, used when the API cannot be reached. */
    fun getCachedStations(): List<StationDto> {
        val stations = mutableListOf<StationDto>()

        helper.readableDatabase.use { db ->
            db.query(
                TABLE_STATION_CACHE, null, null, null, null, null, "$COL_STATION_NAME ASC"
            ).use { cursor ->
                while (cursor.moveToNext()) {
                    stations.add(
                        StationDto(
                            id = cursor.getStringOrEmpty(COL_STATION_ID),
                            code = cursor.getStringOrEmpty(COL_STATION_CODE),
                            name = cursor.getStringOrEmpty(COL_STATION_NAME),
                            addressLine = cursor.getStringOrEmpty(COL_STATION_ADDRESS),
                            city = cursor.getStringOrEmpty(COL_STATION_CITY),
                            latitude = cursor.getDoubleOr(COL_STATION_LAT),
                            longitude = cursor.getDoubleOr(COL_STATION_LNG),
                            capacityKwh = cursor.getDoubleOr(COL_STATION_CAPACITY),
                            totalBatterySlots = cursor.getIntOr(COL_STATION_TOTAL_SLOTS),
                            availableBatterySlots = cursor.getIntOr(COL_STATION_FREE_SLOTS),
                            operatingHours = OperatingHoursDto(
                                openTime = cursor.getStringOrEmpty(COL_STATION_OPEN_TIME),
                                closeTime = cursor.getStringOrEmpty(COL_STATION_CLOSE_TIME)
                            ),
                            // Only active stations are ever cached, because the
                            // API only returns active ones to this application.
                            isActive = true
                        )
                    )
                }
            }
        }

        return stations
    }

    // ---------------------------------------------------------------------
    // Booking cache
    // ---------------------------------------------------------------------

    /** Replaces the cached bookings with the list just fetched. */
    fun replaceBookings(reservations: List<ReservationDto>) {
        helper.writableDatabase.use { db ->
            db.beginTransaction()
            try {
                db.delete(TABLE_BOOKING_CACHE, null, null)

                for (r in reservations) {
                    val values = ContentValues().apply {
                        put(COL_BOOKING_ID, r.id)
                        put(COL_BOOKING_NO, r.reservationNo)
                        put(COL_BOOKING_NIC, r.prosumerNic)
                        put(COL_BOOKING_STATION_ID, r.stationId)
                        put(COL_BOOKING_STATION_NAME, r.stationName)
                        put(COL_BOOKING_SLOT_ID, r.slotId)
                        put(COL_BOOKING_START, r.reservationStartUtc)
                        put(COL_BOOKING_END, r.reservationEndUtc)
                        put(COL_BOOKING_ENERGY, r.energyKwh)
                        put(COL_BOOKING_TYPE, r.type)
                        put(COL_BOOKING_STATUS, r.status)
                        put(COL_BOOKING_CAN_MODIFY, if (r.canBeModified) 1 else 0)
                        put(COL_BOOKING_CAN_CANCEL, if (r.canBeCancelled) 1 else 0)
                        put(COL_BOOKING_HAS_QR, if (r.hasQrCode) 1 else 0)
                        put(COL_BOOKING_CREATED, r.createdAtUtc)
                    }
                    db.insert(TABLE_BOOKING_CACHE, null, values)
                }

                db.setTransactionSuccessful()
            } finally {
                db.endTransaction()
            }
        }
    }

    /**
     * Reads the cached bookings, optionally narrowed by status and by a search
     * term matched against the reference and the station name.
     */
    fun getCachedBookings(status: String? = null, search: String? = null): List<ReservationDto> {
        val where = mutableListOf<String>()
        val args = mutableListOf<String>()

        if (!status.isNullOrBlank()) {
            where.add("$COL_BOOKING_STATUS = ?")
            args.add(status)
        }

        if (!search.isNullOrBlank()) {
            // Parameter binding is used rather than string concatenation, so a
            // search term can never alter the query itself.
            where.add("($COL_BOOKING_NO LIKE ? OR $COL_BOOKING_STATION_NAME LIKE ?)")
            args.add("%$search%")
            args.add("%$search%")
        }

        val selection = if (where.isEmpty()) null else where.joinToString(" AND ")
        val bookings = mutableListOf<ReservationDto>()

        helper.readableDatabase.use { db ->
            db.query(
                TABLE_BOOKING_CACHE, null, selection,
                if (args.isEmpty()) null else args.toTypedArray(),
                null, null, "$COL_BOOKING_START DESC"
            ).use { cursor ->
                while (cursor.moveToNext()) {
                    bookings.add(
                        ReservationDto(
                            id = cursor.getStringOrEmpty(COL_BOOKING_ID),
                            reservationNo = cursor.getStringOrEmpty(COL_BOOKING_NO),
                            prosumerNic = cursor.getStringOrEmpty(COL_BOOKING_NIC),
                            prosumerName = null,
                            stationId = cursor.getStringOrEmpty(COL_BOOKING_STATION_ID),
                            stationName = cursor.getStringOrNull(COL_BOOKING_STATION_NAME),
                            slotId = cursor.getStringOrEmpty(COL_BOOKING_SLOT_ID),
                            reservationStartUtc = cursor.getStringOrEmpty(COL_BOOKING_START),
                            reservationEndUtc = cursor.getStringOrEmpty(COL_BOOKING_END),
                            energyKwh = cursor.getDoubleOr(COL_BOOKING_ENERGY),
                            type = cursor.getStringOrEmpty(COL_BOOKING_TYPE),
                            status = cursor.getStringOrEmpty(COL_BOOKING_STATUS),
                            canBeModified = cursor.getIntOr(COL_BOOKING_CAN_MODIFY) == 1,
                            canBeCancelled = cursor.getIntOr(COL_BOOKING_CAN_CANCEL) == 1,
                            hasQrCode = cursor.getIntOr(COL_BOOKING_HAS_QR) == 1,
                            createdAtUtc = cursor.getStringOrEmpty(COL_BOOKING_CREATED),
                            cancelledAtUtc = null,
                            completedAtUtc = null
                        )
                    )
                }
            }
        }

        return bookings
    }

    // ---------------------------------------------------------------------
    // Cursor helpers
    //
    // Reading by column name keeps the code readable and, more importantly,
    // means a change to the column order cannot silently read the wrong field.
    // ---------------------------------------------------------------------

    private fun Cursor.getStringOrEmpty(column: String): String {
        val index = getColumnIndex(column)
        return if (index < 0 || isNull(index)) "" else getString(index)
    }

    private fun Cursor.getStringOrNull(column: String): String? {
        val index = getColumnIndex(column)
        return if (index < 0 || isNull(index)) null else getString(index)
    }

    private fun Cursor.getDoubleOr(column: String, fallback: Double = 0.0): Double {
        val index = getColumnIndex(column)
        return if (index < 0 || isNull(index)) fallback else getDouble(index)
    }

    private fun Cursor.getIntOr(column: String, fallback: Int = 0): Int {
        val index = getColumnIndex(column)
        return if (index < 0 || isNull(index)) fallback else getInt(index)
    }
}
