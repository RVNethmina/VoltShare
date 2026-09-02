// -----------------------------------------------------------------------------
// File        : ProsumersController.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Controllers
// Description : Management of solar prosumer accounts, keyed by NIC. Serves the
//               back-office prosumer screens, the pending activation list and
//               the self service profile actions of the Android application.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoltShare.Api.Dtos;
using VoltShare.Api.Models;
using VoltShare.Api.Security;
using VoltShare.Api.Services;

namespace VoltShare.Api.Controllers;

/// <summary>
/// Prosumer accounts. Each action carries the narrowest policy that still lets
/// the intended caller through, and ownership is checked where a prosumer is
/// allowed to act on their own record.
/// </summary>
[ApiController]
[Route("api/v1/prosumers")]
[Produces("application/json")]
[Authorize]
public class ProsumersController : ControllerBase
{
    private readonly IAccountService _accounts;

    /// <summary>
    /// Receives the account service from dependency injection.
    /// </summary>
    public ProsumersController(IAccountService accounts)
    {
        _accounts = accounts;
    }

    /// <summary>
    /// Lists prosumers for staff, with an optional activation filter and a
    /// free text search across NIC, name and email.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> ListAsync(
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var prosumers = await _accounts.ListAsync(
            role: UserRoles.Prosumer,
            isActive: isActive,
            deactivationRequested: null,
            search: search,
            cancellationToken: cancellationToken);

        return Ok(prosumers);
    }

    /// <summary>
    /// Lists prosumers awaiting activation. This is the pending activation view
    /// the specification requires in the web application, and it is limited to
    /// back-office officers because only they may activate an account.
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Policy = Policies.Backoffice)]
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> ListPendingAsync(
        CancellationToken cancellationToken)
    {
        // An inactive prosumer is one that has registered from the mobile app
        // but has not yet been approved by the back-office team.
        var pending = await _accounts.ListAsync(
            role: UserRoles.Prosumer,
            isActive: false,
            cancellationToken: cancellationToken);

        return Ok(pending);
    }

    /// <summary>
    /// Lists prosumers who have asked for their account to be closed, so that
    /// a back-office officer can act on the requests.
    /// </summary>
    [HttpGet("deactivation-requests")]
    [Authorize(Policy = Policies.Backoffice)]
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> ListDeactivationRequestsAsync(
        CancellationToken cancellationToken)
    {
        var requests = await _accounts.ListAsync(
            role: UserRoles.Prosumer,
            deactivationRequested: true,
            cancellationToken: cancellationToken);

        return Ok(requests);
    }

    /// <summary>
    /// Returns one prosumer by NIC. A prosumer may read only their own record;
    /// staff may read any.
    /// </summary>
    [HttpGet("{nic}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetByNicAsync(
        string nic, CancellationToken cancellationToken)
    {
        // Ownership check: refuse if a prosumer asks for a different NIC.
        User.EnsureOwnerOrStaff(nic);

        var prosumer = await _accounts.GetByIdAsync(nic, cancellationToken);
        return Ok(prosumer);
    }

    /// <summary>
    /// Creates a prosumer account on behalf of a back-office officer.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Backoffice)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> CreateAsync(
        [FromBody] CreateProsumerRequest request, CancellationToken cancellationToken)
    {
        var created = await _accounts.CreateProsumerAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByNicAsync), new { nic = created.Id }, created);
    }

    /// <summary>
    /// Updates a prosumer profile. A prosumer may edit only their own details,
    /// which is the self service profile edit in the Android application.
    /// </summary>
    [HttpPut("{nic}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> UpdateAsync(
        string nic, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        // Ownership check before any data is changed.
        User.EnsureOwnerOrStaff(nic);

        var updated = await _accounts.UpdateAsync(nic, request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Records a prosumer request to close their own account. The account stays
    /// usable until a back-office officer acts on the request.
    /// </summary>
    [HttpPatch("{nic}/request-deactivation")]
    [Authorize(Policy = Policies.Prosumer)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserResponse>> RequestDeactivationAsync(
        string nic, CancellationToken cancellationToken)
    {
        // A prosumer may only request deactivation of their own account.
        User.EnsureOwnerOrStaff(nic);

        var updated = await _accounts.RequestDeactivationAsync(nic, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Activates a prosumer account. Restricted to back-office officers, which
    /// is the rule that a deactivated account can only be reactivated by the
    /// back office.
    /// </summary>
    [HttpPatch("{nic}/activate")]
    [Authorize(Policy = Policies.Backoffice)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> ActivateAsync(
        string nic, CancellationToken cancellationToken)
    {
        var updated = await _accounts.ActivateAsync(nic, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deactivates a prosumer account. Restricted to back-office officers.
    /// </summary>
    [HttpPatch("{nic}/deactivate")]
    [Authorize(Policy = Policies.Backoffice)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> DeactivateAsync(
        string nic, CancellationToken cancellationToken)
    {
        var updated = await _accounts.DeactivateAsync(nic, cancellationToken);
        return Ok(updated);
    }
}
