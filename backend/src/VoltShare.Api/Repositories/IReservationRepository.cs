// -----------------------------------------------------------------------------
// File        : IReservationRepository.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Repositories
// Description : Data access contract for the "energyReservations" collection,
//               covering booking storage, the filtered searches behind the
//               booking views, and the counts behind the dashboards.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Models;

namespace VoltShare.Api.Repositories;

/// <summary>
/// Criteria for a reservation search. Grouped into one object because the
/// booking list screens combine several filters at once.
/// </summary>
public class ReservationQuery
{
    // Restrict to one prosumer. Set automatically for prosumer callers.
    public string? ProsumerNic { get; set; }

    // Restrict to one station.
    public string? StationId { get; set; }

    // Restrict to one lifecycle status.
    public string? Status { get; set; }

    // Restrict to several statuses at once, used by the "current" views.
    public string[]? Statuses { get; set; }

    // Inclusive lower bound on the reservation start time.
    public DateTime? FromUtc { get; set; }

    // Exclusive upper bound on the reservation start time.
    public DateTime? ToUtc { get; set; }

    // Bounds on when the transfer was actually finalised, as opposed to when
    // the booking was due to start. "Completed today" means finished today,
    // which is a different question from "due today", so it needs its own
    // range rather than reusing the one above.
    public DateTime? CompletedFromUtc { get; set; }

    public DateTime? CompletedToUtc { get; set; }

    // Free text search across reservation number and prosumer NIC.
    public string? Search { get; set; }

    // Largest number of rows to return.
    public int Limit { get; set; } = 200;
}

/// <summary>
/// Reads and writes energy reservation documents.
/// </summary>
public interface IReservationRepository
{
    /// <summary>
    /// Finds one reservation by its identifier.
    /// </summary>
    Task<EnergyReservation?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds one reservation by the QR token issued for it.
    /// </summary>
    Task<EnergyReservation?> GetByQrTokenAsync(
        string qrToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists reservations matching the supplied criteria, newest start first.
    /// </summary>
    Task<IReadOnlyList<EnergyReservation>> SearchAsync(
        ReservationQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts reservations matching the supplied criteria.
    /// </summary>
    Task<long> CountAsync(
        ReservationQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the next upcoming open reservation for a prosumer, or null.
    /// </summary>
    Task<EnergyReservation?> GetNextUpcomingAsync(
        string prosumerNic, DateTime fromUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the prosumer already holds an open booking on that slot,
    /// which stops the same window being booked twice by one person.
    /// </summary>
    Task<bool> ExistsActiveForProsumerAndSlotAsync(
        string prosumerNic,
        string slotId,
        string? excludeReservationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new reservation document.
    /// </summary>
    Task InsertAsync(EnergyReservation reservation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites an existing reservation document.
    /// </summary>
    Task ReplaceAsync(EnergyReservation reservation, CancellationToken cancellationToken = default);

    // The four methods below each perform one guarded status transition as a
    // single atomic FindOneAndUpdate. The expected status is part of the
    // filter, so if another request has already moved the reservation on, no
    // document matches and null is returned instead of the change being
    // applied twice. This is what makes approval and QR completion safe when
    // two operators act at the same moment.

    /// <summary>
    /// Moves a Pending reservation to Approved and attaches its QR token.
    /// Returns null when the reservation was no longer Pending.
    /// </summary>
    Task<EnergyReservation?> TryApproveAsync(
        string reservationId, string qrToken, DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a Pending reservation to Rejected.
    /// Returns null when the reservation was no longer Pending.
    /// </summary>
    Task<EnergyReservation?> TryRejectAsync(
        string reservationId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a Pending or Approved reservation to Cancelled and clears its QR
    /// token so a printed code cannot be presented afterwards.
    /// Returns null when the reservation was already closed.
    /// </summary>
    Task<EnergyReservation?> TryCancelAsync(
        string reservationId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an Approved reservation to Completed, recording which operator
    /// finalised the energy transfer.
    /// Returns null when the reservation was not Approved, which is how a
    /// second scan of the same QR code is refused.
    /// </summary>
    Task<EnergyReservation?> TryCompleteAsync(
        string reservationId, string operatorId, DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the station still has reservations that are Pending or
    /// Approved and start at or after the supplied instant. This is the check
    /// that blocks deactivation of a station that is still in use.
    /// </summary>
    Task<bool> HasActiveForStationAsync(
        string stationId, DateTime fromUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the slot still has reservations that are Pending or Approved,
    /// used to stop a booked window from being deleted.
    /// </summary>
    Task<bool> HasActiveForSlotAsync(
        string slotId, CancellationToken cancellationToken = default);
}
