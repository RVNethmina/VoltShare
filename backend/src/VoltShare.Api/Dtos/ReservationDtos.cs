// -----------------------------------------------------------------------------
// File        : ReservationDtos.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Dtos
// Description : Request and response contracts for energy reservations, the QR
//               verification exchange and the role based dashboards.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace VoltShare.Api.Dtos;

/// <summary>
/// A reservation as returned to the clients.
///
/// The two "can" flags are computed by the server from the twelve hour rule so
/// that the mobile and web clients can enable or disable their buttons without
/// implementing the rule themselves. The server still enforces it on every
/// request; these flags are purely a display aid.
/// </summary>
public record ReservationResponse(
    string Id,
    string ReservationNo,
    string ProsumerNic,
    string? ProsumerName,
    string StationId,
    string? StationName,
    string SlotId,
    DateTime ReservationStartUtc,
    DateTime ReservationEndUtc,
    double EnergyKwh,
    string Type,
    string Status,
    bool CanBeModified,
    bool CanBeCancelled,
    bool HasQrCode,
    DateTime CreatedAtUtc,
    DateTime? CancelledAtUtc,
    DateTime? CompletedAtUtc);

/// <summary>
/// Confirmation returned after creating, updating or cancelling a booking.
/// This is what the Android application shows on its summary screen, so the
/// wording of the outcome is decided by the server, not by the client.
/// </summary>
public record ReservationSummaryResponse(
    string Action,
    string Message,
    ReservationResponse Reservation);

/// <summary>
/// A new booking request.
/// </summary>
public class CreateReservationRequest
{
    [Required(ErrorMessage = "Slot identifier is required.")]
    public string SlotId { get; set; } = string.Empty;

    // Only staff may set this; a prosumer always books for themselves and the
    // service overrides whatever is sent here with their own NIC.
    public string? ProsumerNic { get; set; }

    [Required(ErrorMessage = "Reservation type is required.")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// A change to an existing booking. The prosumer may move to another window,
/// or change the direction of the transfer.
/// </summary>
public class UpdateReservationRequest
{
    [Required(ErrorMessage = "Slot identifier is required.")]
    public string SlotId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Reservation type is required.")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// The QR payload handed to the mobile application after approval.
/// </summary>
public record QrCodeResponse(
    string ReservationId,
    string ReservationNo,
    string Token,
    DateTime IssuedAtUtc,
    DateTime ReservationStartUtc);

/// <summary>
/// A token scanned from a prosumer QR code, sent by the operator for checking.
/// </summary>
public class VerifyQrRequest
{
    [Required(ErrorMessage = "Token is required.")]
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Counts shown on the prosumer home screen of the mobile application.
/// </summary>
public record ProsumerDashboardResponse(
    string ProsumerNic,
    long PendingCount,
    long ApprovedFutureCount,
    long CompletedCount,
    long CancelledCount,
    ReservationResponse? NextReservation);

/// <summary>
/// Counts and today's workload shown to a grid operator.
/// </summary>
public record OperatorDashboardResponse(
    long PendingCount,
    long ApprovedFutureCount,
    long CompletedTodayCount,
    long ActiveStationCount,
    IReadOnlyList<ReservationResponse> TodaySchedule);
