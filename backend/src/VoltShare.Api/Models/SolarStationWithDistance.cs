// -----------------------------------------------------------------------------
// File        : SolarStationWithDistance.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Models
// Description : A station carrying the distance calculated by the MongoDB
//               $geoNear aggregation stage. Used only as the result shape of
//               the nearby stations query.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using MongoDB.Bson.Serialization.Attributes;

namespace VoltShare.Api.Models;

/// <summary>
/// A solar station plus how far it is from the searched point, in metres.
///
/// The distance is produced by the database itself through $geoNear, which
/// keeps the distance calculation on the server where the specification
/// requires the business logic to live.
/// </summary>
public class SolarStationWithDistance : SolarStation
{
    // Populated by the "distanceField" setting of the $geoNear stage.
    [BsonElement("distanceMeters")]
    public double DistanceMeters { get; set; }
}
