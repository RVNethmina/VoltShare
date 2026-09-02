// -----------------------------------------------------------------------------
// File        : QrSettings.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Configuration
// Description : Strongly typed representation of the "Qr" section of
//               appsettings.json. Supplies the secret used to sign the
//               transaction QR tokens carried by prosumers.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

namespace VoltShare.Api.Configuration;

/// <summary>
/// Settings for the tamper-proof QR token issued when a reservation is
/// approved and verified when a grid operator scans it.
/// </summary>
public class QrSettings
{
    // Name of the configuration section this class is bound to.
    public const string SectionName = "Qr";

    // Secret key used to compute the HMAC-SHA256 signature of a QR token.
    // Because only the server knows this value, a QR code cannot be forged.
    public string SigningKey { get; set; } = string.Empty;
}
