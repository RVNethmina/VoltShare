// -----------------------------------------------------------------------------
// File        : ExceptionHandlingMiddleware.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Middleware
// Description : Catches every unhandled exception in the request pipeline and
//               converts it into a consistent ProblemDetails response. This is
//               what allows both clients to display the reason a request was
//               refused without duplicating any business logic themselves.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace VoltShare.Api.Middleware;

/// <summary>
/// Translates application exceptions into HTTP status codes and a JSON body of
/// a single shape, so the React and Android clients only need one error reader.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>
    /// Stores the next middleware in the pipeline and the logger.
    /// </summary>
    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Runs the rest of the pipeline and converts any escaping exception into
    /// a ProblemDetails response.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            // A deliberate application error. Logged at warning level because
            // it is an expected outcome, not a fault in the service.
            _logger.LogWarning("{Code}: {Message}", ex.Code, ex.Message);
            await WriteProblemAsync(context, MapStatusCode(ex), ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            // Anything else is a genuine fault. Log the detail for the
            // developer but never return it to the caller.
            _logger.LogError(ex, "Unhandled exception while processing {Path}.", context.Request.Path);
            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ErrorCodes.Unexpected,
                "An unexpected error occurred while processing the request.");
        }
    }

    /// <summary>
    /// Chooses the HTTP status code that matches the kind of application error.
    /// </summary>
    private static int MapStatusCode(AppException exception) => exception switch
    {
        NotFoundException => StatusCodes.Status404NotFound,
        ValidationException => StatusCodes.Status400BadRequest,
        ForbiddenException => StatusCodes.Status403Forbidden,
        ConflictException => StatusCodes.Status409Conflict,

        // A well formed request that breaks a business rule is reported as
        // 422 Unprocessable Content, which distinguishes a refused booking
        // from malformed input.
        BusinessRuleViolationException => StatusCodes.Status422UnprocessableEntity,

        _ => StatusCodes.Status500InternalServerError
    };

    /// <summary>
    /// Writes the ProblemDetails JSON body and sets the response status code.
    /// </summary>
    private static async Task WriteProblemAsync(
        HttpContext context, int statusCode, string code, string message)
    {
        // If the response has already started there is nothing safe to write,
        // so abandon the attempt rather than throwing a second exception.
        if (context.Response.HasStarted)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = code,
            Detail = message,
            Instance = context.Request.Path
        };

        // The stable error code is repeated in an extension member so clients
        // can read it without parsing the title.
        problem.Extensions["errorCode"] = code;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    // Matches the camelCase convention used by the rest of the API responses.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
