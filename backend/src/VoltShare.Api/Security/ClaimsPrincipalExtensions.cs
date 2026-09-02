// -----------------------------------------------------------------------------
// File        : ClaimsPrincipalExtensions.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Security
// Description : Helper methods for reading the identity and role of the caller
//               out of the validated JWT, so controllers do not repeat claim
//               lookup code and ownership checks stay consistent.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using System.Security.Claims;
using VoltShare.Api.Middleware;
using VoltShare.Api.Models;

namespace VoltShare.Api.Security;

/// <summary>
/// Convenience accessors over the claims carried by an authenticated request.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the identifier of the caller, which is the NIC for a prosumer.
    /// </summary>
    public static string GetUserId(this ClaimsPrincipal principal)
    {
        // The token service writes the identifier into two standard claims;
        // check both so the lookup does not depend on claim mapping settings.
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? principal.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ForbiddenException("The access token does not identify a user.");
        }

        return id;
    }

    /// <summary>
    /// Returns the role carried by the token, or an empty string when absent.
    /// </summary>
    public static string GetRole(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }

    /// <summary>
    /// True when the caller is a back-office officer or a grid operator.
    /// </summary>
    public static bool IsStaff(this ClaimsPrincipal principal)
    {
        var role = principal.GetRole();
        return role == UserRoles.Backoffice || role == UserRoles.GridOperator;
    }

    /// <summary>
    /// Confirms the caller either owns the record identified by the supplied
    /// NIC or is a member of staff, and refuses the request otherwise. This is
    /// what stops one prosumer reading or editing another prosumer's data.
    /// </summary>
    public static void EnsureOwnerOrStaff(this ClaimsPrincipal principal, string ownerNic)
    {
        if (principal.IsStaff())
        {
            return;
        }

        if (!string.Equals(principal.GetUserId(), ownerNic, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("You may only access your own records.");
        }
    }
}
