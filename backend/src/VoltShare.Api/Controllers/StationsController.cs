// -----------------------------------------------------------------------------
// File        : StationsController.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Controllers
// Description : Solar microgrid node endpoints. Serves the back-office station
//               screens, the grid operator battery availability updates, and
//               the nearby stations search used by the Android map.
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
/// Solar microgrid nodes. Reading is open to any signed in user because both
/// prosumers and staff need the station list; every change is restricted.
/// </summary>
[ApiController]
[Route("api/v1/stations")]
[Produces("application/json")]
[Authorize]
public class StationsController : ControllerBase
{
    private readonly IStationService _stations;
    private readonly ISlotService _slots;

    /// <summary>
    /// Receives the station and slot services from dependency injection.
    /// </summary>
    public StationsController(IStationService stations, ISlotService slots)
    {
        _stations = stations;
        _slots = slots;
    }

    /// <summary>
    /// Lists stations with optional activation, city and search filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StationResponse>>> ListAsync(
        [FromQuery] bool? isActive,
        [FromQuery] string? city,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var stations = await _stations.ListAsync(isActive, city, search, cancellationToken);
        return Ok(stations);
    }

    /// <summary>
    /// Finds active stations near a point, nearest first, with the distance to
    /// each calculated by the database. This is what the Android map calls.
    /// </summary>
    [HttpGet("nearby")]
    [ProducesResponseType(typeof(IReadOnlyList<NearbyStationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<NearbyStationResponse>>> FindNearbyAsync(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] double radiusKm = 10d,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var stations = await _stations.FindNearbyAsync(lat, lng, radiusKm, limit, cancellationToken);
        return Ok(stations);
    }

    /// <summary>
    /// Returns a single station by identifier.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StationResponse>> GetByIdAsync(
        string id, CancellationToken cancellationToken)
    {
        var station = await _stations.GetByIdAsync(id, cancellationToken);
        return Ok(station);
    }

    /// <summary>
    /// Creates a new station. Registering microgrid nodes is a back-office
    /// responsibility.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Backoffice)]
    [ProducesResponseType(typeof(StationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StationResponse>> CreateAsync(
        [FromBody] CreateStationRequest request, CancellationToken cancellationToken)
    {
        var created = await _stations.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an existing station.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = Policies.Backoffice)]
    [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StationResponse>> UpdateAsync(
        string id, [FromBody] UpdateStationRequest request, CancellationToken cancellationToken)
    {
        var updated = await _stations.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Brings a station back into service.
    /// </summary>
    [HttpPatch("{id}/activate")]
    [Authorize(Policy = Policies.Backoffice)]
    [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<StationResponse>> ActivateAsync(
        string id, CancellationToken cancellationToken)
    {
        var updated = await _stations.ActivateAsync(id, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Takes a station out of service. Refused with 422 while the station still
    /// holds active energy reservations.
    /// </summary>
    [HttpPatch("{id}/deactivate")]
    [Authorize(Policy = Policies.Backoffice)]
    [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StationResponse>> DeactivateAsync(
        string id, CancellationToken cancellationToken)
    {
        var updated = await _stations.DeactivateAsync(id, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Updates how many battery storage slots are free. Grid operators own this
    /// task, so back-office officers and operators may both perform it.
    /// </summary>
    [HttpPatch("{id}/battery-slots")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StationResponse>> UpdateBatterySlotsAsync(
        string id, [FromBody] UpdateBatterySlotsRequest request, CancellationToken cancellationToken)
    {
        var updated = await _stations.UpdateBatterySlotsAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Lists the booking windows offered by a station, optionally within a
    /// date range. Any signed in user may read these because prosumers need
    /// them to choose a slot.
    /// </summary>
    [HttpGet("{id}/slots")]
    [ProducesResponseType(typeof(IReadOnlyList<SlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SlotResponse>>> ListSlotsAsync(
        string id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var slots = await _slots.ListByStationAsync(id, from, to, isActive, cancellationToken);
        return Ok(slots);
    }

    /// <summary>
    /// Creates a booking window at a station. Staff only.
    /// </summary>
    [HttpPost("{id}/slots")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType(typeof(SlotResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SlotResponse>> CreateSlotAsync(
        string id, [FromBody] CreateSlotRequest request, CancellationToken cancellationToken)
    {
        var created = await _slots.CreateAsync(id, request, cancellationToken);

        // The created window is addressed through the slots controller, so the
        // Location header points there rather than back at this controller.
        return CreatedAtAction(
            actionName: nameof(SlotsController.GetByIdAsync),
            controllerName: "Slots",
            routeValues: new { id = created.Id },
            value: created);
    }
}
