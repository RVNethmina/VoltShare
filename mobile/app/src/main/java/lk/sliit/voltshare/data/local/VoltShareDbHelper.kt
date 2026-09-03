// -----------------------------------------------------------------------------
// File        : VoltShareDbHelper.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Local SQLite database. Holds the signed in session so the user
//               stays logged in between launches, and caches stations and
//               bookings so those screens still show something useful when the
//               handset is briefly offline.
//
//               The cache is never a source of truth: every write goes to the
//               central Web API, and the tables here are refreshed from the
//               service response.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.data.local

import android.content.Context
import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper

class VoltShareDbHelper(context: Context) :
    SQLiteOpenHelper(context.applicationContext, DATABASE_NAME, null, DATABASE_VERSION) {

    /**
     * Creates the schema the first time the application runs on a device.
     */
    override fun onCreate(db: SQLiteDatabase) {
        db.execSQL(CREATE_SESSION_TABLE)
        db.execSQL(CREATE_STATION_TABLE)
        db.execSQL(CREATE_BOOKING_TABLE)
    }

    /**
     * Rebuilds the schema when the version number is raised.
     *
     * Dropping and recreating is safe here because every table is either the
     * current session or a cache of data owned by the server, so nothing is
     * lost that cannot be fetched again.
     */
    override fun onUpgrade(db: SQLiteDatabase, oldVersion: Int, newVersion: Int) {
        db.execSQL("DROP TABLE IF EXISTS $TABLE_BOOKING_CACHE")
        db.execSQL("DROP TABLE IF EXISTS $TABLE_STATION_CACHE")
        db.execSQL("DROP TABLE IF EXISTS $TABLE_SESSION")
        onCreate(db)
    }

    companion object {
        const val DATABASE_NAME = "voltshare.db"
        const val DATABASE_VERSION = 1

        // ------------------------------------------------------------------
        // Session: at most one row, holding the currently signed in account
        // and its access token.
        // ------------------------------------------------------------------
        const val TABLE_SESSION = "session"
        const val COL_SESSION_ID = "id"
        const val COL_SESSION_USER_ID = "user_id"
        const val COL_SESSION_FULL_NAME = "full_name"
        const val COL_SESSION_EMAIL = "email"
        const val COL_SESSION_PHONE = "phone"
        const val COL_SESSION_ADDRESS = "address"
        const val COL_SESSION_ROLE = "role"
        const val COL_SESSION_TOKEN = "access_token"
        const val COL_SESSION_EXPIRES_AT = "expires_at_utc"

        // ------------------------------------------------------------------
        // Station cache: lets the station list and the map open immediately,
        // and still show the last known nodes without a connection.
        // ------------------------------------------------------------------
        const val TABLE_STATION_CACHE = "station_cache"
        const val COL_STATION_ID = "id"
        const val COL_STATION_CODE = "code"
        const val COL_STATION_NAME = "name"
        const val COL_STATION_ADDRESS = "address_line"
        const val COL_STATION_CITY = "city"
        const val COL_STATION_LAT = "latitude"
        const val COL_STATION_LNG = "longitude"
        const val COL_STATION_CAPACITY = "capacity_kwh"
        const val COL_STATION_TOTAL_SLOTS = "total_battery_slots"
        const val COL_STATION_FREE_SLOTS = "available_battery_slots"
        const val COL_STATION_OPEN_TIME = "open_time"
        const val COL_STATION_CLOSE_TIME = "close_time"

        // ------------------------------------------------------------------
        // Booking cache: backs the booking history and the search filter.
        // ------------------------------------------------------------------
        const val TABLE_BOOKING_CACHE = "booking_cache"
        const val COL_BOOKING_ID = "id"
        const val COL_BOOKING_NO = "reservation_no"
        const val COL_BOOKING_NIC = "prosumer_nic"
        const val COL_BOOKING_STATION_ID = "station_id"
        const val COL_BOOKING_STATION_NAME = "station_name"
        const val COL_BOOKING_SLOT_ID = "slot_id"
        const val COL_BOOKING_START = "reservation_start_utc"
        const val COL_BOOKING_END = "reservation_end_utc"
        const val COL_BOOKING_ENERGY = "energy_kwh"
        const val COL_BOOKING_TYPE = "type"
        const val COL_BOOKING_STATUS = "status"
        const val COL_BOOKING_CAN_MODIFY = "can_be_modified"
        const val COL_BOOKING_CAN_CANCEL = "can_be_cancelled"
        const val COL_BOOKING_HAS_QR = "has_qr_code"
        const val COL_BOOKING_CREATED = "created_at_utc"

        private const val CREATE_SESSION_TABLE = """
            CREATE TABLE $TABLE_SESSION (
                $COL_SESSION_ID INTEGER PRIMARY KEY AUTOINCREMENT,
                $COL_SESSION_USER_ID TEXT NOT NULL,
                $COL_SESSION_FULL_NAME TEXT NOT NULL,
                $COL_SESSION_EMAIL TEXT NOT NULL,
                $COL_SESSION_PHONE TEXT,
                $COL_SESSION_ADDRESS TEXT,
                $COL_SESSION_ROLE TEXT NOT NULL,
                $COL_SESSION_TOKEN TEXT NOT NULL,
                $COL_SESSION_EXPIRES_AT TEXT NOT NULL
            )
        """

        private const val CREATE_STATION_TABLE = """
            CREATE TABLE $TABLE_STATION_CACHE (
                $COL_STATION_ID TEXT PRIMARY KEY,
                $COL_STATION_CODE TEXT NOT NULL,
                $COL_STATION_NAME TEXT NOT NULL,
                $COL_STATION_ADDRESS TEXT,
                $COL_STATION_CITY TEXT,
                $COL_STATION_LAT REAL NOT NULL,
                $COL_STATION_LNG REAL NOT NULL,
                $COL_STATION_CAPACITY REAL NOT NULL,
                $COL_STATION_TOTAL_SLOTS INTEGER NOT NULL,
                $COL_STATION_FREE_SLOTS INTEGER NOT NULL,
                $COL_STATION_OPEN_TIME TEXT,
                $COL_STATION_CLOSE_TIME TEXT
            )
        """

        private const val CREATE_BOOKING_TABLE = """
            CREATE TABLE $TABLE_BOOKING_CACHE (
                $COL_BOOKING_ID TEXT PRIMARY KEY,
                $COL_BOOKING_NO TEXT NOT NULL,
                $COL_BOOKING_NIC TEXT NOT NULL,
                $COL_BOOKING_STATION_ID TEXT,
                $COL_BOOKING_STATION_NAME TEXT,
                $COL_BOOKING_SLOT_ID TEXT,
                $COL_BOOKING_START TEXT NOT NULL,
                $COL_BOOKING_END TEXT,
                $COL_BOOKING_ENERGY REAL NOT NULL,
                $COL_BOOKING_TYPE TEXT NOT NULL,
                $COL_BOOKING_STATUS TEXT NOT NULL,
                $COL_BOOKING_CAN_MODIFY INTEGER NOT NULL DEFAULT 0,
                $COL_BOOKING_CAN_CANCEL INTEGER NOT NULL DEFAULT 0,
                $COL_BOOKING_HAS_QR INTEGER NOT NULL DEFAULT 0,
                $COL_BOOKING_CREATED TEXT
            )
        """
    }
}
