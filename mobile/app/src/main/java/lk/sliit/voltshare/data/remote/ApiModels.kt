// -----------------------------------------------------------------------------
// File        : ApiModels.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Kotlin mirrors of the request and response contracts of the
//               VoltShare Web API. Gson maps these by property name, so the
//               names must match the JSON the service returns exactly.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.data.remote

/** Credentials posted at sign in. */
data class LoginRequest(
    val email: String,
    val password: String
)

/** An account as returned by the service. */
data class UserDto(
    val id: String,
    val fullName: String,
    val email: String,
    val phone: String?,
    val address: String?,
    val role: String,
    val isActive: Boolean,
    val deactivationRequested: Boolean,
    val createdAtUtc: String
)

/** Successful sign in: the token plus the caller's profile. */
data class LoginResponse(
    val accessToken: String,
    val expiresAtUtc: String,
    val user: UserDto
)

/** Self service registration submitted from this application. */
data class RegisterProsumerRequest(
    val nic: String,
    val fullName: String,
    val email: String,
    val phone: String?,
    val address: String?,
    val password: String
)

/** Editable profile fields. */
data class UpdateProfileRequest(
    val fullName: String,
    val phone: String?,
    val address: String?
)

/** Daily operating window of a station. */
data class OperatingHoursDto(
    val openTime: String,
    val closeTime: String
)

/** A solar microgrid node. */
data class StationDto(
    val id: String,
    val code: String,
    val name: String,
    val addressLine: String,
    val city: String,
    val latitude: Double,
    val longitude: Double,
    val capacityKwh: Double,
    val totalBatterySlots: Int,
    val availableBatterySlots: Int,
    val operatingHours: OperatingHoursDto,
    val isActive: Boolean
)

/**
 * A station with the distance from the searched point.
 * The distance is calculated by the service, so this application never
 * performs any geographic arithmetic of its own.
 */
data class NearbyStationDto(
    val station: StationDto,
    val distanceMeters: Double
)

/** A bookable energy transfer window. */
data class SlotDto(
    val id: String,
    val stationId: String,
    val startTimeUtc: String,
    val endTimeUtc: String,
    val capacity: Int,
    val bookedCount: Int,
    val remainingCapacity: Int,
    val energyKwhPerSlot: Double,
    val isActive: Boolean
)

/**
 * A reservation.
 *
 * canBeModified and canBeCancelled are decided by the service from the twelve
 * hour notice rule. The screens only read them to enable or disable buttons;
 * the rule itself is never evaluated in this application.
 */
data class ReservationDto(
    val id: String,
    val reservationNo: String,
    val prosumerNic: String,
    val prosumerName: String?,
    val stationId: String,
    val stationName: String?,
    val slotId: String,
    val reservationStartUtc: String,
    val reservationEndUtc: String,
    val energyKwh: Double,
    val type: String,
    val status: String,
    val canBeModified: Boolean,
    val canBeCancelled: Boolean,
    val hasQrCode: Boolean,
    val createdAtUtc: String,
    val cancelledAtUtc: String?,
    val completedAtUtc: String?
)

/**
 * Confirmation returned after a booking action. The message is written by the
 * service, so the summary screen shows the server's own wording.
 */
data class ReservationSummaryDto(
    val action: String,
    val message: String,
    val reservation: ReservationDto
)

/** A new booking request. */
data class CreateReservationRequest(
    val slotId: String,
    val type: String,
    val prosumerNic: String? = null
)

/** A change to an existing booking. */
data class UpdateReservationRequest(
    val slotId: String,
    val type: String
)

/** The QR payload handed to this application once a booking is approved. */
data class QrCodeDto(
    val reservationId: String,
    val reservationNo: String,
    val token: String,
    val issuedAtUtc: String,
    val reservationStartUtc: String
)

/** A token scanned from a prosumer QR code, sent for checking. */
data class VerifyQrRequest(
    val token: String
)

/** Counts shown on the prosumer home screen. */
data class ProsumerDashboardDto(
    val prosumerNic: String,
    val pendingCount: Long,
    val approvedFutureCount: Long,
    val completedCount: Long,
    val cancelledCount: Long,
    val nextReservation: ReservationDto?
)

/** Counts and today's workload shown to a grid operator. */
data class OperatorDashboardDto(
    val pendingCount: Long,
    val approvedFutureCount: Long,
    val completedTodayCount: Long,
    val activeStationCount: Long,
    val todaySchedule: List<ReservationDto>
)

/**
 * The error body the service returns when a request is refused. The errorCode
 * is a stable identifier such as RESERVATION_OUTSIDE_7_DAYS; detail carries the
 * readable explanation that is shown to the user.
 */
data class ProblemDetailsDto(
    val title: String?,
    val status: Int?,
    val detail: String?,
    val errorCode: String?
)

/** The fixed vocabulary shared with the service. */
object ApiConstants {
    const val ROLE_BACKOFFICE = "Backoffice"
    const val ROLE_GRID_OPERATOR = "GridOperator"
    const val ROLE_PROSUMER = "Prosumer"

    const val STATUS_PENDING = "Pending"
    const val STATUS_APPROVED = "Approved"
    const val STATUS_CANCELLED = "Cancelled"
    const val STATUS_REJECTED = "Rejected"
    const val STATUS_COMPLETED = "Completed"

    const val TYPE_INJECTION = "Injection"
    const val TYPE_WITHDRAWAL = "Withdrawal"
}
