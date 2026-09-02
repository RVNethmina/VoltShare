// -----------------------------------------------------------------------------
// File        : JwtSettings.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Configuration
// Description : Strongly typed representation of the "Jwt" section of
//               appsettings.json, describing how access tokens are signed
//               and validated.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

namespace VoltShare.Api.Configuration;

/// <summary>
/// Settings used to issue and validate JSON Web Tokens for both the web
/// application and the Android application.
/// </summary>
public class JwtSettings
{
    // Name of the configuration section this class is bound to.
    public const string SectionName = "Jwt";

    // Symmetric signing key. Must be at least 32 characters for HMAC-SHA256.
    public string Key { get; set; } = string.Empty;

    // Value written into the "iss" claim and validated on every request.
    public string Issuer { get; set; } = "VoltShare.Api";

    // Value written into the "aud" claim and validated on every request.
    public string Audience { get; set; } = "VoltShare.Clients";

    // Token lifetime in minutes. Kept long enough for a mobile session.
    public int ExpiryMinutes { get; set; } = 480;
}
