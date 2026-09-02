// -----------------------------------------------------------------------------
// File        : DatabaseSeeder.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Data
// Description : Creates the very first Backoffice account when the users
//               collection contains none. Without this there would be no way
//               to sign in to the web application on a fresh database.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using MongoDB.Bson;
using VoltShare.Api.Models;
using VoltShare.Api.Repositories;
using VoltShare.Api.Security;

namespace VoltShare.Api.Data;

/// <summary>
/// Puts the minimum data needed to use the system into an empty database.
/// </summary>
public class DatabaseSeeder
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSeeder> _logger;

    /// <summary>
    /// Receives its collaborators from dependency injection.
    /// </summary>
    public DatabaseSeeder(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<DatabaseSeeder> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Creates the bootstrap back-office account if, and only if, no
    /// Backoffice user exists yet. Running this on every start-up is therefore
    /// safe and never overwrites a changed password.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // If an administrator already exists there is nothing to do.
        var existingAdmins = await _users.CountAsync(UserRoles.Backoffice, cancellationToken: cancellationToken);
        if (existingAdmins > 0)
        {
            _logger.LogInformation("Seed skipped: {Count} back-office account(s) already exist.", existingAdmins);
            return;
        }

        // Read the bootstrap credentials from configuration so they are not
        // hard coded in the source, and can be overridden per environment.
        var email = _configuration["SeedAdmin:Email"] ?? "admin@voltshare.lk";
        var password = _configuration["SeedAdmin:Password"] ?? "Admin@123";
        var fullName = _configuration["SeedAdmin:FullName"] ?? "System Administrator";

        var now = DateTime.UtcNow;
        var admin = new User
        {
            Id = ObjectId.GenerateNewId().ToString(),
            FullName = fullName,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(password),
            Role = UserRoles.Backoffice,
            IsActive = true,
            DeactivationRequested = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _users.InsertAsync(admin, cancellationToken);

        // Announce the account so the developer knows how to sign in, and warn
        // that the default password must be changed before the demonstration.
        _logger.LogWarning(
            "Seeded initial back-office account '{Email}'. Change this password before deployment.",
            admin.Email);
    }
}
