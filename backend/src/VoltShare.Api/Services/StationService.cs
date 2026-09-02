// -----------------------------------------------------------------------------
// File        : StationService.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Services
// Description : Implements the solar microgrid node rules: unique station
//               codes, valid coordinates, battery slot availability bounded by
//               the installed total, and the rule that a station cannot be
//               deactivated while it still holds active energy reservations.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using MongoDB.Driver.GeoJsonObjectModel;
using VoltShare.Api.Dtos;
using VoltShare.Api.Middleware;
using VoltShare.Api.Models;
using VoltShare.Api.Repositories;

namespace VoltShare.Api.Services;

/// <summary>
/// Central implementation of the station rules for both client applications.
/// </summary>
public class StationService : IStationService
{
    private readonly IStationRepository _stations;
    private readonly IReservationRepository _reservations;
    private readonly ILogger<StationService> _logger;

    // Largest radius the nearby search will accept, to stop a mobile client
    // from asking for the entire collection in one request.
    private const double MaxSearchRadiusKm = 200d;

    // Largest number of stations returned by a single nearby search.
    private const int MaxNearbyResults = 100;

    /// <summary>
    /// Receives its collaborators from dependency injection.
    /// </summary>
    public StationService(
        IStationRepository stations,
        IReservationRepository reservations,
        ILogger<StationService> logger)
    {
        _stations = stations;
        _reservations = reservations;
        _logger = logger;
    }

    /// <summary>
    /// Lists stations using the supplied optional filters.
    /// </summary>
    public async Task<IReadOnlyList<StationResponse>> ListAsync(
        bool? isActive = null,
        string? city = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var stations = await _stations.ListAsync(isActive, city, search, cancellationToken);
        return stations.ToResponseList();
    }

    /// <summary>
    /// Returns one station, or reports that it does not exist.
    /// </summary>
    public async Task<StationResponse> GetByIdAsync(
        string id, CancellationToken cancellationToken = default)
    {
        var station = await GetRequiredAsync(id, cancellationToken);
        return station.ToResponse();
    }

    /// <summary>
    /// Finds active stations near a point. The distance is computed by MongoDB
    /// so the Android application only has to draw the markers it is given.
    /// </summary>
    public async Task<IReadOnlyList<NearbyStationResponse>> FindNearbyAsync(
        double latitude,
        double longitude,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Validate the point before it reaches the database, because $geoNear
        // rejects out of range coordinates with an unhelpful driver error.
        if (latitude is < -90 or > 90)
        {
            throw new ValidationException("Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ValidationException("Longitude must be between -180 and 180.");
        }

        if (radiusKm <= 0)
        {
            throw new ValidationException("Radius must be greater than zero.");
        }

        // Clamp rather than reject an oversized request, so a client asking for
        // too much still receives a useful answer.
        var effectiveRadiusKm = Math.Min(radiusKm, MaxSearchRadiusKm);
        var effectiveLimit = limit <= 0 ? MaxNearbyResults : Math.Min(limit, MaxNearbyResults);

        // $geoNear expects the radius in metres.
        var results = await _stations.FindNearbyAsync(
            latitude, longitude, effectiveRadiusKm * 1000d, effectiveLimit, cancellationToken);

        return results
            .Select(s => new NearbyStationResponse(s.ToResponse(), Math.Round(s.DistanceMeters, 1)))
            .ToList();
    }

    /// <summary>
    /// Creates a new station after checking that its code is unique and that
    /// the battery slot figures are consistent.
    /// </summary>
    public async Task<StationResponse> CreateAsync(
        CreateStationRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();

        // The code is the reference staff use to identify a node, so it must
        // never be ambiguous.
        if (await _stations.CodeExistsAsync(code, cancellationToken: cancellationToken))
        {
            throw new ConflictException(
                ErrorCodes.StationCodeAlreadyUsed,
                $"A station already exists with code {code}.");
        }

        // When the caller does not say how many battery slots are free, assume
        // the station starts with all of them available.
        var available = request.AvailableBatterySlots ?? request.TotalBatterySlots;

        // A station cannot have more slots free than it physically has.
        if (available > request.TotalBatterySlots)
        {
            throw new ValidationException(
                "Available battery slots cannot exceed the total number of battery slots.");
        }

        EnsureOperatingHoursValid(request.OpenTime, request.CloseTime);

        var now = DateTime.UtcNow;
        var station = new SolarStation
        {
            Code = code,
            Name = request.Name.Trim(),
            AddressLine = request.AddressLine.Trim(),
            City = request.City.Trim(),
            Location = BuildPoint(request.Latitude, request.Longitude),
            CapacityKwh = request.CapacityKwh,
            TotalBatterySlots = request.TotalBatterySlots,
            AvailableBatterySlots = available,
            OperatingHours = new OperatingHours
            {
                OpenTime = request.OpenTime,
                CloseTime = request.CloseTime
            },
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _stations.InsertAsync(station, cancellationToken);
        _logger.LogInformation("Station {Code} created.", code);

        return station.ToResponse();
    }

    /// <summary>
    /// Updates an existing station. The code is not editable because other
    /// records refer to the station by it.
    /// </summary>
    public async Task<StationResponse> UpdateAsync(
        string id, UpdateStationRequest request, CancellationToken cancellationToken = default)
    {
        var station = await GetRequiredAsync(id, cancellationToken);

        EnsureOperatingHoursValid(request.OpenTime, request.CloseTime);

        // Reducing the installed total below the number currently free would
        // leave the two figures contradicting each other, so bring the
        // available count down with it.
        var available = Math.Min(station.AvailableBatterySlots, request.TotalBatterySlots);

        station.Name = request.Name.Trim();
        station.AddressLine = request.AddressLine.Trim();
        station.City = request.City.Trim();
        station.Location = BuildPoint(request.Latitude, request.Longitude);
        station.CapacityKwh = request.CapacityKwh;
        station.TotalBatterySlots = request.TotalBatterySlots;
        station.AvailableBatterySlots = available;
        station.OperatingHours = new OperatingHours
        {
            OpenTime = request.OpenTime,
            CloseTime = request.CloseTime
        };
        station.UpdatedAtUtc = DateTime.UtcNow;

        await _stations.ReplaceAsync(station, cancellationToken);
        return station.ToResponse();
    }

    /// <summary>
    /// Brings a station back into service.
    /// </summary>
    public async Task<StationResponse> ActivateAsync(
        string id, CancellationToken cancellationToken = default)
    {
        var station = await GetRequiredAsync(id, cancellationToken);

        station.IsActive = true;
        station.UpdatedAtUtc = DateTime.UtcNow;

        await _stations.ReplaceAsync(station, cancellationToken);
        _logger.LogInformation("Station {Code} activated.", station.Code);

        return station.ToResponse();
    }

    /// <summary>
    /// Takes a station out of service.
    ///
    /// This is the rule the specification calls out explicitly: deactivation is
    /// blocked while the station still holds reservations that are Pending or
    /// Approved and have not yet happened, because those prosumers are still
    /// expecting to trade energy there.
    /// </summary>
    public async Task<StationResponse> DeactivateAsync(
        string id, CancellationToken cancellationToken = default)
    {
        var station = await GetRequiredAsync(id, cancellationToken);

        // Only reservations that are still in the future matter: a booking in
        // the past can no longer be fulfilled and must not block maintenance.
        var hasActive = await _reservations.HasActiveForStationAsync(
            station.Id, DateTime.UtcNow, cancellationToken);

        if (hasActive)
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.StationHasActiveReservations,
                "This station cannot be deactivated because it still has active energy " +
                "reservations. Cancel or complete them first.");
        }

        station.IsActive = false;
        station.UpdatedAtUtc = DateTime.UtcNow;

        await _stations.ReplaceAsync(station, cancellationToken);
        _logger.LogInformation("Station {Code} deactivated.", station.Code);

        return station.ToResponse();
    }

    /// <summary>
    /// Updates how many battery storage slots are currently free, which is a
    /// grid operator responsibility.
    /// </summary>
    public async Task<StationResponse> UpdateBatterySlotsAsync(
        string id, UpdateBatterySlotsRequest request, CancellationToken cancellationToken = default)
    {
        var station = await GetRequiredAsync(id, cancellationToken);

        // The free count can never exceed the number physically installed.
        if (request.AvailableBatterySlots > station.TotalBatterySlots)
        {
            throw new ValidationException(
                $"Available battery slots cannot exceed the installed total of " +
                $"{station.TotalBatterySlots}.");
        }

        station.AvailableBatterySlots = request.AvailableBatterySlots;
        station.UpdatedAtUtc = DateTime.UtcNow;

        await _stations.ReplaceAsync(station, cancellationToken);
        return station.ToResponse();
    }

    /// <summary>
    /// Builds the GeoJSON point stored on the document. Written in one place so
    /// the longitude and latitude order cannot be swapped by mistake.
    /// </summary>
    private static GeoJsonPoint<GeoJson2DGeographicCoordinates> BuildPoint(
        double latitude, double longitude)
    {
        // GeoJson.Geographic takes longitude first, then latitude.
        return GeoJson.Point(GeoJson.Geographic(longitude, latitude));
    }

    /// <summary>
    /// Confirms the station closes later in the day than it opens.
    /// </summary>
    private static void EnsureOperatingHoursValid(string openTime, string closeTime)
    {
        // The format itself is checked by the request annotations; this is the
        // ordering rule those annotations cannot express.
        if (string.CompareOrdinal(openTime, closeTime) >= 0)
        {
            throw new ValidationException("Close time must be later than open time.");
        }
    }

    /// <summary>
    /// Loads a station and throws a not found error when it does not exist.
    /// </summary>
    private async Task<SolarStation> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        var station = await _stations.GetByIdAsync(id, cancellationToken);

        if (station is null)
        {
            throw new NotFoundException($"No station was found with identifier '{id}'.");
        }

        return station;
    }
}
