// -----------------------------------------------------------------------------
// File        : ReservationRepository.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Repositories
// Description : MongoDB implementation of IReservationRepository.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using MongoDB.Bson;
using MongoDB.Driver;
using VoltShare.Api.Data;
using VoltShare.Api.Models;

namespace VoltShare.Api.Repositories;

/// <summary>
/// Persists reservation documents in the "energyReservations" collection.
/// </summary>
public class ReservationRepository : IReservationRepository
{
    private readonly IMongoCollection<EnergyReservation> _reservations;

    // Shorthand for the driver filter builder, used throughout this class.
    private static readonly FilterDefinitionBuilder<EnergyReservation> Filter =
        Builders<EnergyReservation>.Filter;

    /// <summary>
    /// Resolves the reservations collection from the shared database context.
    /// </summary>
    public ReservationRepository(MongoContext context)
    {
        _reservations = context.Reservations;
    }

    /// <summary>
    /// True when the station still holds future reservations that have not
    /// been cancelled, rejected or completed.
    /// </summary>
    public async Task<bool> HasActiveForStationAsync(
        string stationId, DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(stationId, out _))
        {
            return false;
        }

        // Only Pending and Approved bookings hold the station: a cancelled or
        // completed booking places no further obligation on it.
        var filter = Filter.And(
            Filter.Eq(r => r.StationId, stationId),
            Filter.In(r => r.Status, ReservationStatus.Active),
            Filter.Gte(r => r.ReservationStartUtc, fromUtc));

        return await _reservations.Find(filter).AnyAsync(cancellationToken);
    }

    /// <summary>
    /// True when the slot still holds reservations that have not been
    /// cancelled, rejected or completed.
    /// </summary>
    public async Task<bool> HasActiveForSlotAsync(
        string slotId, CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(slotId, out _))
        {
            return false;
        }

        var filter = Filter.And(
            Filter.Eq(r => r.SlotId, slotId),
            Filter.In(r => r.Status, ReservationStatus.Active));

        return await _reservations.Find(filter).AnyAsync(cancellationToken);
    }
}
