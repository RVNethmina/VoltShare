// -----------------------------------------------------------------------------
// File        : IStationService.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Services
// Description : Contract for solar microgrid node business logic, including
//               the rule that a station holding active reservations may not
//               be deactivated.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Dtos;

namespace VoltShare.Api.Services;

/// <summary>
/// Management of solar microgrid nodes.
/// </summary>
public interface IStationService
{
    /// <summary>
    /// Lists stations with optional activation, city and search filters.
    /// </summary>
    Task<IReadOnlyList<StationResponse>> ListAsync(
        bool? isActive = null,
        string? city = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one station by identifier.
    /// </summary>
    Task<StationResponse> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds active stations near a point, ordered nearest first. This is what
    /// the Android map calls to plot the grid nodes around the prosumer.
    /// </summary>
    Task<IReadOnlyList<NearbyStationResponse>> FindNearbyAsync(
        double latitude,
        double longitude,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new station.
    /// </summary>
    Task<StationResponse> CreateAsync(
        CreateStationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing station.
    /// </summary>
    Task<StationResponse> UpdateAsync(
        string id, UpdateStationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings a station back into service.
    /// </summary>
    Task<StationResponse> ActivateAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a station out of service. Refused while the station still holds
    /// active energy reservations.
    /// </summary>
    Task<StationResponse> DeactivateAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the number of free battery storage slots at a station.
    /// </summary>
    Task<StationResponse> UpdateBatterySlotsAsync(
        string id, UpdateBatterySlotsRequest request, CancellationToken cancellationToken = default);
}
