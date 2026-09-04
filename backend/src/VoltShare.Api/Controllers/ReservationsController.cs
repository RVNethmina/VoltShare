// -----------------------------------------------------------------------------
// File        : ReservationsController.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Controllers
// Description : Energy reservation endpoints covering the whole booking
//               lifecycle: request, change, cancel, approve, reject, the QR
//               payload for the prosumer, and the operator scan and completion.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoltShare.Api.Dtos;
using VoltShare.Api.Models;
using VoltShare.Api.Repositories;
using VoltShare.Api.Security;
using VoltShare.Api.Services;

namespace VoltShare.Api.Controllers;

/// <summary>
/// Power trading reservations. Ownership is decided in the service from the
/// caller context built here, so a prosumer can never reach another person's
/// booking whatever they put in the query string.
/// </summary>
[ApiController]
[Route("api/v1/reservations")]
[Produces("application/json")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservations;

    /// <summary>
    /// Receives the reservation service from dependency injection.
    /// </summary>
    public ReservationsController(IReservationService reservations)
    {
        _reservations = reservations;
    }

    /// <summary>
    /// Builds the caller context from the validated access token. The identity
    /// always comes from the token, never from the request body.
    /// </summary>
    private CallerContext Caller => new(User.GetUserId(), User.GetRole());

    /// <summary>
    /// Searches bookings. Staff see everything; a prosumer sees only their own
    /// records regardless of the filters they supply.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ReservationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReservationResponse>>> SearchAsync(
        [FromQuery] string? nic,
        [FromQuery] string? stationId,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? q,
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var query = new ReservationQuery
        {
            ProsumerNic = nic,
            StationId = stationId,
            Status = status,
            FromUtc = from,
            ToUtc = to,
            Search = q,
            Limit = limit
        };

        var results = await _reservations.SearchAsync(query, Caller, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Lists the bookings still awaiting approval, for the operator dashboard.
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType(typeof(IReadOnlyList<ReservationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReservationResponse>>> ListPendingAsync(
        CancellationToken cancellationToken)
    {
        var query = new ReservationQuery { Status = ReservationStatus.Pending };
        var results = await _reservations.SearchAsync(query, Caller, cancellationToken);

        return Ok(results);
    }

    /// <summary>
    /// Returns one booking.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationResponse>> GetByIdAsync(
        string id, CancellationToken cancellationToken)
    {
        var reservation = await _reservations.GetByIdAsync(id, Caller, cancellationToken);
        return Ok(reservation);
    }

    /// <summary>
    /// Creates a booking. Refused with 422 when the window is in the past or
    /// more than seven days away, and with 409 when the window is full.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReservationSummaryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReservationSummaryResponse>> CreateAsync(
        [FromBody] CreateReservationRequest request, CancellationToken cancellationToken)
    {
        var summary = await _reservations.CreateAsync(request, Caller, cancellationToken);

        return CreatedAtAction(
            nameof(GetByIdAsync), new { id = summary.Reservation.Id }, summary);
    }

    /// <summary>
    /// Changes a booking. Refused with 422 inside the twelve hour notice period.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ReservationSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReservationSummaryResponse>> UpdateAsync(
        string id, [FromBody] UpdateReservationRequest request, CancellationToken cancellationToken)
    {
        var summary = await _reservations.UpdateAsync(id, request, Caller, cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// Cancels a booking. Refused with 422 inside the twelve hour notice period.
    /// </summary>
    [HttpPatch("{id}/cancel")]
    [ProducesResponseType(typeof(ReservationSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReservationSummaryResponse>> CancelAsync(
        string id, CancellationToken cancellationToken)
    {
        var summary = await _reservations.CancelAsync(id, Caller, cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// Approves a pending booking and issues its transaction QR token.
    /// </summary>
    [HttpPatch("{id}/approve")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReservationResponse>> ApproveAsync(
        string id, CancellationToken cancellationToken)
    {
        var reservation = await _reservations.ApproveAsync(id, cancellationToken);
        return Ok(reservation);
    }

    /// <summary>
    /// Rejects a pending booking and returns its place to the slot.
    /// </summary>
    [HttpPatch("{id}/reject")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReservationResponse>> RejectAsync(
        string id, CancellationToken cancellationToken)
    {
        var reservation = await _reservations.RejectAsync(id, cancellationToken);
        return Ok(reservation);
    }

    /// <summary>
    /// Returns the QR payload for an approved booking. The mobile application
    /// renders this string as a QR code; it carries no personal data.
    /// </summary>
    [HttpGet("{id}/qr")]
    [ProducesResponseType(typeof(QrCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<QrCodeResponse>> GetQrCodeAsync(
        string id, CancellationToken cancellationToken)
    {
        var qr = await _reservations.GetQrCodeAsync(id, Caller, cancellationToken);
        return Ok(qr);
    }

    /// <summary>
    /// Verifies a scanned QR token against the server and returns the booking
    /// it identifies. Nothing is changed, so the operator can check the details
    /// before deciding to finalise the transfer.
    /// </summary>
    [HttpPost("verify-qr")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReservationResponse>> VerifyQrAsync(
        [FromBody] VerifyQrRequest request, CancellationToken cancellationToken)
    {
        var reservation = await _reservations.VerifyQrAsync(request.Token, cancellationToken);
        return Ok(reservation);
    }

    /// <summary>
    /// Finalises the energy transfer. A second attempt on the same booking is
    /// refused, which is what makes a QR code single use.
    /// </summary>
    [HttpPost("{id}/complete")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType(typeof(ReservationSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReservationSummaryResponse>> CompleteAsync(
        string id, CancellationToken cancellationToken)
    {
        var summary = await _reservations.CompleteAsync(id, Caller, cancellationToken);
        return Ok(summary);
    }
}
