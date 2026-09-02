// -----------------------------------------------------------------------------
// File        : SlotRepository.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Repositories
// Description : MongoDB implementation of ISlotRepository. The capacity
//               operations use a single atomic FindOneAndUpdate so that two
//               prosumers cannot both take the last free place on a slot.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using MongoDB.Bson;
using MongoDB.Driver;
using VoltShare.Api.Data;
using VoltShare.Api.Models;

namespace VoltShare.Api.Repositories;

/// <summary>
/// Persists booking slot documents in the "energyBookingSlots" collection.
/// </summary>
public class SlotRepository : ISlotRepository
{
    private readonly IMongoCollection<EnergyBookingSlot> _slots;

    // Shorthand for the driver builders, used throughout this class.
    private static readonly FilterDefinitionBuilder<EnergyBookingSlot> Filter =
        Builders<EnergyBookingSlot>.Filter;

    private static readonly UpdateDefinitionBuilder<EnergyBookingSlot> Update =
        Builders<EnergyBookingSlot>.Update;

    /// <summary>
    /// Resolves the slots collection from the shared database context.
    /// </summary>
    public SlotRepository(MongoContext context)
    {
        _slots = context.BookingSlots;
    }

    /// <summary>
    /// Finds one booking slot by its identifier.
    /// </summary>
    public async Task<EnergyBookingSlot?> GetByIdAsync(
        string id, CancellationToken cancellationToken = default)
    {
        // Guard against a value that is not a valid ObjectId, which would
        // otherwise make the driver throw while building the filter.
        if (!ObjectId.TryParse(id, out _))
        {
            return null;
        }

        return await _slots.Find(Filter.Eq(s => s.Id, id))
                           .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lists the slots of a station within an optional date range.
    /// </summary>
    public async Task<IReadOnlyList<EnergyBookingSlot>> ListByStationAsync(
        string stationId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(stationId, out _))
        {
            return Array.Empty<EnergyBookingSlot>();
        }

        // Build the range filter from only the bounds that were supplied.
        var filters = new List<FilterDefinition<EnergyBookingSlot>>
        {
            Filter.Eq(s => s.StationId, stationId)
        };

        if (fromUtc.HasValue)
        {
            filters.Add(Filter.Gte(s => s.StartTimeUtc, fromUtc.Value));
        }

        if (toUtc.HasValue)
        {
            filters.Add(Filter.Lt(s => s.StartTimeUtc, toUtc.Value));
        }

        if (isActive.HasValue)
        {
            filters.Add(Filter.Eq(s => s.IsActive, isActive.Value));
        }

        return await _slots.Find(Filter.And(filters))
                           .SortBy(s => s.StartTimeUtc)
                           .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Stores a new booking slot document.
    /// </summary>
    public async Task InsertAsync(
        EnergyBookingSlot slot, CancellationToken cancellationToken = default)
    {
        await _slots.InsertOneAsync(slot, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Overwrites an existing booking slot document.
    /// </summary>
    public async Task ReplaceAsync(
        EnergyBookingSlot slot, CancellationToken cancellationToken = default)
    {
        await _slots.ReplaceOneAsync(
            Filter.Eq(s => s.Id, slot.Id), slot, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Permanently removes a booking slot.
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _slots.DeleteOneAsync(Filter.Eq(s => s.Id, id), cancellationToken);
    }

    /// <summary>
    /// True when the station already offers a window starting at that instant.
    /// </summary>
    public async Task<bool> ExistsAtStartAsync(
        string stationId,
        DateTime startTimeUtc,
        string? excludeSlotId = null,
        CancellationToken cancellationToken = default)
    {
        var filter = Filter.And(
            Filter.Eq(s => s.StationId, stationId),
            Filter.Eq(s => s.StartTimeUtc, startTimeUtc));

        // When a slot is being edited its own document must not count as a
        // clash with itself.
        if (!string.IsNullOrWhiteSpace(excludeSlotId))
        {
            filter = Filter.And(filter, Filter.Ne(s => s.Id, excludeSlotId));
        }

        return await _slots.Find(filter).AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Atomically takes one place on a slot.
    ///
    /// The availability test is expressed as part of the filter rather than as
    /// a separate read, so MongoDB evaluates the condition and applies the
    /// increment as one indivisible operation. Reading the slot first and then
    /// saving it would leave a window in which two requests both see the same
    /// free place and both take it, overbooking the slot.
    /// </summary>
    public async Task<EnergyBookingSlot?> TryClaimPlaceAsync(
        string slotId, CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(slotId, out _))
        {
            return null;
        }

        // Comparing two fields of the same document requires the $expr
        // operator, which the typed builder cannot express; a raw document is
        // used here and converts implicitly to a filter definition.
        FilterDefinition<EnergyBookingSlot> hasFreePlace = new BsonDocument(
            "$expr", new BsonDocument("$lt", new BsonArray { "$bookedCount", "$capacity" }));

        // Match only a slot that is active and still has a free place.
        var filter = Filter.And(
            Filter.Eq(s => s.Id, slotId),
            Filter.Eq(s => s.IsActive, true),
            hasFreePlace);

        var update = Update
            .Inc(s => s.BookedCount, 1)
            .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);

        // Returning the document after the update lets the caller see the new
        // booked count without a second query.
        var options = new FindOneAndUpdateOptions<EnergyBookingSlot>
        {
            ReturnDocument = ReturnDocument.After
        };

        // A null result means no document satisfied the filter, that is the
        // slot was inactive or already full.
        return await _slots.FindOneAndUpdateAsync(
            filter, update, options, cancellationToken);
    }

    /// <summary>
    /// Atomically gives one place back after a cancellation.
    /// </summary>
    public async Task ReleasePlaceAsync(
        string slotId, CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(slotId, out _))
        {
            return;
        }

        // The booked count guard stops a double cancellation from driving the
        // counter negative.
        var filter = Filter.And(
            Filter.Eq(s => s.Id, slotId),
            Filter.Gt(s => s.BookedCount, 0));

        var update = Update
            .Inc(s => s.BookedCount, -1)
            .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);

        await _slots.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}
