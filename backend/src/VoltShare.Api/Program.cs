// -----------------------------------------------------------------------------
// File        : Program.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Application entry point
// Description : Builds and starts the web service. Registers configuration, the
//               MongoDB context, repositories, services, JWT authentication,
//               role based authorisation policies, CORS and Swagger, then wires
//               the HTTP request pipeline in the correct order.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using VoltShare.Api.Configuration;
using VoltShare.Api.Data;
using VoltShare.Api.Middleware;
using VoltShare.Api.Models;
using VoltShare.Api.Repositories;
using VoltShare.Api.Security;
using VoltShare.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// Configuration binding
// Each settings class is bound to its section so the rest of the application
// depends on typed objects rather than raw configuration strings.
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
// connection pool meant to live for the lifetime of the process. Repositories
// are scoped: they are cheap wrappers created per request.
// -----------------------------------------------------------------------------
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IStationRepository, StationRepository>();
builder.Services.AddScoped<ISlotRepository, SlotRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<DatabaseSeeder>();

// -----------------------------------------------------------------------------
// Security services
// -----------------------------------------------------------------------------
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IQrTokenService, QrTokenService>();

// -----------------------------------------------------------------------------
// Business services
// This is where the FAT service pattern is realised: every rule the clients
// depend on is implemented behind one of these interfaces.
// -----------------------------------------------------------------------------
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IStationService, StationService>();
builder.Services.AddScoped<ISlotService, SlotService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// -----------------------------------------------------------------------------
// Authentication
// Tokens are validated on every request against the same issuer, audience and
// signing key that JwtTokenService used to create them.
// -----------------------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                  ?? new JwtSettings();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(jwtSettings.Key)
                    ? new string('0', 32)   // placeholder; JwtTokenService fails fast if unset
                    : jwtSettings.Key)),

            // Reject expired tokens with no grace period, so the 8 hour expiry
            // written into the token is the expiry actually enforced.
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,

            // Tell the framework which claims carry the name and the role, so
            // User.IsInRole and the policies below work as expected.
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    });

// -----------------------------------------------------------------------------
// Authorisation policies
// Named policies keep role names out of the controllers and make the intent of
// each endpoint obvious at a glance.
// -----------------------------------------------------------------------------
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Backoffice, policy =>
        policy.RequireRole(UserRoles.Backoffice))
    .AddPolicy(Policies.GridOperator, policy =>
        policy.RequireRole(UserRoles.GridOperator))
    .AddPolicy(Policies.Prosumer, policy =>
        policy.RequireRole(UserRoles.Prosumer))
    .AddPolicy(Policies.Staff, policy =>
        policy.RequireRole(UserRoles.Backoffice, UserRoles.GridOperator));

// -----------------------------------------------------------------------------
// Cross origin requests
// The React web application is served from a different origin to the API, so
// the browser refuses its calls unless those origins are allowed here.
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
// -----------------------------------------------------------------------------
builder.Services.AddControllers(options =>
{
    // By default MVC removes the "Async" suffix when it registers an action
    // name, which makes CreatedAtAction(nameof(GetByIdAsync)) fail to find its
    // own route. Keeping the suffix lets nameof() stay accurate and avoids
    // hard coded action name strings that silently break when a method is
    // renamed.
    options.SuppressAsyncSuffixInActionNames = false;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Note: Swashbuckle 10 uses OpenAPI.NET 2.x, where the document types live
    // directly in the Microsoft.OpenApi namespace rather than in .Models.
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VoltShare API",
        Version = "v1",
        Description = "Smart Solar Microgrid Trading System - central web service. " +
                      "All business logic for the web and Android clients lives here."
    });

    // Adds the Authorize button to Swagger UI so a bearer token can be pasted
    // in and every protected endpoint demonstrated during the viva.
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste only the token returned by /api/v1/auth/login."
    };

    options.AddSecurityDefinition("Bearer", scheme);

    // In Swashbuckle 10 the requirement is supplied as a factory that receives
    // the document being generated, rather than as a ready made object.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

var app = builder.Build();

// -----------------------------------------------------------------------------
// Start-up tasks: verify indexes and seed the first administrator.
// Wrapped in a try/catch so an unreachable database does not stop the service
// from starting; the health endpoint then reports the problem clearly.
// -----------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<MongoContext>();
        await context.CreateIndexesAsync();
        logger.LogInformation("MongoDB indexes verified successfully.");

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex,
            "Start-up database tasks did not complete. The API will still start; " +
            "check GET /api/v1/health for details.");
    }
}

// -----------------------------------------------------------------------------
// HTTP request pipeline. Order matters here:
// exception handling first so it can catch everything after it, then CORS,
// then authentication, then authorisation, then the endpoints themselves.
// -----------------------------------------------------------------------------
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger is exposed in every environment because the hosted IIS deployment has
// to be demonstrated to the marker.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "VoltShare API v1");
    options.DocumentTitle = "VoltShare API";
});

// HTTPS redirection is deliberately not enabled: the Android application talks
// to this service over plain HTTP on the local network during development, and
// a redirect would break those calls. IIS handles TLS termination in the
// hosted deployment instead.
app.UseCors(WebClientCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
