// -----------------------------------------------------------------------------
// File        : UsersController.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Controllers
// Description : Back-office administration of web application accounts, that
//               is Backoffice and GridOperator users. The whole controller is
//               restricted to back-office officers.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoltShare.Api.Dtos;
using VoltShare.Api.Models;
using VoltShare.Api.Services;

namespace VoltShare.Api.Controllers;

/// <summary>
/// Creates and maintains staff accounts. Applying the policy at the class
/// level means every action inherits the same restriction, so a new endpoint
/// cannot accidentally be left unprotected.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
[Authorize(Policy = Policies.Backoffice)]
public class UsersController : ControllerBase
{
    private readonly IAccountService _accounts;

    /// <summary>
    /// Receives the account service from dependency injection.
    /// </summary>
    public UsersController(IAccountService accounts)
    {
        _accounts = accounts;
    }

    /// <summary>
    /// Lists accounts, optionally filtered by role, activation state and a
    /// free text search across identifier, name and email.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> ListAsync(
        [FromQuery] string? role,
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var users = await _accounts.ListAsync(
            role, isActive, deactivationRequested: null, search: search,
            cancellationToken: cancellationToken);

        return Ok(users);
    }

    /// <summary>
    /// Returns a single account by its identifier.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetByIdAsync(
        string id, CancellationToken cancellationToken)
    {
        var user = await _accounts.GetByIdAsync(id, cancellationToken);
        return Ok(user);
    }

    /// <summary>
    /// Creates a Backoffice or GridOperator account.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> CreateAsync(
        [FromBody] CreateStaffUserRequest request, CancellationToken cancellationToken)
    {
        var created = await _accounts.CreateStaffUserAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates the editable profile fields of an account.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> UpdateAsync(
        string id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var updated = await _accounts.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Activates an account so that it can sign in again.
    /// </summary>
    [HttpPatch("{id}/activate")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> ActivateAsync(
        string id, CancellationToken cancellationToken)
    {
        var updated = await _accounts.ActivateAsync(id, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deactivates an account so that it can no longer sign in.
    /// </summary>
    [HttpPatch("{id}/deactivate")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> DeactivateAsync(
        string id, CancellationToken cancellationToken)
    {
        var updated = await _accounts.DeactivateAsync(id, cancellationToken);
        return Ok(updated);
    }
}
