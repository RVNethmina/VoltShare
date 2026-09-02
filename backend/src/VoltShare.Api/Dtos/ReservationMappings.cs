// -----------------------------------------------------------------------------
// File        : ReservationMappings.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Dtos
// Description : Conversion from the reservation storage document to the client
//               contract, including the server side evaluation of whether the
//               booking may still be changed under the twelve hour rule.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Models;

namespace VoltShare.Api.Dtos;

/// <summary>
/// Maps reservation documents onto response contracts.
/// </summary>
public static class ReservationMappings
{
    /// <summary>
    /// Builds the client facing representation of a reservation.
    ///
    /// The prosumer and station names are optional because they come from other
    /// collections; the caller supplies them when it has already loaded them,
    /// which avoids a lookup per row when building a list.
    /// </summary>
    public static ReservationResponse ToResponse(
        this EnergyReservation reservation,
        string? prosumerName = null,
        string? stationName = null,
        DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;

        // A booking can only be changed while it is still open and the twelve
        // hour notice period has not yet been entered. Working this out here
        // means both clients show the same answer without either of them
        // knowing what the rule is.
        var isOpen = reservation.Status == ReservationStatus.Pending
                     || reservation.Status == ReservationStatus.Approved;

        var noticeDeadline = reservation.ReservationStartUtc
            .AddHours(-BusinessRules.MinChangeNoticeHours);

        var withinNotice = now < noticeDeadline;
        var changeable = isOpen && withinNotice;

        return new ReservationResponse(
            reservation.Id,
            reservation.ReservationNo,
            reservation.ProsumerNic,
            prosumerName,
            reservation.StationId,
            stationName,
            reservation.SlotId,
            reservation.ReservationStartUtc,
            reservation.ReservationEndUtc,
            reservation.EnergyKwh,
            reservation.Type,
            reservation.Status,
            CanBeModified: changeable,
            CanBeCancelled: changeable,
            HasQrCode: !string.IsNullOrWhiteSpace(reservation.QrToken)
                       && reservation.Status == ReservationStatus.Approved,
            reservation.CreatedAtUtc,
            reservation.CancelledAtUtc,
            reservation.CompletedAtUtc);
    }

    /// <summary>
    /// Maps a collection of reservations, resolving the display names from
    /// dictionaries the caller has already loaded in one query each.
    /// </summary>
    public static IReadOnlyList<ReservationResponse> ToResponseList(
        this IEnumerable<EnergyReservation> reservations,
        IReadOnlyDictionary<string, string>? prosumerNames = null,
        IReadOnlyDictionary<string, string>? stationNames = null)
    {
        // Evaluate "now" once so every row in the list is judged against the
        // same instant.
        var now = DateTime.UtcNow;

        return reservations.Select(r => r.ToResponse(
            prosumerName: LookUp(prosumerNames, r.ProsumerNic),
            stationName: LookUp(stationNames, r.StationId),
            nowUtc: now)).ToList();
    }

    /// <summary>
    /// Reads a name from an optional dictionary without throwing when the
    /// dictionary or the entry is missing.
    /// </summary>
    private static string? LookUp(IReadOnlyDictionary<string, string>? source, string key)
    {
        if (source is null || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return source.TryGetValue(key, out var value) ? value : null;
    }
}
