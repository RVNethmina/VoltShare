// -----------------------------------------------------------------------------
// File        : ReservationService.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Services
// Description : Implements every energy reservation rule required by the
//               specification: a booking must start within seven days, changes
//               and cancellations need twelve hours notice, a slot cannot be
//               overbooked, only active prosumers and stations may trade, and
//               an approved booking carries a single use QR token that a grid
//               operator verifies against the server before completing it.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using MongoDB.Bson;
using VoltShare.Api.Dtos;
using VoltShare.Api.Middleware;
using VoltShare.Api.Models;
using VoltShare.Api.Repositories;
using VoltShare.Api.Security;

namespace VoltShare.Api.Services;

/// <summary>
/// Central implementation of the reservation rules for both clients.
/// </summary>
public class ReservationService : IReservationService
{
    private readonly IReservationRepository _reservations;
    private readonly ISlotRepository _slots;
    private readonly IStationRepository _stations;
    private readonly IUserRepository _users;
    private readonly IQrTokenService _qrTokens;
    private readonly ILogger<ReservationService> _logger;

    /// <summary>
    /// Receives its collaborators from dependency injection.
    /// </summary>
    public ReservationService(
        IReservationRepository reservations,
        ISlotRepository slots,
        IStationRepository stations,
        IUserRepository users,
        IQrTokenService qrTokens,
        ILogger<ReservationService> logger)
    {
        _reservations = reservations;
        _slots = slots;
        _stations = stations;
        _users = users;
        _qrTokens = qrTokens;
        _logger = logger;
    }

    /// <summary>
    /// Creates a booking against a slot.
    /// </summary>
    public async Task<ReservationSummaryResponse> CreateAsync(
        CreateReservationRequest request,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // A prosumer always books for themselves; only staff may name someone
        // else, which stops one prosumer booking in another person's name.
        var prosumerNic = caller.IsStaff
            ? (request.ProsumerNic ?? string.Empty).Trim().ToUpperInvariant()
            : caller.UserId;

        if (string.IsNullOrWhiteSpace(prosumerNic))
        {
            throw new ValidationException("A prosumer NIC is required when staff create a booking.");
        }

        EnsureTypeValid(request.Type);

        // Rule: an inactive prosumer may not trade energy.
        var prosumer = await _users.GetByIdAsync(prosumerNic, cancellationToken)
            ?? throw new NotFoundException($"No prosumer was found with NIC '{prosumerNic}'.");

        if (prosumer.Role != UserRoles.Prosumer)
        {
            throw new ValidationException(
                "Bookings can only be made for prosumer accounts.", ErrorCodes.RoleNotAllowed);
        }

        if (!prosumer.IsActive)
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.AccountInactive,
                "This prosumer account is not active and cannot make bookings.");
        }

        var slot = await GetRequiredSlotAsync(request.SlotId, cancellationToken);

        if (!slot.IsActive)
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.SlotInactive, "This booking window is no longer available.");
        }

        // Rule: an inactive station may not take bookings.
        var station = await _stations.GetByIdAsync(slot.StationId, cancellationToken)
            ?? throw new NotFoundException("The station for this booking window no longer exists.");

        if (!station.IsActive)
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.StationInactive, "This station is not currently in service.");
        }

        // Rule: the booking must be in the future and within seven days.
        EnsureWithinBookingHorizon(slot.StartTimeUtc, now);

        // One prosumer must not hold two open bookings on the same window.
        if (await _reservations.ExistsActiveForProsumerAndSlotAsync(
                prosumerNic, slot.Id, cancellationToken: cancellationToken))
        {
            throw new ConflictException(
                ErrorCodes.DuplicateReservation,
                "You already hold a booking for this window.");
        }

        // Rule: capacity. Claiming the place is a single atomic operation, so
        // two prosumers cannot both take the last place on the slot.
        var claimed = await _slots.TryClaimPlaceAsync(slot.Id, cancellationToken)
            ?? throw new ConflictException(
                ErrorCodes.SlotFull, "This booking window is now full.");

        // The identifier is generated up front so the human readable
        // reservation number can be derived from it and stays unique without
        // needing a separate counter collection.
        var id = ObjectId.GenerateNewId();
        var reservation = new EnergyReservation
        {
            Id = id.ToString(),
            ReservationNo = BuildReservationNo(id, now),
            ProsumerNic = prosumerNic,
            StationId = station.Id,
            SlotId = claimed.Id,
            ReservationStartUtc = claimed.StartTimeUtc,
            ReservationEndUtc = claimed.EndTimeUtc,
            EnergyKwh = claimed.EnergyKwhPerSlot,
            Type = request.Type,
            Status = ReservationStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        // If storing the booking fails after the place was taken, the place
        // must be handed back or the slot would leak capacity for ever.
        try
        {
            await _reservations.InsertAsync(reservation, cancellationToken);
        }
        catch
        {
            await _slots.ReleasePlaceAsync(claimed.Id, CancellationToken.None);
            throw;
        }

        _logger.LogInformation(
            "Reservation {No} created for prosumer {Nic}.", reservation.ReservationNo, prosumerNic);

        return BuildSummary(
            "Created",
            $"Booking {reservation.ReservationNo} has been requested and is awaiting approval.",
            reservation, prosumer.FullName, station.Name);
    }

    /// <summary>
    /// Changes an existing booking, moving it to another window if asked.
    /// </summary>
    public async Task<ReservationSummaryResponse> UpdateAsync(
        string id,
        UpdateReservationRequest request,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var reservation = await GetRequiredAsync(id, cancellationToken);

        EnsureCallerMayAct(reservation, caller);
        EnsureTypeValid(request.Type);
        EnsureStillOpen(reservation);

        // Rule: twelve hours notice, measured against the booking as it stands
        // now, so a prosumer cannot escape the rule by editing at the last
        // moment and moving to a later window.
        EnsureChangeNoticeGiven(reservation.ReservationStartUtc, now, "changed");

        var movingSlot = !string.Equals(request.SlotId, reservation.SlotId, StringComparison.Ordinal);
        var previousSlotId = reservation.SlotId;

        var station = await _stations.GetByIdAsync(reservation.StationId, cancellationToken);

        if (movingSlot)
        {
            var newSlot = await GetRequiredSlotAsync(request.SlotId, cancellationToken);

            if (!newSlot.IsActive)
            {
                throw new BusinessRuleViolationException(
                    ErrorCodes.SlotInactive, "The chosen booking window is no longer available.");
            }

            station = await _stations.GetByIdAsync(newSlot.StationId, cancellationToken)
                ?? throw new NotFoundException("The station for this booking window no longer exists.");

            if (!station.IsActive)
            {
                throw new BusinessRuleViolationException(
                    ErrorCodes.StationInactive, "That station is not currently in service.");
            }

            // The replacement window must itself satisfy the seven day rule.
            EnsureWithinBookingHorizon(newSlot.StartTimeUtc, now);

            if (await _reservations.ExistsActiveForProsumerAndSlotAsync(
                    reservation.ProsumerNic, newSlot.Id, reservation.Id, cancellationToken))
            {
                throw new ConflictException(
                    ErrorCodes.DuplicateReservation,
                    "You already hold a booking for that window.");
            }

            // Take the new place before giving up the old one, so a failure
            // never leaves the prosumer holding no booking at all.
            var claimed = await _slots.TryClaimPlaceAsync(newSlot.Id, cancellationToken)
                ?? throw new ConflictException(
                    ErrorCodes.SlotFull, "That booking window is now full.");

            reservation.SlotId = claimed.Id;
            reservation.StationId = station.Id;
            reservation.ReservationStartUtc = claimed.StartTimeUtc;
            reservation.ReservationEndUtc = claimed.EndTimeUtc;
            reservation.EnergyKwh = claimed.EnergyKwhPerSlot;
        }

        reservation.Type = request.Type;
        reservation.UpdatedAtUtc = now;

        // A changed booking is a materially different obligation for the
        // station, so it returns to Pending for re-approval and any QR token
        // already issued is discarded rather than left valid for the old plan.
        reservation.Status = ReservationStatus.Pending;
        reservation.QrToken = null;
        reservation.QrIssuedAtUtc = null;

        await _reservations.ReplaceAsync(reservation, cancellationToken);

        // Only once the change is safely stored is the old place released.
        if (movingSlot)
        {
            await _slots.ReleasePlaceAsync(previousSlotId, cancellationToken);
        }

        _logger.LogInformation("Reservation {No} updated.", reservation.ReservationNo);

        return BuildSummary(
            "Updated",
            $"Booking {reservation.ReservationNo} has been updated and is awaiting approval again.",
            reservation, stationName: station?.Name);
    }

    /// <summary>
    /// Cancels a booking and returns its place to the slot.
    /// </summary>
    public async Task<ReservationSummaryResponse> CancelAsync(
        string id, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var reservation = await GetRequiredAsync(id, cancellationToken);

        EnsureCallerMayAct(reservation, caller);
        EnsureStillOpen(reservation);

        // Rule: twelve hours notice before the window starts.
        EnsureChangeNoticeGiven(reservation.ReservationStartUtc, now, "cancelled");

        // The status guard lives inside the update, so a booking cancelled by
        // somebody else a moment earlier is reported rather than cancelled
        // twice and its slot place released twice.
        var cancelled = await _reservations.TryCancelAsync(reservation.Id, now, cancellationToken)
            ?? throw new BusinessRuleViolationException(
                ErrorCodes.ReservationAlreadyClosed,
                "This booking has already been closed and cannot be cancelled.");

        await _slots.ReleasePlaceAsync(cancelled.SlotId, cancellationToken);

        _logger.LogInformation("Reservation {No} cancelled.", cancelled.ReservationNo);

        var station = await _stations.GetByIdAsync(cancelled.StationId, cancellationToken);

        return BuildSummary(
            "Cancelled",
            $"Booking {cancelled.ReservationNo} has been cancelled.",
            cancelled, stationName: station?.Name);
    }

    /// <summary>
    /// Approves a pending booking and issues its transaction QR token.
    /// </summary>
    public async Task<ReservationResponse> ApproveAsync(
        string id, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var reservation = await GetRequiredAsync(id, cancellationToken);

        if (reservation.Status != ReservationStatus.Pending)
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.ReservationNotPending,
                $"Only a pending booking can be approved; this one is {reservation.Status}.");
        }

        // The token is signed with a server only secret, so the QR code the
        // prosumer displays cannot be forged or edited on the device.
        var token = _qrTokens.Issue(reservation.Id, reservation.ReservationStartUtc);

        var approved = await _reservations.TryApproveAsync(
                reservation.Id, token, now, cancellationToken)
            ?? throw new BusinessRuleViolationException(
                ErrorCodes.ReservationNotPending,
                "This booking is no longer pending and could not be approved.");

        _logger.LogInformation("Reservation {No} approved.", approved.ReservationNo);

        return await BuildResponseAsync(approved, cancellationToken);
    }

    /// <summary>
    /// Rejects a pending booking and returns its place to the slot.
    /// </summary>
    public async Task<ReservationResponse> RejectAsync(
        string id, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var reservation = await GetRequiredAsync(id, cancellationToken);

        var rejected = await _reservations.TryRejectAsync(reservation.Id, now, cancellationToken)
            ?? throw new BusinessRuleViolationException(
                ErrorCodes.ReservationNotPending,
                $"Only a pending booking can be rejected; this one is {reservation.Status}.");

        // A rejected booking no longer occupies the window.
        await _slots.ReleasePlaceAsync(rejected.SlotId, cancellationToken);

        _logger.LogInformation("Reservation {No} rejected.", rejected.ReservationNo);

        return await BuildResponseAsync(rejected, cancellationToken);
    }

    /// <summary>
    /// Returns one booking, subject to the ownership rules.
    /// </summary>
    public async Task<ReservationResponse> GetByIdAsync(
        string id, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var reservation = await GetRequiredAsync(id, cancellationToken);

        EnsureCallerMayAct(reservation, caller);

        return await BuildResponseAsync(reservation, cancellationToken);
    }

    /// <summary>
    /// Searches bookings, restricting a prosumer to their own records.
    /// </summary>
    public async Task<IReadOnlyList<ReservationResponse>> SearchAsync(
        ReservationQuery query, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // A prosumer cannot widen the search to other people by supplying a
        // different NIC: their own is forced in regardless of what was sent.
        if (!caller.IsStaff)
        {
            query.ProsumerNic = caller.UserId;
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && !ReservationStatus.All.Contains(query.Status))
        {
            throw new ValidationException($"Unknown reservation status '{query.Status}'.");
        }

        var reservations = await _reservations.SearchAsync(query, cancellationToken);

        // The station names are resolved in one query rather than one per row.
        var stationNames = await LoadStationNamesAsync(reservations, cancellationToken);

        return reservations.ToResponseList(stationNames: stationNames);
    }

    /// <summary>
    /// Returns the QR payload for an approved booking.
    /// </summary>
    public async Task<QrCodeResponse> GetQrCodeAsync(
        string id, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var reservation = await GetRequiredAsync(id, cancellationToken);

        EnsureCallerMayAct(reservation, caller);

        // A QR code only exists once the booking has been approved.
        if (reservation.Status != ReservationStatus.Approved
            || string.IsNullOrWhiteSpace(reservation.QrToken))
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.ReservationNotApproved,
                "A transaction QR code is only available once the booking has been approved.");
        }

        return new QrCodeResponse(
            reservation.Id,
            reservation.ReservationNo,
            reservation.QrToken,
            reservation.QrIssuedAtUtc ?? reservation.UpdatedAtUtc,
            reservation.ReservationStartUtc);
    }

    /// <summary>
    /// Verifies a scanned QR token and returns the booking it identifies.
    /// </summary>
    public async Task<ReservationResponse> VerifyQrAsync(
        string token, CancellationToken cancellationToken = default)
    {
        // First check the signature. A token we did not issue is rejected here
        // without any database work at all.
        var reservationId = _qrTokens.Verify(token)
            ?? throw new BusinessRuleViolationException(
                ErrorCodes.QrInvalid, "This QR code is not valid.");

        var reservation = await _reservations.GetByIdAsync(reservationId, cancellationToken)
            ?? throw new BusinessRuleViolationException(
                ErrorCodes.QrInvalid, "This QR code does not match any booking.");

        // The signature proves the token was issued by us, but not that it is
        // still the current one. Comparing against the stored token rejects a
        // code that was superseded when the booking was changed or cancelled.
        if (!string.Equals(reservation.QrToken, token, StringComparison.Ordinal))
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.QrInvalid,
                "This QR code is no longer valid because the booking has changed.");
        }

        if (reservation.Status == ReservationStatus.Completed)
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.QrAlreadyUsed,
                "This booking has already been completed.");
        }

        if (reservation.Status != ReservationStatus.Approved)
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.ReservationNotApproved,
                $"This booking is {reservation.Status} and cannot be processed.");
        }

        return await BuildResponseAsync(reservation, cancellationToken);
    }

    /// <summary>
    /// Finalises the energy transfer after a successful scan.
    /// </summary>
    public async Task<ReservationSummaryResponse> CompleteAsync(
        string id, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var reservation = await GetRequiredAsync(id, cancellationToken);

        // Requiring Approved inside the update is what makes the QR single
        // use: the second scan finds nothing to update and is refused.
        var completed = await _reservations.TryCompleteAsync(
                reservation.Id, caller.UserId, now, cancellationToken)
            ?? throw new BusinessRuleViolationException(
                reservation.Status == ReservationStatus.Completed
                    ? ErrorCodes.QrAlreadyUsed
                    : ErrorCodes.ReservationNotApproved,
                reservation.Status == ReservationStatus.Completed
                    ? "This booking has already been completed."
                    : $"This booking is {reservation.Status} and cannot be completed.");

        _logger.LogInformation(
            "Reservation {No} completed by operator {OperatorId}.",
            completed.ReservationNo, caller.UserId);

        var station = await _stations.GetByIdAsync(completed.StationId, cancellationToken);

        return BuildSummary(
            "Completed",
            $"Energy transfer for booking {completed.ReservationNo} has been completed.",
            completed, stationName: station?.Name);
    }

    /// <summary>
    /// Rule: a booking must start in the future and no more than seven days
    /// from now. Both halves are checked here so the message explains which
    /// half was broken.
    /// </summary>
    private static void EnsureWithinBookingHorizon(DateTime slotStartUtc, DateTime nowUtc)
    {
        if (slotStartUtc <= nowUtc)
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.ReservationInPast,
                "This booking window has already started and can no longer be reserved.");
        }

        var latestAllowed = nowUtc.AddDays(BusinessRules.MaxBookingHorizonDays);

        if (slotStartUtc > latestAllowed)
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.ReservationOutside7Days,
                $"Bookings can only be made up to {BusinessRules.MaxBookingHorizonDays} days in advance.");
        }
    }

    /// <summary>
    /// Rule: changes and cancellations need at least twelve hours notice
    /// before the booking is due to start.
    /// </summary>
    private static void EnsureChangeNoticeGiven(
        DateTime reservationStartUtc, DateTime nowUtc, string action)
    {
        var deadline = reservationStartUtc.AddHours(-BusinessRules.MinChangeNoticeHours);

        if (nowUtc >= deadline)
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.ChangeWindowExpired,
                $"A booking can only be {action} at least " +
                $"{BusinessRules.MinChangeNoticeHours} hours before it starts.");
        }
    }

    /// <summary>
    /// Confirms a booking is still open, that is Pending or Approved.
    /// </summary>
    private static void EnsureStillOpen(EnergyReservation reservation)
    {
        if (!ReservationStatus.Active.Contains(reservation.Status))
        {
            throw new BusinessRuleViolationException(
                ErrorCodes.ReservationAlreadyClosed,
                $"This booking is {reservation.Status} and can no longer be changed.");
        }
    }

    /// <summary>
    /// Confirms the reservation type is one the system recognises.
    /// </summary>
    private static void EnsureTypeValid(string type)
    {
        if (!ReservationType.All.Contains(type))
        {
            throw new ValidationException(
                $"Reservation type must be either {ReservationType.Injection} " +
                $"or {ReservationType.Withdrawal}.");
        }
    }

    /// <summary>
    /// Confirms the caller either owns the booking or is a member of staff.
    /// </summary>
    private static void EnsureCallerMayAct(EnergyReservation reservation, CallerContext caller)
    {
        if (caller.IsStaff)
        {
            return;
        }

        if (!string.Equals(reservation.ProsumerNic, caller.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("You may only act on your own bookings.");
        }
    }

    /// <summary>
    /// Builds the human readable booking reference from the generated
    /// identifier, which is already unique, so no counter collection is needed.
    /// </summary>
    private static string BuildReservationNo(ObjectId id, DateTime nowUtc)
    {
        var suffix = id.ToString()[^6..].ToUpperInvariant();
        return $"RS-{nowUtc:yyyyMMdd}-{suffix}";
    }

    /// <summary>
    /// Wraps a reservation in the summary the clients show after each action.
    /// </summary>
    private static ReservationSummaryResponse BuildSummary(
        string action,
        string message,
        EnergyReservation reservation,
        string? prosumerName = null,
        string? stationName = null)
    {
        return new ReservationSummaryResponse(
            action, message, reservation.ToResponse(prosumerName, stationName));
    }

    /// <summary>
    /// Builds a single response, resolving the station and prosumer names.
    /// </summary>
    private async Task<ReservationResponse> BuildResponseAsync(
        EnergyReservation reservation, CancellationToken cancellationToken)
    {
        var station = await _stations.GetByIdAsync(reservation.StationId, cancellationToken);
        var prosumer = await _users.GetByIdAsync(reservation.ProsumerNic, cancellationToken);

        return reservation.ToResponse(prosumer?.FullName, station?.Name);
    }

    /// <summary>
    /// Loads the names of every station referenced by a list of bookings in a
    /// single query, avoiding one database call per row.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> LoadStationNamesAsync(
        IReadOnlyList<EnergyReservation> reservations, CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>();

        var stationIds = reservations
            .Select(r => r.StationId)
            .Where(sid => !string.IsNullOrWhiteSpace(sid))
            .Distinct()
            .ToList();

        // The station list is small in this system, so fetching them all and
        // filtering in memory is cheaper than one query per identifier.
        if (stationIds.Count == 0)
        {
            return names;
        }

        var stations = await _stations.ListAsync(cancellationToken: cancellationToken);

        foreach (var station in stations.Where(s => stationIds.Contains(s.Id)))
        {
            names[station.Id] = station.Name;
        }

        return names;
    }

    /// <summary>
    /// Loads a reservation and throws a not found error when it is missing.
    /// </summary>
    private async Task<EnergyReservation> GetRequiredAsync(
        string id, CancellationToken cancellationToken)
    {
        return await _reservations.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"No booking was found with identifier '{id}'.");
    }

    /// <summary>
    /// Loads a booking window and throws a not found error when it is missing.
    /// </summary>
    private async Task<EnergyBookingSlot> GetRequiredSlotAsync(
        string slotId, CancellationToken cancellationToken)
    {
        return await _slots.GetByIdAsync(slotId, cancellationToken)
            ?? throw new NotFoundException($"No booking window was found with identifier '{slotId}'.");
    }
}
