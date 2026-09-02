// -----------------------------------------------------------------------------
// File        : ISlotRepository.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Repositories
// Description : Data access contract for the "energyBookingSlots" collection,
//               including the atomic capacity operations that make concurrent
//               booking of the last free place safe.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Models;

namespace VoltShare.Api.Repositories;

/// <summary>
/// Reads and writes energy booking slot documents.
/// </summary>
public interface ISlotRepository
{
    /// <summary>
    /// Finds one booking slot by its identifier.
    /// </summary>
    Task<EnergyBookingSlot?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the slots of a station within an optional date range.
    /// </summary>
    Task<IReadOnlyList<EnergyBookingSlot>> ListByStationAsync(
        string stationId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new booking slot document.
    /// </summary>
    Task InsertAsync(EnergyBookingSlot slot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites an existing booking slot document.
    /// </summary>
    Task ReplaceAsync(EnergyBookingSlot slot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently removes a booking slot.
    /// </summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the station already offers a window starting at that instant.
    /// </summary>
    Task<bool> ExistsAtStartAsync(
        string stationId,
        DateTime startTimeUtc,
        string? excludeSlotId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically takes one place on a slot, but only if the slot is active and
    /// still has room. Returns the updated slot, or null when the slot was
    /// already full, which signals that the caller lost the race.
    /// </summary>
    Task<EnergyBookingSlot?> TryClaimPlaceAsync(
        string slotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically gives one place back after a cancellation. Guarded so the
    /// booked count can never fall below zero.
    /// </summary>
    Task ReleasePlaceAsync(string slotId, CancellationToken cancellationToken = default);
}
