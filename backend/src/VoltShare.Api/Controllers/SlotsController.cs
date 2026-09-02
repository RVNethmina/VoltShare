// -----------------------------------------------------------------------------
// File        : SlotsController.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Controllers
// Description : Endpoints that address a single energy booking window directly.
//               Creation and listing by station live on StationsController,
//               because a window only exists in the context of its station.
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
/// Individual energy booking windows.
/// </summary>
[ApiController]
[Route("api/v1/slots")]
[Produces("application/json")]
[Authorize]
public class SlotsController : ControllerBase
{
    private readonly ISlotService _slots;

    /// <summary>
    /// Receives the slot service from dependency injection.
    /// </summary>
    public SlotsController(ISlotService slots)
    {
        _slots = slots;
    }

    /// <summary>
    /// Returns one booking window by identifier.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SlotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SlotResponse>> GetByIdAsync(
        string id, CancellationToken cancellationToken)
    {
        var slot = await _slots.GetByIdAsync(id, cancellationToken);
        return Ok(slot);
    }

    /// <summary>
    /// Updates a booking window. Refused if the capacity would drop below the
    /// number of places already booked.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType(typeof(SlotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SlotResponse>> UpdateAsync(
        string id, [FromBody] UpdateSlotRequest request, CancellationToken cancellationToken)
    {
        var updated = await _slots.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a booking window. Refused while prosumers still hold active
    /// reservations against it.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _slots.DeleteAsync(id, cancellationToken);

        // 204 because there is no longer any resource to return.
        return NoContent();
    }
}
