// -----------------------------------------------------------------------------
// File        : Program.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Application entry point
// Description : Builds and starts the web service. Registers configuration,
//               the MongoDB context, CORS for the React client and Swagger,
//               then wires the HTTP request pipeline.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Configuration;
using VoltShare.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// Configuration binding
// Each settings class is bound to its section so that the rest of the
// application depends on typed objects rather than raw configuration strings.
// -----------------------------------------------------------------------------
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection(MongoDbSettings.SectionName));
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<QrSettings>(
    builder.Configuration.GetSection(QrSettings.SectionName));

// -----------------------------------------------------------------------------
// Data access
// MongoContext is a singleton because the underlying MongoClient owns a
// connection pool that is meant to live for the lifetime of the process.
// -----------------------------------------------------------------------------
builder.Services.AddSingleton<MongoContext>();

// -----------------------------------------------------------------------------
// Cross origin requests
// The React web application is served from a different origin to the API, so
// the browser will refuse its calls unless those origins are allowed here.
// The allowed origins are read from configuration so the deployed web address
// can be added without recompiling.
// -----------------------------------------------------------------------------
const string WebClientCorsPolicy = "WebClientCorsPolicy";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(WebClientCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// -----------------------------------------------------------------------------
// MVC controllers and API documentation
// Swagger gives a browsable UI that is used to demonstrate every endpoint
// during the demonstration and viva.
// -----------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Note: Swashbuckle 10 uses OpenAPI.NET 2.x, where the document types live
    // directly in the Microsoft.OpenApi namespace rather than in .Models.
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "VoltShare API",
        Version = "v1",
        Description = "Smart Solar Microgrid Trading System - central web service. " +
                      "All business logic for the web and Android clients lives here."
    });
});

var app = builder.Build();

// -----------------------------------------------------------------------------
// Index creation
// Runs once at start-up. Wrapped in a try/catch so that a missing or
// unreachable database does not stop the service from starting: the health
// endpoint then reports the problem clearly instead of the process dying.
// -----------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<MongoContext>();
        await context.CreateIndexesAsync();
        logger.LogInformation("MongoDB indexes verified successfully.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex,
            "Could not reach MongoDB at start-up, so indexes were not created. " +
            "The API will still start; check GET /health for details.");
    }
}

// -----------------------------------------------------------------------------
// HTTP request pipeline
// -----------------------------------------------------------------------------

// Swagger is exposed in every environment because the hosted IIS deployment
// has to be demonstrated to the marker.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "VoltShare API v1");
    options.DocumentTitle = "VoltShare API";
});

app.UseHttpsRedirection();
app.UseCors(WebClientCorsPolicy);
app.MapControllers();

app.Run();
