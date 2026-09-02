// -----------------------------------------------------------------------------
// File        : IAccountService.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Services
// Description : Contract for all authentication and account management
//               business logic. Every rule about who may register, who may be
//               activated and who may sign in is implemented behind this
//               interface, never in a controller or in a client.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Dtos;

namespace VoltShare.Api.Services;

/// <summary>
/// Authentication and account lifecycle operations.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Verifies credentials and issues an access token.
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a solar prosumer from the mobile application. The account is
    /// created inactive and must be activated by a back-office officer.
    /// </summary>
    Task<UserResponse> RegisterProsumerAsync(
        RegisterProsumerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the profile of the currently signed in account.
    /// </summary>
    Task<UserResponse> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists accounts with optional role, status and search filters.
    /// </summary>
    Task<IReadOnlyList<UserResponse>> ListAsync(
        string? role = null,
        bool? isActive = null,
        bool? deactivationRequested = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Backoffice or GridOperator account.
    /// </summary>
    Task<UserResponse> CreateStaffUserAsync(
        CreateStaffUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a prosumer account on behalf of a back-office officer.
    /// </summary>
    Task<UserResponse> CreateProsumerAsync(
        CreateProsumerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the editable profile fields of an account.
    /// </summary>
    Task<UserResponse> UpdateAsync(
        string id, UpdateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates an account. Restricted to back-office officers at the
    /// controller, which is how the reactivation rule is enforced.
    /// </summary>
    Task<UserResponse> ActivateAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates an account.
    /// </summary>
    Task<UserResponse> DeactivateAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a prosumer request to close their own account. The account stays
    /// active until a back-office officer acts on the request.
    /// </summary>
    Task<UserResponse> RequestDeactivationAsync(string id, CancellationToken cancellationToken = default);
}
