// -----------------------------------------------------------------------------
// File        : StationDtos.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Dtos
// Description : Request and response contracts for solar microgrid node
//               endpoints. Latitude and longitude are exposed as plain numbers
//               so that neither client has to understand the GeoJSON layout
//               used for storage.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace VoltShare.Api.Dtos;

/// <summary>
/// Daily operating window of a station.
/// </summary>
public record OperatingHoursDto(string OpenTime, string CloseTime);

/// <summary>
/// A solar microgrid node as returned to the clients.
/// </summary>
public record StationResponse(
    string Id,
    string Code,
    string Name,
    string AddressLine,
    string City,
    double Latitude,
    double Longitude,
    double CapacityKwh,
    int TotalBatterySlots,
    int AvailableBatterySlots,
    OperatingHoursDto OperatingHours,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// A station together with how far it is from the point that was searched.
/// The distance is calculated by MongoDB, not by the Android application, so
/// no geographic logic lives in the client.
/// </summary>
public record NearbyStationResponse(
    StationResponse Station,
    double DistanceMeters);

/// <summary>
/// Fields required to create a new station.
/// </summary>
public class CreateStationRequest
{
    [Required(ErrorMessage = "Station code is required.")]
    [StringLength(30, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Station name is required.")]
    [StringLength(120, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Address is required.")]
    public string AddressLine { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required.")]
    public string City { get; set; } = string.Empty;

    // Latitude is bounded to the real world range so an obviously wrong value
    // is rejected before it reaches the database and breaks the map.
    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double Longitude { get; set; }

    [Range(0.1, 100000, ErrorMessage = "Capacity must be greater than zero.")]
    public double CapacityKwh { get; set; }

    [Range(0, 10000, ErrorMessage = "Total battery slots cannot be negative.")]
    public int TotalBatterySlots { get; set; }

    // When omitted the station starts with every battery slot free.
    [Range(0, 10000, ErrorMessage = "Available battery slots cannot be negative.")]
    public int? AvailableBatterySlots { get; set; }

    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Open time must be in HH:mm format.")]
    public string OpenTime { get; set; } = "06:00";

    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Close time must be in HH:mm format.")]
    public string CloseTime { get; set; } = "20:00";
}

/// <summary>
/// Fields that may be changed on an existing station. The code is deliberately
/// not editable because it is the reference other records are known by.
/// </summary>
public class UpdateStationRequest
{
    [Required(ErrorMessage = "Station name is required.")]
    [StringLength(120, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Address is required.")]
    public string AddressLine { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required.")]
    public string City { get; set; } = string.Empty;

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double Longitude { get; set; }

    [Range(0.1, 100000, ErrorMessage = "Capacity must be greater than zero.")]
    public double CapacityKwh { get; set; }

    [Range(0, 10000, ErrorMessage = "Total battery slots cannot be negative.")]
    public int TotalBatterySlots { get; set; }

    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Open time must be in HH:mm format.")]
    public string OpenTime { get; set; } = "06:00";

    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Close time must be in HH:mm format.")]
    public string CloseTime { get; set; } = "20:00";
}

/// <summary>
/// Battery slot availability update, used by grid operators from either client
/// as they bring storage in and out of service.
/// </summary>
public class UpdateBatterySlotsRequest
{
    [Range(0, 10000, ErrorMessage = "Available battery slots cannot be negative.")]
    public int AvailableBatterySlots { get; set; }
}
