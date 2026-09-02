// -----------------------------------------------------------------------------
// File        : IUserRepository.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Repositories
// Description : Data access contract for the "users" collection. Contains only
//               storage operations; every business rule lives in the service
//               layer, which is what keeps the FAT service pattern intact.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Models;

namespace VoltShare.Api.Repositories;

/// <summary>
/// Reads and writes user documents.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Finds one account by its identifier, which is the NIC for a prosumer.
    /// </summary>
    Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds one account by email address, used at login.
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists accounts, optionally narrowed by role, activation state, whether
    /// a deactivation has been requested, and a free text search term.
    /// </summary>
    Task<IReadOnlyList<User>> ListAsync(
        string? role = null,
        bool? isActive = null,
        bool? deactivationRequested = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new account document.
    /// </summary>
    Task InsertAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites an existing account document.
    /// </summary>
    Task ReplaceAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when an account already uses the supplied email address.
    /// An optional identifier can be excluded so a user keeps their own email.
    /// </summary>
    Task<bool> EmailExistsAsync(
        string email, string? excludeUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts accounts matching a role and activation state, used by dashboards.
    /// </summary>
    Task<long> CountAsync(
        string? role = null, bool? isActive = null, CancellationToken cancellationToken = default);
}
