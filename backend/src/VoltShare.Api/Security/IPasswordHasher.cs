// -----------------------------------------------------------------------------
// File        : IPasswordHasher.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Security
// Description : Contract for hashing and verifying account passwords. Declared
//               as an interface so the hashing algorithm can be replaced
//               without touching any service that depends on it.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

namespace VoltShare.Api.Security;

/// <summary>
/// Turns plain passwords into one way hashes and checks them again at login.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Produces a salted one way hash of the supplied plain password.
    /// </summary>
    string Hash(string plainPassword);

    /// <summary>
    /// Returns true when the plain password matches the stored hash.
    /// </summary>
    bool Verify(string plainPassword, string storedHash);
}
