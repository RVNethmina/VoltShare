// -----------------------------------------------------------------------------
// File        : IStationRepository.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Repositories
// Description : Data access contract for the "solarStationInfo" collection,
//               including the geospatial search used by the Android map.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Models;

namespace VoltShare.Api.Repositories;

/// <summary>
/// Reads and writes solar microgrid node documents.
/// </summary>
public interface IStationRepository
{
    /// <summary>
    /// Finds one station by its identifier.
    /// </summary>
    Task<SolarStation?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds one station by its unique human readable code.
    /// </summary>
    Task<SolarStation?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists stations, optionally narrowed by activation state, city and a
    /// free text search across code, name and address.
    /// </summary>
    Task<IReadOnlyList<SolarStation>> ListAsync(
        bool? isActive = null,
        string? city = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds active stations within a radius of a point, ordered nearest first,
    /// with the distance to each calculated by the database.
    /// </summary>
    Task<IReadOnlyList<SolarStationWithDistance>> FindNearbyAsync(
        double latitude,
        double longitude,
        double radiusMetres,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new station document.
    /// </summary>
    Task InsertAsync(SolarStation station, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites an existing station document.
    /// </summary>
    Task ReplaceAsync(SolarStation station, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the supplied code already belongs to a different station.
    /// </summary>
    Task<bool> CodeExistsAsync(
        string code, string? excludeStationId = null, CancellationToken cancellationToken = default);
}
