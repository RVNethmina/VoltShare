// -----------------------------------------------------------------------------
// File        : AccountService.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Services
// Description : Implements every authentication and account management rule:
//               unique NIC and email, inactive accounts cannot sign in, mobile
//               registrations start inactive awaiting back-office activation,
//               and only a back-office officer may reactivate an account.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using MongoDB.Bson;
using VoltShare.Api.Dtos;
using VoltShare.Api.Middleware;
using VoltShare.Api.Models;
using VoltShare.Api.Repositories;
using VoltShare.Api.Security;

namespace VoltShare.Api.Services;

/// <summary>
/// Central implementation of the account rules for both client applications.
/// </summary>
public class AccountService : IAccountService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AccountService> _logger;

    /// <summary>
    /// Receives its collaborators from dependency injection.
    /// </summary>
    public AccountService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<AccountService> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Verifies credentials and issues an access token carrying the role, so
    /// the client can route the user to the correct home screen.
    /// </summary>
    public async Task<LoginResponse> LoginAsync(
        LoginRequest request, CancellationToken cancellationToken = default)
    {
        // Look the account up by its normalised email address.
        var user = await _users.GetByEmailAsync(request.Email, cancellationToken);

        // Deliberately give the same error whether the email is unknown or the
        // password is wrong, so the response cannot be used to discover which
        // email addresses are registered.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for {Email}.", request.Email);
            throw new ValidationException(
                "Email or password is incorrect.", ErrorCodes.InvalidCredentials);
        }

        // A self registered prosumer awaiting activation, or an account that a
        // back-office officer has closed, must not be able to sign in.
        if (!user.IsActive)
        {
            throw new ForbiddenException(
                "This account is not active. Please contact the back-office team.",
                ErrorCodes.AccountInactive);
        }

        // Issue the signed token describing the user.
        var token = _tokenService.CreateAccessToken(user);

        return new LoginResponse(token.AccessToken, token.ExpiresAtUtc, user.ToResponse());
    }

    /// <summary>
    /// Registers a prosumer from the Android application. The NIC becomes the
    /// document identifier and the account starts inactive.
    /// </summary>
    public async Task<UserResponse> RegisterProsumerAsync(
        RegisterProsumerRequest request, CancellationToken cancellationToken = default)
    {
        // Self service registration never activates the account: it must be
        // approved by a back-office officer, which is what populates the
        // pending activation screen in the web application.
        return await CreateProsumerInternalAsync(request, activate: false, cancellationToken);
    }

    /// <summary>
    /// Creates a prosumer on behalf of a back-office officer, who may choose to
    /// activate the account immediately because they have checked the details.
    /// </summary>
    public async Task<UserResponse> CreateProsumerAsync(
        CreateProsumerRequest request, CancellationToken cancellationToken = default)
    {
        return await CreateProsumerInternalAsync(request, request.ActivateImmediately, cancellationToken);
    }

    /// <summary>
    /// Shared prosumer creation logic used by both the self service and the
    /// back-office paths, so the uniqueness rules cannot drift apart.
    /// </summary>
    private async Task<UserResponse> CreateProsumerInternalAsync(
        RegisterProsumerRequest request, bool activate, CancellationToken cancellationToken)
    {
        var nic = request.Nic.Trim().ToUpperInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        // The NIC is the primary key, so an existing document with that key
        // means this person is already registered.
        var existing = await _users.GetByIdAsync(nic, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException(
                ErrorCodes.NicAlreadyRegistered,
                $"A prosumer is already registered with NIC {nic}.");
        }

        // Email is the login identifier and must therefore also be unique.
        if (await _users.EmailExistsAsync(email, cancellationToken: cancellationToken))
        {
            throw new ConflictException(
                ErrorCodes.EmailAlreadyUsed,
                $"The email address {email} is already in use.");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = nic,
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRoles.Prosumer,
            IsActive = activate,
            DeactivationRequested = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _users.InsertAsync(user, cancellationToken);
        _logger.LogInformation("Prosumer {Nic} registered. Active: {IsActive}.", nic, activate);

        return user.ToResponse();
    }

    /// <summary>
    /// Creates a Backoffice or GridOperator account. Staff accounts are active
    /// as soon as they are created because an officer created them.
    /// </summary>
    public async Task<UserResponse> CreateStaffUserAsync(
        CreateStaffUserRequest request, CancellationToken cancellationToken = default)
    {
        // Only the two web application roles may be created here. A prosumer
        // must be created through the prosumer endpoint so that a NIC is
        // always supplied as the primary key.
        if (request.Role != UserRoles.Backoffice && request.Role != UserRoles.GridOperator)
        {
            throw new ValidationException(
                $"Role must be either {UserRoles.Backoffice} or {UserRoles.GridOperator}.",
                ErrorCodes.RoleNotAllowed);
        }

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _users.EmailExistsAsync(email, cancellationToken: cancellationToken))
        {
            throw new ConflictException(
                ErrorCodes.EmailAlreadyUsed,
                $"The email address {email} is already in use.");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            // Staff have no NIC, so a generated identifier is used instead.
            Id = ObjectId.GenerateNewId().ToString(),
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = request.Phone?.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = request.Role,
            IsActive = true,
            DeactivationRequested = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _users.InsertAsync(user, cancellationToken);
        _logger.LogInformation("Staff account {Email} created with role {Role}.", email, request.Role);

        return user.ToResponse();
    }

    /// <summary>
    /// Returns one account, or reports that it does not exist.
    /// </summary>
    public async Task<UserResponse> GetByIdAsync(
        string id, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredAsync(id, cancellationToken);
        return user.ToResponse();
    }

    /// <summary>
    /// Lists accounts using the supplied optional filters.
    /// </summary>
    public async Task<IReadOnlyList<UserResponse>> ListAsync(
        string? role = null,
        bool? isActive = null,
        bool? deactivationRequested = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        // Reject an unrecognised role rather than silently returning nothing,
        // which would look like an empty database to the caller.
        if (!string.IsNullOrWhiteSpace(role) && !UserRoles.All.Contains(role))
        {
            throw new ValidationException($"Unknown role '{role}'.");
        }

        var users = await _users.ListAsync(role, isActive, deactivationRequested, search, cancellationToken);
        return users.ToResponseList();
    }

    /// <summary>
    /// Updates the editable profile fields of an account.
    /// </summary>
    public async Task<UserResponse> UpdateAsync(
        string id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredAsync(id, cancellationToken);

        user.FullName = request.FullName.Trim();
        user.Phone = request.Phone?.Trim();
        user.Address = request.Address?.Trim();
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _users.ReplaceAsync(user, cancellationToken);
        return user.ToResponse();
    }

    /// <summary>
    /// Activates an account and clears any outstanding deactivation request.
    /// The controller restricts this to back-office officers, which is how the
    /// "only a back-office officer may reactivate" rule is enforced.
    /// </summary>
    public async Task<UserResponse> ActivateAsync(
        string id, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredAsync(id, cancellationToken);

        user.IsActive = true;

        // Reactivating settles any request the prosumer had raised, so the
        // account stops appearing in the outstanding requests list.
        user.DeactivationRequested = false;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _users.ReplaceAsync(user, cancellationToken);
        _logger.LogInformation("Account {Id} activated.", id);

        return user.ToResponse();
    }

    /// <summary>
    /// Deactivates an account so that it can no longer sign in.
    /// </summary>
    public async Task<UserResponse> DeactivateAsync(
        string id, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredAsync(id, cancellationToken);

        user.IsActive = false;

        // The request has now been carried out, so clear the flag.
        user.DeactivationRequested = false;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _users.ReplaceAsync(user, cancellationToken);
        _logger.LogInformation("Account {Id} deactivated.", id);

        return user.ToResponse();
    }

    /// <summary>
    /// Flags that a prosumer has asked for their account to be closed. The
    /// account deliberately stays active: only a back-office officer may
    /// actually deactivate it, and only they may activate it again.
    /// </summary>
    public async Task<UserResponse> RequestDeactivationAsync(
        string id, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredAsync(id, cancellationToken);

        // Only prosumers use the self service request; staff accounts are
        // managed directly by a back-office officer.
        if (user.Role != UserRoles.Prosumer)
        {
            throw new ValidationException(
                "Only prosumer accounts may request their own deactivation.",
                ErrorCodes.RoleNotAllowed);
        }

        user.DeactivationRequested = true;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _users.ReplaceAsync(user, cancellationToken);
        _logger.LogInformation("Prosumer {Id} requested deactivation.", id);

        return user.ToResponse();
    }

    /// <summary>
    /// Loads an account and throws a not found error when it does not exist,
    /// so callers never have to repeat the null check.
    /// </summary>
    private async Task<User> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException($"No account was found with identifier '{id}'.");
        }

        return user;
    }
}
