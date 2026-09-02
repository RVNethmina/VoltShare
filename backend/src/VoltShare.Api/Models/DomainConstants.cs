// -----------------------------------------------------------------------------
// File        : DomainConstants.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Models
// Description : Central definition of the fixed vocabulary used across the
//               system: user roles, authorisation policies, reservation
//               statuses, reservation types and the business rule thresholds
//               mandated by the assignment specification.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

namespace VoltShare.Api.Models;

/// <summary>
/// The three roles recognised by the system. Backoffice and GridOperator are
/// web application users; Prosumer accounts are created from the mobile app.
/// Stored as plain strings so the documents stay readable in MongoDB Compass.
/// </summary>
public static class UserRoles
{
    public const string Backoffice = "Backoffice";
    public const string GridOperator = "GridOperator";
    public const string Prosumer = "Prosumer";

    // Used to validate a role value supplied by a client.
    public static readonly string[] All = { Backoffice, GridOperator, Prosumer };
}

/// <summary>
/// Authorisation policy names registered in Program.cs and applied to
/// controllers through the [Authorize(Policy = ...)] attribute.
/// </summary>
public static class Policies
{
    // Only back-office administrators.
    public const string Backoffice = "BackofficePolicy";

    // Only grid operators.
    public const string GridOperator = "GridOperatorPolicy";

    // Only solar prosumers.
    public const string Prosumer = "ProsumerPolicy";

    // Any member of staff, that is back-office or grid operator.
    public const string Staff = "StaffPolicy";
}

/// <summary>
/// Lifecycle states of an energy reservation.
/// </summary>
public static class ReservationStatus
{
    // Created by a prosumer, awaiting operator approval.
    public const string Pending = "Pending";

    // Approved by staff; a transaction QR token has been issued.
    public const string Approved = "Approved";

    // Withdrawn by the prosumer or by staff before the slot started.
    public const string Cancelled = "Cancelled";

    // Refused by staff.
    public const string Rejected = "Rejected";

    // Energy transfer finalised by an operator after scanning the QR code.
    public const string Completed = "Completed";

    // Statuses that still occupy capacity on a booking slot. A station cannot
    // be deactivated while reservations in these states exist in the future.
    public static readonly string[] Active = { Pending, Approved };

    // Used to validate a status filter supplied by a client.
    public static readonly string[] All = { Pending, Approved, Cancelled, Rejected, Completed };
}

/// <summary>
/// Direction of the energy transfer being reserved.
/// </summary>
public static class ReservationType
{
    // Prosumer delivers surplus solar energy into the microgrid.
    public const string Injection = "Injection";

    // Prosumer draws stored energy out of the microgrid.
    public const string Withdrawal = "Withdrawal";

    // Used to validate a type value supplied by a client.
    public static readonly string[] All = { Injection, Withdrawal };
}

/// <summary>
/// Business rule thresholds taken directly from the assignment specification.
/// Declared in one place so that the rules are easy to locate, to change and
/// to explain, rather than being scattered as magic numbers in the services.
/// </summary>
public static class BusinessRules
{
    // A reservation must start within this many days of the moment it is made.
    public const int MaxBookingHorizonDays = 7;

    // Updates and cancellations must be made at least this many hours before
    // the reservation is due to start.
    public const int MinChangeNoticeHours = 12;
}
