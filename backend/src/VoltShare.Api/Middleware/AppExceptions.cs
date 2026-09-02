// -----------------------------------------------------------------------------
// File        : AppExceptions.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Middleware
// Description : Application specific exception types and the stable error codes
//               that accompany them. Services throw these instead of returning
//               status codes, which keeps HTTP concerns out of the business
//               logic while still producing precise responses.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

namespace VoltShare.Api.Middleware;

/// <summary>
/// Machine readable error codes returned to the web and Android clients.
/// The clients switch on these codes rather than on message text, so wording
/// can change without breaking either application.
/// </summary>
public static class ErrorCodes
{
    // Generic
    public const string NotFound = "NOT_FOUND";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unexpected = "UNEXPECTED_ERROR";

    // Authentication and accounts
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountInactive = "ACCOUNT_INACTIVE";
    public const string EmailAlreadyUsed = "EMAIL_ALREADY_USED";
    public const string NicAlreadyRegistered = "NIC_ALREADY_REGISTERED";
    public const string RoleNotAllowed = "ROLE_NOT_ALLOWED";

    // Stations and slots
    public const string StationCodeAlreadyUsed = "STATION_CODE_ALREADY_USED";
    public const string StationInactive = "STATION_INACTIVE";
    public const string StationHasActiveReservations = "STATION_HAS_ACTIVE_RESERVATIONS";
    public const string SlotInactive = "SLOT_INACTIVE";
    public const string SlotHasBookings = "SLOT_HAS_BOOKINGS";
    public const string SlotFull = "SLOT_FULL";

    // Reservations
    public const string ReservationInPast = "RESERVATION_IN_PAST";
    public const string ReservationOutside7Days = "RESERVATION_OUTSIDE_7_DAYS";
    public const string ChangeWindowExpired = "CHANGE_WINDOW_EXPIRED";
    public const string ReservationNotPending = "RESERVATION_NOT_PENDING";
    public const string ReservationNotApproved = "RESERVATION_NOT_APPROVED";
    public const string ReservationAlreadyClosed = "RESERVATION_ALREADY_CLOSED";
    public const string DuplicateReservation = "DUPLICATE_RESERVATION";

    // QR verification
    public const string QrInvalid = "QR_INVALID";
    public const string QrAlreadyUsed = "QR_ALREADY_USED";
}

/// <summary>
/// Base class for every error the application raises deliberately. Carrying a
/// code on the exception lets the middleware build a consistent response.
/// </summary>
public abstract class AppException : Exception
{
    /// <summary>
    /// Creates the exception with a stable error code and a readable message.
    /// </summary>
    protected AppException(string code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// The stable machine readable code sent to the client.
    /// </summary>
    public string Code { get; }
}

/// <summary>
/// Raised when a requested document does not exist. Mapped to HTTP 404.
/// </summary>
public class NotFoundException : AppException
{
    /// <summary>
    /// Creates a not found error describing which entity was missing.
    /// </summary>
    public NotFoundException(string message, string code = ErrorCodes.NotFound)
        : base(code, message)
    {
    }
}

/// <summary>
/// Raised when input fails validation before any rule is evaluated.
/// Mapped to HTTP 400.
/// </summary>
public class ValidationException : AppException
{
    /// <summary>
    /// Creates a validation error describing what was wrong with the input.
    /// </summary>
    public ValidationException(string message, string code = ErrorCodes.ValidationFailed)
        : base(code, message)
    {
    }
}

/// <summary>
/// Raised when a request is well formed but breaks a business rule, such as
/// booking more than seven days ahead. Mapped to HTTP 422.
/// </summary>
public class BusinessRuleViolationException : AppException
{
    /// <summary>
    /// Creates a business rule error carrying the rule specific code.
    /// </summary>
    public BusinessRuleViolationException(string code, string message)
        : base(code, message)
    {
    }
}

/// <summary>
/// Raised when a request clashes with the current state of the data, such as
/// a duplicate email or a slot that filled up. Mapped to HTTP 409.
/// </summary>
public class ConflictException : AppException
{
    /// <summary>
    /// Creates a conflict error carrying the conflict specific code.
    /// </summary>
    public ConflictException(string code, string message)
        : base(code, message)
    {
    }
}

/// <summary>
/// Raised when an authenticated user tries to act on something that is not
/// theirs. Mapped to HTTP 403.
/// </summary>
public class ForbiddenException : AppException
{
    /// <summary>
    /// Creates a forbidden error explaining why the action is not permitted.
    /// </summary>
    public ForbiddenException(string message, string code = ErrorCodes.RoleNotAllowed)
        : base(code, message)
    {
    }
}
