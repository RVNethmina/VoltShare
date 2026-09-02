// -----------------------------------------------------------------------------
// File        : SlotService.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Services
// Description : Implements the energy booking slot rules: a window must belong
//               to an active station, must end after it starts, must not clash
//               with another window at the same station, cannot have its
//               capacity cut below what is already booked, and cannot be
//               deleted while prosumers still hold reservations against it.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Dtos;
using VoltShare.Api.Middleware;
using VoltShare.Api.Models;
using VoltShare.Api.Repositories;

namespace VoltShare.Api.Services;

/// <summary>
/// Central implementation of the booking slot rules.
/// </summary>
public class SlotService : ISlotService
{
    private readonly ISlotRepository _slots;
    private readonly IStationRepository _stations;
    private readonly IReservationRepository _reservations;
    private readonly ILogger<SlotService> _logger;

    /// <summary>
    /// Receives its collaborators from dependency injection.
    /// </summary>
    public SlotService(
        ISlotRepository slots,
        IStationRepository stations,
        IReservationRepository reservations,
        ILogger<SlotService> logger)
    {
        _slots = slots;
        _stations = stations;
        _reservations = reservations;
        _logger = logger;
    }

    /// <summary>
    /// Lists the booking windows of a station within an optional date range.
    /// </summary>
    public async Task<IReadOnlyList<SlotResponse>> ListByStationAsync(
        string stationId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        // Confirm the station exists so an unknown identifier reports a clear
        // not found error rather than silently returning an empty list.
        await GetRequiredStationAsync(stationId, cancellationToken);

        var slots = await _slots.ListByStationAsync(
            stationId, fromUtc, toUtc, isActive, cancellationToken);

        return slots.ToResponseList();
    }

    /// <summary>
    /// Returns one booking window, or reports that it does not exist.
    /// </summary>
    public async Task<SlotResponse> GetByIdAsync(
        string id, CancellationToken cancellationToken = default)
    {
        var slot = await GetRequiredSlotAsync(id, cancellationToken);
        return slot.ToResponse();
    }

    /// <summary>
    /// Creates a booking window at a station.
    /// </summary>
    public async Task<SlotResponse> CreateAsync(
        string stationId, CreateSlotRequest request, CancellationToken cancellationToken = default)
    {
        var station = await GetRequiredStationAsync(stationId, cancellationToken);

        // A decommissioned node must not take on new obligations.
        if (!station.IsActive)
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.StationInactive,
                "Booking windows cannot be added to a station that is not active.");
        }

        // Normalise to UTC so comparisons and storage are unambiguous whatever
        // the client sent.
        var startUtc = NormaliseToUtc(request.StartTimeUtc);
        var endUtc = NormaliseToUtc(request.EndTimeUtc);

        EnsureWindowValid(startUtc, endUtc);

        // The database has a unique index on station and start time; checking
        // first turns a driver duplicate key error into a clear message.
        if (await _slots.ExistsAtStartAsync(stationId, startUtc, cancellationToken: cancellationToken))
        {
            throw new ConflictException(
                ErrorCodes.SlotHasBookings,
                $"This station already offers a booking window starting at {startUtc:u}.");
        }

        var now = DateTime.UtcNow;
        var slot = new EnergyBookingSlot
        {
            StationId = stationId,
            StartTimeUtc = startUtc,
            EndTimeUtc = endUtc,
            Capacity = request.Capacity,
            BookedCount = 0,
            EnergyKwhPerSlot = request.EnergyKwhPerSlot,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _slots.InsertAsync(slot, cancellationToken);
        _logger.LogInformation(
            "Booking window created at station {StationId} for {Start:u}.", stationId, startUtc);

        return slot.ToResponse();
    }

    /// <summary>
    /// Updates an existing booking window.
    /// </summary>
    public async Task<SlotResponse> UpdateAsync(
        string id, UpdateSlotRequest request, CancellationToken cancellationToken = default)
    {
        var slot = await GetRequiredSlotAsync(id, cancellationToken);

        var startUtc = NormaliseToUtc(request.StartTimeUtc);
        var endUtc = NormaliseToUtc(request.EndTimeUtc);

        EnsureWindowValid(startUtc, endUtc);

        // Capacity may not be cut below the number of places already taken,
        // because that would leave existing bookings without a place.
        if (request.Capacity < slot.BookedCount)
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.SlotHasBookings,
                $"Capacity cannot be reduced to {request.Capacity} because " +
                $"{slot.BookedCount} place(s) are already booked.");
        }

        // Moving the window to a time another window already occupies would
        // violate the unique index on station and start time.
        if (startUtc != slot.StartTimeUtc &&
            await _slots.ExistsAtStartAsync(slot.StationId, startUtc, slot.Id, cancellationToken))
        {
            throw new ConflictException(
                ErrorCodes.SlotHasBookings,
                $"This station already offers a booking window starting at {startUtc:u}.");
        }

        slot.StartTimeUtc = startUtc;
        slot.EndTimeUtc = endUtc;
        slot.Capacity = request.Capacity;
        slot.EnergyKwhPerSlot = request.EnergyKwhPerSlot;
        slot.IsActive = request.IsActive;
        slot.UpdatedAtUtc = DateTime.UtcNow;

        await _slots.ReplaceAsync(slot, cancellationToken);
        return slot.ToResponse();
    }

    /// <summary>
    /// Deletes a booking window, provided no prosumer is still holding a
    /// reservation against it.
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var slot = await GetRequiredSlotAsync(id, cancellationToken);

        // Deleting a booked window would orphan those reservations, so refuse
        // and let staff cancel them explicitly first.
        if (await _reservations.HasActiveForSlotAsync(slot.Id, cancellationToken))
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.SlotHasBookings,
                "This booking window cannot be deleted because prosumers still hold " +
                "active reservations for it. Cancel those reservations first.");
        }

        await _slots.DeleteAsync(slot.Id, cancellationToken);
        _logger.LogInformation("Booking window {SlotId} deleted.", slot.Id);
    }

    /// <summary>
    /// Confirms a window ends after it starts and is not absurdly long.
    /// </summary>
    private static void EnsureWindowValid(DateTime startUtc, DateTime endUtc)
    {
        if (endUtc <= startUtc)
        {
            throw new ValidationException("The window end time must be after its start time.");
        }

        // A transfer window longer than a day almost certainly means the caller
        // sent the wrong date, so reject it rather than store it.
        if (endUtc - startUtc > TimeSpan.FromHours(24))
        {
            throw new ValidationException("A booking window cannot be longer than 24 hours.");
        }
    }

    /// <summary>
    /// Treats an incoming time as UTC so that storage and every later
    /// comparison use one consistent reference, whatever the client sent.
    /// </summary>
    private static DateTime NormaliseToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,

            // A local time is converted properly rather than reinterpreted.
            DateTimeKind.Local => value.ToUniversalTime(),

            // An unspecified kind is assumed to already be UTC, which is what
            // the API documentation asks clients to send.
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Loads a station and throws a not found error when it does not exist.
    /// </summary>
    private async Task<SolarStation> GetRequiredStationAsync(
        string stationId, CancellationToken cancellationToken)
    {
        var station = await _stations.GetByIdAsync(stationId, cancellationToken);

        if (station is null)
        {
            throw new NotFoundException($"No station was found with identifier '{stationId}'.");
        }

        return station;
    }

    /// <summary>
    /// Loads a booking window and throws a not found error when it is missing.
    /// </summary>
    private async Task<EnergyBookingSlot> GetRequiredSlotAsync(
        string id, CancellationToken cancellationToken)
    {
        var slot = await _slots.GetByIdAsync(id, cancellationToken);

        if (slot is null)
        {
            throw new NotFoundException($"No booking window was found with identifier '{id}'.");
        }

        return slot;
    }
}
