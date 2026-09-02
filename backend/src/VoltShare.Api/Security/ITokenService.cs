// -----------------------------------------------------------------------------
// File        : ITokenService.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Security
// Description : Contract for issuing JSON Web Tokens to authenticated users of
//               the web application and the Android application.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Models;

namespace VoltShare.Api.Security;

/// <summary>
/// Result of issuing an access token, returned to the client at login.
/// </summary>
/// <param name="AccessToken">The signed JWT string.</param>
/// <param name="ExpiresAtUtc">The moment the token stops being accepted.</param>
public record TokenResult(string AccessToken, DateTime ExpiresAtUtc);

/// <summary>
/// Creates signed access tokens carrying the identity and role of a user.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Issues a signed JWT describing the supplied user.
    /// </summary>
    TokenResult CreateAccessToken(User user);
}
