// -----------------------------------------------------------------------------
// File        : UserDtos.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Dtos
// Description : Request and response contracts for account related endpoints.
//               These types exist so that MongoDB documents are never exposed
//               directly: the password hash, for example, can never leak into
//               a response because it is not part of any response contract.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace VoltShare.Api.Dtos;

/// <summary>
/// Account details returned to the web and Android clients.
/// </summary>
public record UserResponse(
    string Id,
    string FullName,
    string Email,
    string? Phone,
    string? Address,
    string Role,
    bool IsActive,
    bool DeactivationRequested,
    DateTime CreatedAtUtc);

/// <summary>
/// Credentials supplied at login by either client.
/// </summary>
public class LoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Successful login result: the token plus the profile of the signed in user,
/// which lets the client route straight to the correct home screen.
/// </summary>
public record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserResponse User);

/// <summary>
/// Self service registration submitted from the Android application.
/// The NIC becomes the primary key of the created document.
/// </summary>
public class RegisterProsumerRequest
{
    [Required(ErrorMessage = "NIC is required.")]
    [StringLength(20, MinimumLength = 5, ErrorMessage = "NIC must be between 5 and 20 characters.")]
    public string Nic { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(120, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Phone must be a valid telephone number.")]
    public string? Phone { get; set; }

    public string? Address { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Creation of a Backoffice or GridOperator account by a back-office officer.
/// </summary>
public class CreateStaffUserRequest
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(120, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Phone must be a valid telephone number.")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Role is required.")]
    public string Role { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Creation of a prosumer account by a back-office officer, as opposed to
/// self service registration from the mobile application.
/// </summary>
public class CreateProsumerRequest : RegisterProsumerRequest
{
    // A back-office created prosumer may be activated straight away, because
    // an officer has already checked the details.
    public bool ActivateImmediately { get; set; } = true;
}

/// <summary>
/// Editable profile fields. Role, activation state and password are changed
/// through their own dedicated endpoints so each has its own authorisation.
/// </summary>
public class UpdateUserRequest
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(120, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Phone must be a valid telephone number.")]
    public string? Phone { get; set; }

    public string? Address { get; set; }
}
