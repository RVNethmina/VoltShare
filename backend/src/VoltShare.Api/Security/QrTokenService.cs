// -----------------------------------------------------------------------------
// File        : QrTokenService.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Security
// Description : Issues and verifies the transaction QR tokens. A token is the
//               payload plus an HMAC-SHA256 signature computed with a server
//               only secret, so a QR code cannot be forged or altered by a
//               prosumer, and the operator's scan is checked against the
//               server rather than trusted on its own.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using VoltShare.Api.Configuration;

namespace VoltShare.Api.Security;

/// <summary>
/// HMAC based implementation of IQrTokenService.
///
/// The token deliberately carries no NIC, name or other personal data. It
/// identifies a reservation only; the operator's device posts it back and the
/// server returns the booking details. That way a photographed QR code leaks
/// nothing about the prosumer.
/// </summary>
public class QrTokenService : IQrTokenService
{
    private readonly byte[] _signingKey;

    // Separates the payload from its signature inside the token string.
    private const char TokenSeparator = '.';

    // Separates the fields inside the payload.
    private const char PayloadSeparator = '|';

    /// <summary>
    /// Reads the signing secret and refuses to start without a usable one.
    /// </summary>
    public QrTokenService(IOptions<QrSettings> options)
    {
        var key = options.Value.SigningKey;

        // A short or missing secret would make forged QR codes feasible, so
        // fail at start-up with a clear message rather than run insecurely.
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException(
                "Qr:SigningKey must be configured and be at least 32 bytes long.");
        }

        _signingKey = Encoding.UTF8.GetBytes(key);
    }

    /// <summary>
    /// Issues a signed token for a reservation.
    /// </summary>
    public string Issue(string reservationId, DateTime slotStartUtc)
    {
        // A random nonce means re-approving a reservation produces a different
        // token, so an old screenshot of a QR code cannot be reused.
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));

        // Ticks give an unambiguous, culture independent timestamp.
        var payload = string.Join(PayloadSeparator,
            reservationId,
            nonce,
            slotStartUtc.Ticks.ToString());

        var signature = ComputeSignature(payload);

        return $"{Base64UrlEncode(Encoding.UTF8.GetBytes(payload))}" +
               $"{TokenSeparator}" +
               $"{Base64UrlEncode(signature)}";
    }

    /// <summary>
    /// Verifies a scanned token and extracts the reservation identifier.
    /// </summary>
    public string? Verify(string token)
    {
        // A blank scan is simply an invalid token, not an error condition.
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        // The whole method is wrapped because a scanner can hand us arbitrary
        // text; malformed input must be reported as invalid, never as a crash.
        try
        {
            var parts = token.Split(TokenSeparator);
            if (parts.Length != 2)
            {
                return null;
            }

            var payloadBytes = Base64UrlDecode(parts[0]);
            var payload = Encoding.UTF8.GetString(payloadBytes);
            var providedSignature = Base64UrlDecode(parts[1]);

            var expectedSignature = ComputeSignature(payload);

            // A fixed time comparison stops an attacker learning the correct
            // signature byte by byte from how long the comparison takes.
            if (!CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
            {
                return null;
            }

            var fields = payload.Split(PayloadSeparator);
            if (fields.Length != 3 || string.IsNullOrWhiteSpace(fields[0]))
            {
                return null;
            }

            // Only the reservation identifier is returned; the caller looks the
            // booking up and applies the status rules.
            return fields[0];
        }
        catch (FormatException)
        {
            // The token was not valid base64url text.
            return null;
        }
        catch (ArgumentException)
        {
            // The token was structurally unusable.
            return null;
        }
    }

    /// <summary>
    /// Computes the HMAC-SHA256 signature of a payload using the server secret.
    /// </summary>
    private byte[] ComputeSignature(string payload)
    {
        using var hmac = new HMACSHA256(_signingKey);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>
    /// Encodes bytes as base64url, which is safe inside a QR code and a URL
    /// because it avoids the +, / and = characters.
    /// </summary>
    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value)
                      .TrimEnd('=')
                      .Replace('+', '-')
                      .Replace('/', '_');
    }

    /// <summary>
    /// Reverses Base64UrlEncode, restoring the padding that was trimmed.
    /// </summary>
    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');

        // Base64 text must be a multiple of four characters long.
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
