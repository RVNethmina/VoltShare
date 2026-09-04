// -----------------------------------------------------------------------------
// File        : EnergyReservation.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Models
// Description : MongoDB document for the "energyReservations" collection. Each
//               document is one prosumer booking of one energy transfer slot,
//               through its whole lifecycle from Pending to Completed.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace VoltShare.Api.Models;

/// <summary>
/// A power trading reservation made by a solar prosumer.
/// </summary>
public class EnergyReservation
{
    // Generated primary key, exposed to clients as a 24 character hex string.
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    // Friendly reference such as RS-20260915-0042, shown to the prosumer and
    // printed on the booking summary screen. Uniquely indexed.
    [BsonElement("reservationNo")]
    public string ReservationNo { get; set; } = string.Empty;

    // NIC of the prosumer who owns this reservation. References User.Id.
    [BsonElement("prosumerNic")]
    public string ProsumerNic { get; set; } = string.Empty;

    // Station the energy is traded with. References SolarStation.Id.
    [BsonElement("stationId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string StationId { get; set; } = string.Empty;

    // Booking window claimed. References EnergyBookingSlot.Id.
    [BsonElement("slotId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string SlotId { get; set; } = string.Empty;

    // Start and end of the window, copied from the slot when the reservation
    // is made. Denormalised on purpose: the 7 day and 12 hour rules and every
    // dashboard count are range queries on these fields, and copying them
    // avoids a lookup into the slots collection on every single read.
    [BsonElement("reservationStartUtc")]
    public DateTime ReservationStartUtc { get; set; }

    [BsonElement("reservationEndUtc")]
    public DateTime ReservationEndUtc { get; set; }

    // Energy volume reserved, in kWh.
    [BsonElement("energyKwh")]
    public double EnergyKwh { get; set; }

    // One of the values defined in ReservationType.
    [BsonElement("type")]
    public string Type { get; set; } = ReservationType.Injection;

    // One of the values defined in ReservationStatus.
    [BsonElement("status")]
    public string Status { get; set; } = ReservationStatus.Pending;

    // Signed, opaque token issued when the reservation is approved. The mobile
    // app renders this string as a QR code; the operator scans it and the
    // server verifies the signature. Deliberately carries no personal data.
    // Omitted from the document entirely while no token has been issued.
    // Without this the driver would store an explicit null, and several
    // documents all holding null would collide on the unique index.
    [BsonElement("qrToken")]
    [BsonIgnoreIfNull]
    public string? QrToken { get; set; }

    [BsonElement("qrIssuedAtUtc")]
    public DateTime? QrIssuedAtUtc { get; set; }

    // Audit trail of the reservation lifecycle. Always stored in UTC.
    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }

    [BsonElement("cancelledAtUtc")]
    public DateTime? CancelledAtUtc { get; set; }

    [BsonElement("completedAtUtc")]
    public DateTime? CompletedAtUtc { get; set; }

    // Identifier of the grid operator who finalised the energy transfer.
    [BsonElement("completedByOperatorId")]
    public string? CompletedByOperatorId { get; set; }
}
