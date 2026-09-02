// -----------------------------------------------------------------------------
// File        : StationRepository.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Repositories
// Description : MongoDB implementation of IStationRepository, including the
//               $geoNear aggregation that powers the nearby stations map.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using VoltShare.Api.Data;
using VoltShare.Api.Models;

namespace VoltShare.Api.Repositories;

/// <summary>
/// Persists station documents in the "solarStationInfo" collection.
/// </summary>
public class StationRepository : IStationRepository
{
    private readonly IMongoCollection<SolarStation> _stations;

    // Shorthand for the driver filter builder, used throughout this class.
    private static readonly FilterDefinitionBuilder<SolarStation> Filter =
        Builders<SolarStation>.Filter;

    /// <summary>
    /// Resolves the stations collection from the shared database context.
    /// </summary>
    public StationRepository(MongoContext context)
    {
        _stations = context.SolarStations;
    }

    /// <summary>
    /// Finds one station by its identifier.
    /// </summary>
    public async Task<SolarStation?> GetByIdAsync(
        string id, CancellationToken cancellationToken = default)
    {
        // A value that is not a valid ObjectId can never match a stored
        // document, and would make the driver throw when building the filter.
        if (!ObjectId.TryParse(id, out _))
        {
            return null;
        }

        return await _stations.Find(Filter.Eq(s => s.Id, id))
                              .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Finds one station by its unique code.
    /// </summary>
    public async Task<SolarStation?> GetByCodeAsync(
        string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalised = code.Trim().ToUpperInvariant();

        return await _stations.Find(Filter.Eq(s => s.Code, normalised))
                              .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lists stations matching the supplied optional filters.
    /// </summary>
    public async Task<IReadOnlyList<SolarStation>> ListAsync(
        bool? isActive = null,
        string? city = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        // Start from an empty filter and narrow it only by what was supplied.
        var filters = new List<FilterDefinition<SolarStation>> { Filter.Empty };

        if (isActive.HasValue)
        {
            filters.Add(Filter.Eq(s => s.IsActive, isActive.Value));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            // Cities are matched case insensitively so "Colombo" and "colombo"
            // return the same list.
            filters.Add(Filter.Regex(s => s.City,
                new BsonRegularExpression($"^{Regex.Escape(city.Trim())}$", "i")));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // The term is escaped so characters such as . or * are treated as
            // literal text rather than as regular expression operators.
            var pattern = new BsonRegularExpression(Regex.Escape(search.Trim()), "i");

            filters.Add(Filter.Or(
                Filter.Regex(s => s.Code, pattern),
                Filter.Regex(s => s.Name, pattern),
                Filter.Regex(s => s.AddressLine, pattern)));
        }

        return await _stations.Find(Filter.And(filters))
                              .SortBy(s => s.Name)
                              .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Finds active stations within a radius of a point using the $geoNear
    /// aggregation stage, which returns them ordered nearest first and adds
    /// the distance to each document.
    /// </summary>
    public async Task<IReadOnlyList<SolarStationWithDistance>> FindNearbyAsync(
        double latitude,
        double longitude,
        double radiusMetres,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // $geoNear must be the first stage of the pipeline and requires the
        // 2dsphere index created at start-up. Note the GeoJSON coordinate
        // order is [longitude, latitude].
        var geoNear = new BsonDocument("$geoNear", new BsonDocument
        {
            {
                "near", new BsonDocument
                {
                    { "type", "Point" },
                    { "coordinates", new BsonArray { longitude, latitude } }
                }
            },
            // Name of the field the calculated distance is written into.
            { "distanceField", "distanceMeters" },
            { "maxDistance", radiusMetres },

            // Spherical geometry is required for a 2dsphere index and gives
            // true earth distances rather than flat plane distances.
            { "spherical", true },

            // Only active stations are offered to prosumers on the map.
            { "query", new BsonDocument("isActive", true) }
        });

        // Cap the result size so a very large radius cannot return the whole
        // collection to a mobile device.
        var limitStage = new BsonDocument("$limit", limit);

        var pipeline = PipelineDefinition<SolarStation, SolarStationWithDistance>
            .Create(geoNear, limitStage);

        var cursor = await _stations.AggregateAsync(
            pipeline, cancellationToken: cancellationToken);

        return await cursor.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Stores a new station document.
    /// </summary>
    public async Task InsertAsync(
        SolarStation station, CancellationToken cancellationToken = default)
    {
        await _stations.InsertOneAsync(station, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Overwrites an existing station document.
    /// </summary>
    public async Task ReplaceAsync(
        SolarStation station, CancellationToken cancellationToken = default)
    {
        await _stations.ReplaceOneAsync(
            Filter.Eq(s => s.Id, station.Id), station, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// True when the code is already used by a different station.
    /// </summary>
    public async Task<bool> CodeExistsAsync(
        string code, string? excludeStationId = null, CancellationToken cancellationToken = default)
    {
        var normalised = code.Trim().ToUpperInvariant();
        var filter = Filter.Eq(s => s.Code, normalised);

        // When a station is being edited its own document must not count as a
        // clash with itself.
        if (!string.IsNullOrWhiteSpace(excludeStationId))
        {
            filter = Filter.And(filter, Filter.Ne(s => s.Id, excludeStationId));
        }

        return await _stations.Find(filter).AnyAsync(cancellationToken);
    }
}
