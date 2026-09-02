// -----------------------------------------------------------------------------
// File        : IQrTokenService.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Security
// Description : Contract for issuing and verifying the transaction QR tokens
//               that a prosumer presents and a grid operator scans.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

namespace VoltShare.Api.Security;

/// <summary>
/// Creates tamper proof tokens for approved reservations and checks them again
/// when an operator scans the QR code.
/// </summary>
public interface IQrTokenService
{
    /// <summary>
    /// Issues a signed token for a reservation. The mobile application renders
    /// the returned string as a QR code; it carries no personal data.
    /// </summary>
    string Issue(string reservationId, DateTime slotStartUtc);

    /// <summary>
    /// Checks the signature of a scanned token and returns the reservation
    /// identifier it refers to. Returns null when the token is malformed or
    /// the signature does not match, which means it was not issued by us.
    /// </summary>
    string? Verify(string token);
}
