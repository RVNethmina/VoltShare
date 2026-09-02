// -----------------------------------------------------------------------------
// File        : HealthController.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Controllers
// Description : Diagnostic endpoint used to prove that the service is running
//               on IIS and that the MongoDB connection is alive. Called during
//               deployment and demonstrated during the viva.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using Microsoft.AspNetCore.Mvc;
using VoltShare.Api.Data;

namespace VoltShare.Api.Controllers;

/// <summary>
/// Reports the state of the web service and its database connection.
/// Deliberately left unauthenticated so it can be checked from a phone or
/// another machine on the network without first obtaining a token.
/// </summary>
[ApiController]
[Route("api/v1/health")]
public class HealthController : ControllerBase
{
    private readonly MongoContext _context;
    private readonly ILogger<HealthController> _logger;

    /// <summary>
    /// Receives the database context and logger from dependency injection.
    /// </summary>
    public HealthController(MongoContext context, ILogger<HealthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Returns 200 when the API is running and MongoDB answers a ping,
    /// or 503 when the database cannot be reached.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        // Ping the database so the check proves real connectivity rather than
        // only proving that the web process itself is alive.
        try
        {
            await _context.PingAsync(cancellationToken);

            return Ok(new
            {
                status = "Healthy",
                service = "VoltShare.Api",
                database = "Connected",
                serverTimeUtc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            // Log the full error for the developer but return a short message
            // to the caller so connection details are never leaked.
            _logger.LogError(ex, "Health check failed: MongoDB is unreachable.");

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "Unhealthy",
                service = "VoltShare.Api",
                database = "Unreachable",
                serverTimeUtc = DateTime.UtcNow
            });
        }
    }
}
