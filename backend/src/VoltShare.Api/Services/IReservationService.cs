// -----------------------------------------------------------------------------
// File        : IReservationService.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Services
// Description : Contract for the energy reservation business logic: the seven
//               day booking horizon, the twelve hour change notice, slot
//               capacity, the approval workflow and QR verification.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Dtos;
using VoltShare.Api.Models;
using VoltShare.Api.Repositories;

namespace VoltShare.Api.Services;

/// <summary>
/// Who is making the request. Passed in by the controller so that the service
/// can apply ownership rules without depending on anything from HTTP.
/// </summary>
/// <param name="UserId">Identifier of the caller; the NIC for a prosumer.</param>
/// <param name="Role">One of the values in UserRoles.</param>
public record CallerContext(string UserId, string Role)
{
    /// <summary>
    /// True when the caller is a back-office officer or a grid operator.
    /// </summary>
    public bool IsStaff =>
        Role == UserRoles.Backoffice || Role == UserRoles.GridOperator;
}

/// <summary>
/// Management of power trading reservations.
/// </summary>
public interface IReservationService
{
    /// <summary>
    /// Creates a booking, enforcing the seven day horizon and slot capacity.
    /// </summary>
    Task<ReservationSummaryResponse> CreateAsync(
        CreateReservationRequest request,
        CallerContext caller,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes an existing booking, enforcing the twelve hour notice rule.
    /// </summary>
    Task<ReservationSummaryResponse> UpdateAsync(
        string id,
        UpdateReservationRequest request,
        CallerContext caller,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a booking, enforcing the twelve hour notice rule.
    /// </summary>
    Task<ReservationSummaryResponse> CancelAsync(
        string id, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a pending booking and issues its transaction QR token.
    /// </summary>
    Task<ReservationResponse> ApproveAsync(
        string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a pending booking and returns its place to the slot.
    /// </summary>
    Task<ReservationResponse> RejectAsync(
        string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one booking, subject to the ownership rules.
    /// </summary>
    Task<ReservationResponse> GetByIdAsync(
        string id, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches bookings. A prosumer caller is silently restricted to their own
    /// records, whatever filters they supply.
    /// </summary>
    Task<IReadOnlyList<ReservationResponse>> SearchAsync(
        ReservationQuery query, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the QR payload for an approved booking, for the owner only.
    /// </summary>
    Task<QrCodeResponse> GetQrCodeAsync(
        string id, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a scanned QR token against the server and returns the booking
    /// it identifies, without changing anything.
    /// </summary>
    Task<ReservationResponse> VerifyQrAsync(
        string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finalises the energy transfer after a successful scan.
    /// </summary>
    Task<ReservationSummaryResponse> CompleteAsync(
        string id, CallerContext caller, CancellationToken cancellationToken = default);
}
