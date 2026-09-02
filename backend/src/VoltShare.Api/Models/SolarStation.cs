// -----------------------------------------------------------------------------
// File        : SolarStation.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Models
// Description : MongoDB document for the "solarStationInfo" collection,
//               describing a physical solar microgrid node including its
//               geographic position, capacity and battery slot inventory.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace VoltShare.Api.Models;

/// <summary>
/// Daily opening and closing time of a station, stored as "HH:mm" strings
/// because they describe a repeating wall-clock schedule rather than a
/// single instant in time.
/// </summary>
public class OperatingHours
{
    [BsonElement("openTime")]
    public string OpenTime { get; set; } = "06:00";

    [BsonElement("closeTime")]
    public string CloseTime { get; set; } = "20:00";
}

/// <summary>
/// A solar microgrid node (hub) that prosumers trade energy with.
/// </summary>
public class SolarStation
{
    // Generated primary key, exposed to clients as a 24 character hex string.
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    // Short human readable reference such as SS-COL-001. Uniquely indexed.
    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    // Display name of the station.
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    // Street address of the station.
    [BsonElement("addressLine")]
    public string AddressLine { get; set; } = string.Empty;

    // City, used by the web application as a list filter.
    [BsonElement("city")]
    public string City { get; set; } = string.Empty;

    // GeoJSON point holding the station position. A 2dsphere index on this
    // field lets the server answer "which stations are near me" with a $near
    // query, so the Android map never has to calculate distances itself and
    // no geographic logic leaks into the client.
    //
    // Note carefully: GeoJSON stores coordinates as [longitude, latitude],
    // which is the reverse of the order people normally say them aloud.
    [BsonElement("location")]
    public GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; set; } = default!;

    // Rated generation and transfer capacity of the node, in kWh.
    [BsonElement("capacityKwh")]
    public double CapacityKwh { get; set; }

    // Total number of battery storage slots physically installed at the node.
    [BsonElement("totalBatterySlots")]
    public int TotalBatterySlots { get; set; }

    // Battery storage slots currently free. Maintained by grid operators.
    [BsonElement("availableBatterySlots")]
    public int AvailableBatterySlots { get; set; }

    // Daily operating window of the node.
    [BsonElement("operatingHours")]
    public OperatingHours OperatingHours { get; set; } = new();

    // Deactivation is blocked by the service layer while the station still
    // holds active reservations in the future.
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    // Audit timestamps. Always stored in UTC.
    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }
}
