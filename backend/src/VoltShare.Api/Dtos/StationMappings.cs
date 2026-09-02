// -----------------------------------------------------------------------------
// File        : StationMappings.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Dtos
// Description : Conversion between the station and slot storage documents and
//               their client facing contracts, including translation of the
//               GeoJSON point into plain latitude and longitude values.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Models;

namespace VoltShare.Api.Dtos;

/// <summary>
/// Maps station and slot documents onto response contracts.
/// </summary>
public static class StationMappings
{
    /// <summary>
    /// Builds the client facing representation of a station, unpacking the
    /// GeoJSON coordinate array into named latitude and longitude fields.
    /// </summary>
    public static StationResponse ToResponse(this SolarStation station)
    {
        // GeoJSON stores coordinates as [longitude, latitude]. Reading them
        // back in the right order here is what keeps the map markers correct.
        var longitude = station.Location?.Coordinates?.Longitude ?? 0d;
        var latitude = station.Location?.Coordinates?.Latitude ?? 0d;

        return new StationResponse(
            station.Id,
            station.Code,
            station.Name,
            station.AddressLine,
            station.City,
            latitude,
            longitude,
            station.CapacityKwh,
            station.TotalBatterySlots,
            station.AvailableBatterySlots,
            new OperatingHoursDto(station.OperatingHours.OpenTime, station.OperatingHours.CloseTime),
            station.IsActive,
            station.CreatedAtUtc,
            station.UpdatedAtUtc);
    }

    /// <summary>
    /// Maps a whole collection of stations onto response objects.
    /// </summary>
    public static IReadOnlyList<StationResponse> ToResponseList(this IEnumerable<SolarStation> stations)
    {
        return stations.Select(s => s.ToResponse()).ToList();
    }

    /// <summary>
    /// Builds the client facing representation of a booking slot and works out
    /// the remaining capacity so the clients never have to calculate it.
    /// </summary>
    public static SlotResponse ToResponse(this EnergyBookingSlot slot)
    {
        // Clamped at zero so a slot can never report negative availability
        // even if the counters were ever to drift.
        var remaining = Math.Max(0, slot.Capacity - slot.BookedCount);

        return new SlotResponse(
            slot.Id,
            slot.StationId,
            slot.StartTimeUtc,
            slot.EndTimeUtc,
            slot.Capacity,
            slot.BookedCount,
            remaining,
            slot.EnergyKwhPerSlot,
            slot.IsActive);
    }

    /// <summary>
    /// Maps a whole collection of slots onto response objects.
    /// </summary>
    public static IReadOnlyList<SlotResponse> ToResponseList(this IEnumerable<EnergyBookingSlot> slots)
    {
        return slots.Select(s => s.ToResponse()).ToList();
    }
}
