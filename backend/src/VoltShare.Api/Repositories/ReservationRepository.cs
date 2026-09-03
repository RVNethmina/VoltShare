// -----------------------------------------------------------------------------
// File        : ReservationRepository.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Repositories
// Description : MongoDB implementation of IReservationRepository. The status
//               transitions are written as guarded atomic updates so that two
//               simultaneous requests cannot both act on the same booking.
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
/// Persists reservation documents in the "energyReservations" collection.
/// </summary>
public class ReservationRepository : IReservationRepository
{
    private readonly IMongoCollection<EnergyReservation> _reservations;

    // Shorthand for the driver builders, used throughout this class.
    private static readonly FilterDefinitionBuilder<EnergyReservation> Filter =
        Builders<EnergyReservation>.Filter;

    private static readonly UpdateDefinitionBuilder<EnergyReservation> Update =
        Builders<EnergyReservation>.Update;

    // Returning the document after an update lets callers see the new state
    // without issuing a second query.
    private static readonly FindOneAndUpdateOptions<EnergyReservation> ReturnUpdated =
        new() { ReturnDocument = ReturnDocument.After };

    /// <summary>
    /// Resolves the reservations collection from the shared database context.
    /// </summary>
    public ReservationRepository(MongoContext context)
    {
        _reservations = context.Reservations;
    }

    /// <summary>
    /// Finds one reservation by its identifier.
    /// </summary>
    public async Task<EnergyReservation?> GetByIdAsync(
        string id, CancellationToken cancellationToken = default)
    {
        // A value that is not a valid ObjectId can never match a document and
        // would make the driver throw while building the filter.
        if (!ObjectId.TryParse(id, out _))
        {
            return null;
        }

        return await _reservations.Find(Filter.Eq(r => r.Id, id))
                                  .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Finds one reservation by the QR token issued for it.
    /// </summary>
    public async Task<EnergyReservation?> GetByQrTokenAsync(
        string qrToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qrToken))
        {
            return null;
        }

        return await _reservations.Find(Filter.Eq(r => r.QrToken, qrToken))
                                  .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lists reservations matching the supplied criteria.
    /// </summary>
    public async Task<IReadOnlyList<EnergyReservation>> SearchAsync(
        ReservationQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query);

        // Sorted soonest first so the booking lists read naturally, and capped
        // so a large history cannot flood a mobile device.
        return await _reservations.Find(filter)
                                  .SortByDescending(r => r.ReservationStartUtc)
                                  .Limit(query.Limit <= 0 ? 200 : query.Limit)
                                  .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Counts reservations matching the supplied criteria.
    /// </summary>
    public async Task<long> CountAsync(
        ReservationQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query);
        return await _reservations.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Returns the next upcoming open reservation for a prosumer.
    /// </summary>
    public async Task<EnergyReservation?> GetNextUpcomingAsync(
        string prosumerNic, DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        var filter = Filter.And(
            Filter.Eq(r => r.ProsumerNic, prosumerNic),
            Filter.In(r => r.Status, ReservationStatus.Active),
            Filter.Gte(r => r.ReservationStartUtc, fromUtc));

        // Ascending order so the first result is the soonest booking.
        return await _reservations.Find(filter)
                                  .SortBy(r => r.ReservationStartUtc)
                                  .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// True when the prosumer already holds an open booking on that slot.
    /// </summary>
    public async Task<bool> ExistsActiveForProsumerAndSlotAsync(
        string prosumerNic,
        string slotId,
        string? excludeReservationId = null,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(slotId, out _))
        {
            return false;
        }

        var filter = Filter.And(
            Filter.Eq(r => r.ProsumerNic, prosumerNic),
            Filter.Eq(r => r.SlotId, slotId),
            Filter.In(r => r.Status, ReservationStatus.Active));

        // When a booking is being edited its own document must not count as a
        // clash with itself.
        if (!string.IsNullOrWhiteSpace(excludeReservationId))
        {
            filter = Filter.And(filter, Filter.Ne(r => r.Id, excludeReservationId));
        }

        return await _reservations.Find(filter).AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Stores a new reservation document.
    /// </summary>
    public async Task InsertAsync(
        EnergyReservation reservation, CancellationToken cancellationToken = default)
    {
        await _reservations.InsertOneAsync(reservation, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Overwrites an existing reservation document.
    /// </summary>
    public async Task ReplaceAsync(
        EnergyReservation reservation, CancellationToken cancellationToken = default)
    {
        await _reservations.ReplaceOneAsync(
            Filter.Eq(r => r.Id, reservation.Id), reservation,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Moves a Pending reservation to Approved and attaches its QR token.
    /// </summary>
    public async Task<EnergyReservation?> TryApproveAsync(
        string reservationId, string qrToken, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        // Pending is part of the filter, so a reservation that somebody else
        // already approved or cancelled simply will not match.
        var filter = Filter.And(
            Filter.Eq(r => r.Id, reservationId),
            Filter.Eq(r => r.Status, ReservationStatus.Pending));

        var update = Update
            .Set(r => r.Status, ReservationStatus.Approved)
            .Set(r => r.QrToken, qrToken)
            .Set(r => r.QrIssuedAtUtc, nowUtc)
            .Set(r => r.UpdatedAtUtc, nowUtc);

        return await _reservations.FindOneAndUpdateAsync(
            filter, update, ReturnUpdated, cancellationToken);
    }

    /// <summary>
    /// Moves a Pending reservation to Rejected.
    /// </summary>
    public async Task<EnergyReservation?> TryRejectAsync(
        string reservationId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var filter = Filter.And(
            Filter.Eq(r => r.Id, reservationId),
            Filter.Eq(r => r.Status, ReservationStatus.Pending));

        var update = Update
            .Set(r => r.Status, ReservationStatus.Rejected)
            .Set(r => r.UpdatedAtUtc, nowUtc);

        return await _reservations.FindOneAndUpdateAsync(
            filter, update, ReturnUpdated, cancellationToken);
    }

    /// <summary>
    /// Moves a Pending or Approved reservation to Cancelled.
    /// </summary>
    public async Task<EnergyReservation?> TryCancelAsync(
        string reservationId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var filter = Filter.And(
            Filter.Eq(r => r.Id, reservationId),
            Filter.In(r => r.Status, ReservationStatus.Active));

        var update = Update
            .Set(r => r.Status, ReservationStatus.Cancelled)
            .Set(r => r.CancelledAtUtc, nowUtc)
            .Set(r => r.UpdatedAtUtc, nowUtc)

            // The token is removed so a screenshot of the QR code taken before
            // cancellation cannot be presented at the station afterwards.
            .Unset(r => r.QrToken);

        return await _reservations.FindOneAndUpdateAsync(
            filter, update, ReturnUpdated, cancellationToken);
    }

    /// <summary>
    /// Moves an Approved reservation to Completed.
    /// </summary>
    public async Task<EnergyReservation?> TryCompleteAsync(
        string reservationId, string operatorId, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        // Requiring Approved here is what makes a QR code single use: once the
        // first scan completes the booking, a second scan matches nothing.
        var filter = Filter.And(
            Filter.Eq(r => r.Id, reservationId),
            Filter.Eq(r => r.Status, ReservationStatus.Approved));

        var update = Update
            .Set(r => r.Status, ReservationStatus.Completed)
            .Set(r => r.CompletedAtUtc, nowUtc)
            .Set(r => r.CompletedByOperatorId, operatorId)
            .Set(r => r.UpdatedAtUtc, nowUtc);

        return await _reservations.FindOneAndUpdateAsync(
            filter, update, ReturnUpdated, cancellationToken);
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

    /// <summary>
    /// Turns the search criteria into a MongoDB filter, adding only the
    /// conditions that were actually supplied.
    /// </summary>
    private static FilterDefinition<EnergyReservation> BuildFilter(ReservationQuery query)
    {
        var filters = new List<FilterDefinition<EnergyReservation>> { Filter.Empty };

        if (!string.IsNullOrWhiteSpace(query.ProsumerNic))
        {
            filters.Add(Filter.Eq(r => r.ProsumerNic, query.ProsumerNic));
        }

        if (!string.IsNullOrWhiteSpace(query.StationId) && ObjectId.TryParse(query.StationId, out _))
        {
            filters.Add(Filter.Eq(r => r.StationId, query.StationId));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filters.Add(Filter.Eq(r => r.Status, query.Status));
        }

        if (query.Statuses is { Length: > 0 })
        {
            filters.Add(Filter.In(r => r.Status, query.Statuses));
        }

        if (query.FromUtc.HasValue)
        {
            filters.Add(Filter.Gte(r => r.ReservationStartUtc, query.FromUtc.Value));
        }

        if (query.ToUtc.HasValue)
        {
            filters.Add(Filter.Lt(r => r.ReservationStartUtc, query.ToUtc.Value));
        }

        if (query.CompletedFromUtc.HasValue)
        {
            filters.Add(Filter.Gte(r => r.CompletedAtUtc, query.CompletedFromUtc.Value));
        }

        if (query.CompletedToUtc.HasValue)
        {
            filters.Add(Filter.Lt(r => r.CompletedAtUtc, query.CompletedToUtc.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // The term is escaped so characters such as . or * are treated as
            // literal text rather than as regular expression operators.
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");

            filters.Add(Filter.Or(
                Filter.Regex(r => r.ReservationNo, pattern),
                Filter.Regex(r => r.ProsumerNic, pattern)));
        }

        return Filter.And(filters);
    }
}
