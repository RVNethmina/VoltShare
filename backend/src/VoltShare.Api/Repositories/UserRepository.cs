// -----------------------------------------------------------------------------
// File        : UserRepository.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Repositories
// Description : MongoDB implementation of IUserRepository using the official
//               MongoDB.Driver package.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using VoltShare.Api.Data;
using VoltShare.Api.Models;

namespace VoltShare.Api.Repositories;

/// <summary>
/// Persists user documents in the "users" collection.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;

    // Shorthand for the driver filter builder, used throughout this class.
    private static readonly FilterDefinitionBuilder<User> Filter = Builders<User>.Filter;

    /// <summary>
    /// Resolves the users collection from the shared database context.
    /// </summary>
    public UserRepository(MongoContext context)
    {
        _users = context.Users;
    }

    /// <summary>
    /// Finds one account by its identifier.
    /// </summary>
    public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        // A blank identifier can never match, so avoid the database round trip.
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return await _users.Find(Filter.Eq(u => u.Id, id))
                           .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Finds one account by email address.
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        // Emails are stored lower cased by the service layer, so matching on a
        // normalised value keeps the unique index usable and login predictable.
        var normalised = email.Trim().ToLowerInvariant();

        return await _users.Find(Filter.Eq(u => u.Email, normalised))
                           .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lists accounts matching the supplied optional filters.
    /// </summary>
    public async Task<IReadOnlyList<User>> ListAsync(
        string? role = null,
        bool? isActive = null,
        bool? deactivationRequested = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        // Build the filter up from only the criteria that were supplied, so an
        // absent parameter widens the result instead of excluding everything.
        var filters = new List<FilterDefinition<User>> { Filter.Empty };

        if (!string.IsNullOrWhiteSpace(role))
        {
            filters.Add(Filter.Eq(u => u.Role, role));
        }

        if (isActive.HasValue)
        {
            filters.Add(Filter.Eq(u => u.IsActive, isActive.Value));
        }

        if (deactivationRequested.HasValue)
        {
            filters.Add(Filter.Eq(u => u.DeactivationRequested, deactivationRequested.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Case insensitive "contains" search across the fields a member of
            // staff would realistically type: identifier, name and email.
            // The term is escaped so that characters such as . or * typed by a
            // user are treated as literal text and not as regular expression
            // operators, which would otherwise change the meaning of the search.
            var pattern = new BsonRegularExpression(
                Regex.Escape(search.Trim()), "i");

            filters.Add(Filter.Or(
                Filter.Regex(u => u.Id, pattern),
                Filter.Regex(u => u.FullName, pattern),
                Filter.Regex(u => u.Email, pattern)));
        }

        return await _users.Find(Filter.And(filters))
                           .SortBy(u => u.FullName)
                           .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Stores a new account document.
    /// </summary>
    public async Task InsertAsync(User user, CancellationToken cancellationToken = default)
    {
        await _users.InsertOneAsync(user, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Overwrites an existing account document.
    /// </summary>
    public async Task ReplaceAsync(User user, CancellationToken cancellationToken = default)
    {
        await _users.ReplaceOneAsync(
            Filter.Eq(u => u.Id, user.Id), user, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// True when the email address is already taken by a different account.
    /// </summary>
    public async Task<bool> EmailExistsAsync(
        string email, string? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var filter = Filter.Eq(u => u.Email, normalised);

        // When a user is editing their own profile their existing document
        // must not count as a clash with itself.
        if (!string.IsNullOrWhiteSpace(excludeUserId))
        {
            filter = Filter.And(filter, Filter.Ne(u => u.Id, excludeUserId));
        }

        return await _users.Find(filter).AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Counts accounts matching a role and activation state.
    /// </summary>
    public async Task<long> CountAsync(
        string? role = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<User>> { Filter.Empty };

        if (!string.IsNullOrWhiteSpace(role))
        {
            filters.Add(Filter.Eq(u => u.Role, role));
        }

        if (isActive.HasValue)
        {
            filters.Add(Filter.Eq(u => u.IsActive, isActive.Value));
        }

        return await _users.CountDocumentsAsync(Filter.And(filters), cancellationToken: cancellationToken);
    }
}
