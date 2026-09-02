// -----------------------------------------------------------------------------
// File        : UserMappings.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Dtos
// Description : Conversion from the User storage document to the UserResponse
//               contract. Kept in one place so that no endpoint can
//               accidentally return the password hash.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Models;

namespace VoltShare.Api.Dtos;

/// <summary>
/// Maps user documents onto the response contract sent to clients.
/// </summary>
public static class UserMappings
{
    /// <summary>
    /// Builds the client facing representation of an account. Note that the
    /// password hash is simply not copied, so it cannot leak.
    /// </summary>
    public static UserResponse ToResponse(this User user)
    {
        return new UserResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.Phone,
            user.Address,
            user.Role,
            user.IsActive,
            user.DeactivationRequested,
            user.CreatedAtUtc);
    }

    /// <summary>
    /// Maps a whole collection of accounts onto response objects.
    /// </summary>
    public static IReadOnlyList<UserResponse> ToResponseList(this IEnumerable<User> users)
    {
        return users.Select(u => u.ToResponse()).ToList();
    }
}
