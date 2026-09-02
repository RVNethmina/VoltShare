// -----------------------------------------------------------------------------
// File        : ISlotService.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Services
// Description : Contract for energy booking slot business logic: creating the
//               windows a station offers, and protecting windows that
//               prosumers have already booked.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Dtos;

namespace VoltShare.Api.Services;

/// <summary>
/// Management of the bookable energy transfer windows at a station.
/// </summary>
public interface ISlotService
{
    /// <summary>
    /// Lists the booking windows of a station within an optional date range.
    /// </summary>
    Task<IReadOnlyList<SlotResponse>> ListByStationAsync(
        string stationId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one booking window by identifier.
    /// </summary>
    Task<SlotResponse> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a booking window at a station.
    /// </summary>
    Task<SlotResponse> CreateAsync(
        string stationId, CreateSlotRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing booking window.
    /// </summary>
    Task<SlotResponse> UpdateAsync(
        string id, UpdateSlotRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a booking window. Refused while prosumers still hold active
    /// reservations against it.
    /// </summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
