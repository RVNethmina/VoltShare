// -----------------------------------------------------------------------------
// File        : User.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Models
// Description : MongoDB document for the "users" collection. Stores both web
//               application staff (Backoffice, GridOperator) and mobile
//               application solar prosumers in a single collection.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace VoltShare.Api.Models;

/// <summary>
/// A single account in the system.
///
/// The document identifier is deliberately declared as a string: for a
/// prosumer it holds the National Identity Card number, which makes the NIC
/// the literal primary key demanded by the specification and gives the
/// fastest possible lookup by NIC. Staff accounts store a generated
/// identifier in the same field, so one strongly typed class serves both.
/// </summary>
public class User
{
    // Primary key. NIC for a prosumer, generated identifier for staff.
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    // Full name shown in listings and on the mobile dashboard.
    [BsonElement("fullName")]
    public string FullName { get; set; } = string.Empty;

    // Login identifier. A unique index is created on this field at start-up.
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    // Contact number, optional.
    [BsonElement("phone")]
    public string? Phone { get; set; }

    // Postal address of the property carrying the solar array, optional.
    [BsonElement("address")]
    public string? Address { get; set; }

    // BCrypt hash of the password. The plain password is never stored.
    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    // One of the values defined in UserRoles.
    [BsonElement("role")]
    public string Role { get; set; } = UserRoles.Prosumer;

    // False for a self-registered prosumer until a back-office officer
    // activates the account, and false again once the account is deactivated.
    [BsonElement("isActive")]
    public bool IsActive { get; set; }

    // Raised by a prosumer from the mobile app when they ask for their account
    // to be closed. Only a back-office officer may act on it, which is how the
    // "reactivation by back-office only" rule is enforced.
    [BsonElement("deactivationRequested")]
    public bool DeactivationRequested { get; set; }

    // Audit timestamps. Always stored in UTC and converted at the client edge.
    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }
}
