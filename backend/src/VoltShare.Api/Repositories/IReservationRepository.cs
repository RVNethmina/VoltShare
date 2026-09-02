// -----------------------------------------------------------------------------
// File        : IReservationRepository.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Repositories
// Description : Data access contract for the "energyReservations" collection.
//               At this stage it carries the lookups needed to protect
//               stations and slots from being removed while they are still in
//               use; the booking operations are added with the reservation
//               feature itself.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

namespace VoltShare.Api.Repositories;

/// <summary>
/// Reads and writes energy reservation documents.
/// </summary>
public interface IReservationRepository
{
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
