// -----------------------------------------------------------------------------
// File        : EnergyBookingSlot.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Models
// Description : MongoDB document for the "energyBookingSlots" collection. Each
//               document is one bookable time window at one solar station,
//               together with how many prosumers may book it.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace VoltShare.Api.Models;

/// <summary>
/// A bookable energy transfer window offered by a solar station.
///
/// Capacity is tracked with a simple counter rather than a list of bookings so
/// that a reservation can claim a place using a single atomic MongoDB update,
/// which prevents two prosumers taking the last place at the same instant.
/// </summary>
public class EnergyBookingSlot
{
    // Generated primary key, exposed to clients as a 24 character hex string.
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    // Owning solar station. Part of a unique compound index with StartTimeUtc.
    [BsonElement("stationId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string StationId { get; set; } = string.Empty;

    // Start of the transfer window, in UTC.
    [BsonElement("startTimeUtc")]
    public DateTime StartTimeUtc { get; set; }

    // End of the transfer window, in UTC.
    [BsonElement("endTimeUtc")]
    public DateTime EndTimeUtc { get; set; }

    // How many prosumers may book this window in total.
    [BsonElement("capacity")]
    public int Capacity { get; set; }

    // How many places have been taken so far. Incremented and decremented
    // atomically by the reservation service, never by a client.
    [BsonElement("bookedCount")]
    public int BookedCount { get; set; }

    // Energy volume, in kWh, allotted to each booking of this window.
    [BsonElement("energyKwhPerSlot")]
    public double EnergyKwhPerSlot { get; set; }

    // Allows staff to withdraw a window from sale without deleting history.
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    // Audit timestamps. Always stored in UTC.
    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }
}
