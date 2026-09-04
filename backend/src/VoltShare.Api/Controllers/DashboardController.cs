// -----------------------------------------------------------------------------
// File        : DashboardController.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Controllers
// Description : Role based dashboard endpoints. Every figure is computed by the
//               service from live data, so no count is ever calculated or hard
//               coded inside the web or Android applications.
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
/// Summary figures for the prosumer home screen and the operator dashboard.
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
[Produces("application/json")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    /// <summary>
    /// Receives the dashboard service from dependency injection.
    /// </summary>
    public DashboardController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    /// <summary>
    /// Returns the counts for the signed in prosumer: bookings awaiting
    /// approval, approved bookings still to come, and the next one due.
    /// </summary>
    [HttpGet("prosumer")]
    [Authorize(Policy = Policies.Prosumer)]
    [ProducesResponseType(typeof(ProsumerDashboardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProsumerDashboardResponse>> GetProsumerDashboardAsync(
        CancellationToken cancellationToken)
    {
        // The NIC comes from the validated token, so a prosumer can only ever
        // see their own figures.
        var nic = User.GetUserId();
        var dashboard = await _dashboard.GetProsumerDashboardAsync(nic, cancellationToken);

        return Ok(dashboard);
    }

    /// <summary>
    /// Returns the operator figures: outstanding approvals, approved bookings
    /// still to come, transfers completed today and today's schedule.
    /// </summary>
    [HttpGet("operator")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType(typeof(OperatorDashboardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperatorDashboardResponse>> GetOperatorDashboardAsync(
        CancellationToken cancellationToken)
    {
        var dashboard = await _dashboard.GetOperatorDashboardAsync(cancellationToken);
        return Ok(dashboard);
    }
}
