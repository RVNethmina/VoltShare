// -----------------------------------------------------------------------------
// File        : VoltShareApi.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Retrofit description of every VoltShare Web API endpoint this
//               application uses. Declarations only: no rule is applied here,
//               because all business logic lives in the central service.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.data.remote

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.PATCH
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Path
import retrofit2.http.Query

interface VoltShareApi {

    // ---------------------------------------------------------------------
    // Authentication and account
    // ---------------------------------------------------------------------

    /** Signs in and returns the access token and profile. */
    @POST("auth/login")
    suspend fun login(@Body request: LoginRequest): Response<LoginResponse>

    /**
     * Registers a prosumer using the NIC as the primary key. The service
     * creates the account inactive, awaiting back-office activation.
     */
    @POST("auth/register-prosumer")
    suspend fun registerProsumer(@Body request: RegisterProsumerRequest): Response<UserDto>

    /** Returns the profile of the signed in account. */
    @GET("auth/me")
    suspend fun me(): Response<UserDto>

    /** Updates the caller's own profile. */
    @PUT("prosumers/{nic}")
    suspend fun updateProsumer(
        @Path("nic") nic: String,
        @Body request: UpdateProfileRequest
    ): Response<UserDto>

    /** Asks the back office to close the caller's account. */
    @PATCH("prosumers/{nic}/request-deactivation")
    suspend fun requestDeactivation(@Path("nic") nic: String): Response<UserDto>

    // ---------------------------------------------------------------------
    // Stations and booking windows
    // ---------------------------------------------------------------------

    /** Lists stations, optionally filtered. */
    @GET("stations")
    suspend fun listStations(
        @Query("isActive") isActive: Boolean? = true,
        @Query("search") search: String? = null
    ): Response<List<StationDto>>

    /**
     * Finds active stations near a point. The service performs the geographic
     * query and returns the distance to each, so the map screen only has to
     * place the markers it is given.
     */
    @GET("stations/nearby")
    suspend fun nearbyStations(
        @Query("lat") latitude: Double,
        @Query("lng") longitude: Double,
        @Query("radiusKm") radiusKm: Double = 25.0,
        @Query("limit") limit: Int = 50
    ): Response<List<NearbyStationDto>>

    /** Lists the booking windows offered by a station. */
    @GET("stations/{id}/slots")
    suspend fun listSlots(
        @Path("id") stationId: String,
        @Query("isActive") isActive: Boolean? = true
    ): Response<List<SlotDto>>

    // ---------------------------------------------------------------------
    // Reservations
    // ---------------------------------------------------------------------

    /** Searches the caller's bookings; the service restricts the scope. */
    @GET("reservations")
    suspend fun listReservations(
        @Query("status") status: String? = null,
        @Query("q") search: String? = null
    ): Response<List<ReservationDto>>

    /** Returns one booking. */
    @GET("reservations/{id}")
    suspend fun getReservation(@Path("id") id: String): Response<ReservationDto>

    /** Requests a new booking. */
    @POST("reservations")
    suspend fun createReservation(
        @Body request: CreateReservationRequest
    ): Response<ReservationSummaryDto>

    /** Changes an existing booking. */
    @PUT("reservations/{id}")
    suspend fun updateReservation(
        @Path("id") id: String,
        @Body request: UpdateReservationRequest
    ): Response<ReservationSummaryDto>

    /** Cancels a booking. */
    @PATCH("reservations/{id}/cancel")
    suspend fun cancelReservation(@Path("id") id: String): Response<ReservationSummaryDto>

    /** Fetches the QR payload for an approved booking. */
    @GET("reservations/{id}/qr")
    suspend fun getQrCode(@Path("id") id: String): Response<QrCodeDto>

    // ---------------------------------------------------------------------
    // Grid operator
    // ---------------------------------------------------------------------

    /** Checks a scanned token against the service without changing anything. */
    @POST("reservations/verify-qr")
    suspend fun verifyQr(@Body request: VerifyQrRequest): Response<ReservationDto>

    /** Finalises the energy transfer after a successful scan. */
    @POST("reservations/{id}/complete")
    suspend fun completeReservation(@Path("id") id: String): Response<ReservationSummaryDto>

    // ---------------------------------------------------------------------
    // Dashboards
    // ---------------------------------------------------------------------

    /** Counts for the signed in prosumer, computed by the service. */
    @GET("dashboard/prosumer")
    suspend fun prosumerDashboard(): Response<ProsumerDashboardDto>

    /** Counts and today's workload for a grid operator. */
    @GET("dashboard/operator")
    suspend fun operatorDashboard(): Response<OperatorDashboardDto>
}
