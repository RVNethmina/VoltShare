// -----------------------------------------------------------------------------
// File        : JwtTokenService.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Security
// Description : Issues signed JSON Web Tokens. The token carries the user
//               identifier and role, which is what lets the API authorise a
//               request without going back to the database every time.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VoltShare.Api.Configuration;
using VoltShare.Api.Models;

namespace VoltShare.Api.Security;

/// <summary>
/// Builds HMAC-SHA256 signed tokens from the settings in the "Jwt" section.
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;

    /// <summary>
    /// Receives the bound JWT settings and validates that a signing key exists.
    /// </summary>
    public JwtTokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;

        // HMAC-SHA256 requires a key of at least 256 bits. Checking here means
        // a misconfigured deployment fails at start-up with a clear message
        // rather than at the first login attempt with a cryptic one.
        if (string.IsNullOrWhiteSpace(_settings.Key) || Encoding.UTF8.GetByteCount(_settings.Key) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key must be configured and be at least 32 bytes long.");
        }
    }

    /// <summary>
    /// Creates a signed access token describing the supplied user.
    /// </summary>
    public TokenResult CreateAccessToken(User user)
    {
        // Work out the expiry once so the same value is signed into the token
        // and handed back to the caller.
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);

        // Claims are the facts the API will trust on every later request.
        // The subject holds User.Id, which for a prosumer is their NIC.
        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id,
            [JwtRegisteredClaimNames.Email] = user.Email,
            [JwtRegisteredClaimNames.Name] = user.FullName,
            [ClaimTypes.NameIdentifier] = user.Id,
            [ClaimTypes.Role] = user.Role
        };

        // Describe the token: who it is about, who issued it, who may consume
        // it, how long it lives and how it is signed.
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = claims,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = DateTime.UtcNow,
            Expires = expiresAtUtc,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)),
                SecurityAlgorithms.HmacSha256)
        };

        // Serialise the descriptor into the compact JWT string sent to clients.
        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new TokenResult(token, expiresAtUtc);
    }
}
