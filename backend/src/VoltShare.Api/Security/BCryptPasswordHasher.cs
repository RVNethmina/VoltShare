// -----------------------------------------------------------------------------
// File        : BCryptPasswordHasher.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Security
// Description : BCrypt implementation of IPasswordHasher. BCrypt is used
//               because it salts every hash automatically and is deliberately
//               slow, which makes stolen hashes expensive to attack.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

namespace VoltShare.Api.Security;

/// <summary>
/// Hashes passwords with the BCrypt algorithm from the BCrypt.Net-Next package.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    // Cost factor of the BCrypt work loop. Each increment doubles the time
    // taken to compute a hash. 12 is a common balance between security and
    // login responsiveness on modest hardware.
    private const int WorkFactor = 12;

    /// <summary>
    /// Produces a salted BCrypt hash. The generated salt is stored inside the
    /// returned string, so no separate salt column is needed.
    /// </summary>
    public string Hash(string plainPassword)
    {
        // Reject empty passwords here so a blank value can never be hashed
        // and silently accepted at login later.
        if (string.IsNullOrWhiteSpace(plainPassword))
        {
            throw new ArgumentException("Password must not be empty.", nameof(plainPassword));
        }

        return BCrypt.Net.BCrypt.HashPassword(plainPassword, WorkFactor);
    }

    /// <summary>
    /// Compares a plain password against a stored hash.
    /// </summary>
    public bool Verify(string plainPassword, string storedHash)
    {
        // Guard against missing input so verification cannot throw during a
        // login attempt; an empty value simply fails to match.
        if (string.IsNullOrWhiteSpace(plainPassword) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        // A malformed hash in the database would otherwise throw and surface
        // as a 500, so treat it as a failed login instead.
        try
        {
            return BCrypt.Net.BCrypt.Verify(plainPassword, storedHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
