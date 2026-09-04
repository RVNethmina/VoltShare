// -----------------------------------------------------------------------------
// File        : MongoContext.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Data
// Description : Single entry point to the MongoDB database. Exposes the four
//               collections required by the specification and creates the
//               indexes that the business rules and queries depend on.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using VoltShare.Api.Configuration;
using VoltShare.Api.Models;

namespace VoltShare.Api.Data;

/// <summary>
/// Wraps the MongoDB driver and hands out typed collection handles.
///
/// Registered as a singleton because IMongoClient maintains its own internal
/// connection pool and is designed to be created once for the lifetime of the
/// application. Creating a client per request would exhaust the pool.
/// </summary>
public class MongoContext
{
    // The database handle is created lazily rather than in the constructor.
    // If it were created in the constructor, a missing connection string would
    // make dependency injection itself fail and every endpoint - including the
    // health check - would return an unhelpful 500. Building it on first use
    // lets the health endpoint catch the problem and report a clean 503.
    private readonly Lazy<IMongoDatabase> _database;

    /// <summary>
    /// Stores the configuration and prepares the lazy database connection.
    /// </summary>
    public MongoContext(IOptions<MongoDbSettings> options)
    {
        // Read the settings that were bound from configuration at start-up.
        var settings = options.Value;

        _database = new Lazy<IMongoDatabase>(() =>
        {
            // Reject a missing connection string with a message that says
            // exactly how to fix it, instead of a confusing driver error.
            if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new InvalidOperationException(
                    "MongoDb:ConnectionString is not configured. Set it with " +
                    "dotnet user-secrets set \"MongoDb:ConnectionString\" \"<your connection string>\"");
            }

            // One client for the whole application, then resolve the database.
            var client = new MongoClient(settings.ConnectionString);
            return client.GetDatabase(settings.DatabaseName);
        });
    }

    /// <summary>
    /// The underlying database, opened on first access.
    /// </summary>
    private IMongoDatabase Database => _database.Value;

    // The four collections named by the assignment marking scheme.
    // The string literals are the actual collection names seen in Compass.

    public IMongoCollection<User> Users =>
        Database.GetCollection<User>("users");

    public IMongoCollection<SolarStation> SolarStations =>
        Database.GetCollection<SolarStation>("solarStationInfo");

    public IMongoCollection<EnergyBookingSlot> BookingSlots =>
        Database.GetCollection<EnergyBookingSlot>("energyBookingSlots");

    public IMongoCollection<EnergyReservation> Reservations =>
        Database.GetCollection<EnergyReservation>("energyReservations");

    /// <summary>
    /// Confirms the database is reachable by issuing a ping command. Used by
    /// the health endpoint so a broken Atlas connection is obvious immediately
    /// rather than surfacing later as a failing feature.
    /// </summary>
    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        // The ping command is the cheapest possible round trip to the server.
        await Database.RunCommandAsync<BsonDocument>(
            new BsonDocument("ping", 1), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates every index the application relies on. Index creation in MongoDB
    /// is idempotent, so this is safe to run on every start-up.
    /// </summary>
    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        // Email must be unique because it is the login identifier.
        await Users.Indexes.CreateOneAsync(
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Name = "ux_users_email", Unique = true }),
            cancellationToken: cancellationToken);

        // Listing users by role and activation state is the most common query
        // behind the back-office user and pending activation screens.
        await Users.Indexes.CreateOneAsync(
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Role).Ascending(u => u.IsActive),
                new CreateIndexOptions { Name = "ix_users_role_isActive" }),
            cancellationToken: cancellationToken);

        // Station code is a human readable unique reference.
        await SolarStations.Indexes.CreateOneAsync(
            new CreateIndexModel<SolarStation>(
                Builders<SolarStation>.IndexKeys.Ascending(s => s.Code),
                new CreateIndexOptions { Name = "ux_stations_code", Unique = true }),
            cancellationToken: cancellationToken);

        // A 2dsphere index is what makes the $near query in the nearby stations
        // endpoint possible. Without it MongoDB refuses the query outright.
        await SolarStations.Indexes.CreateOneAsync(
            new CreateIndexModel<SolarStation>(
                Builders<SolarStation>.IndexKeys.Geo2DSphere(s => s.Location),
                new CreateIndexOptions { Name = "ix_stations_location_2dsphere" }),
            cancellationToken: cancellationToken);

        // A station cannot offer two windows starting at the same instant.
        await BookingSlots.Indexes.CreateOneAsync(
            new CreateIndexModel<EnergyBookingSlot>(
                Builders<EnergyBookingSlot>.IndexKeys
                    .Ascending(s => s.StationId)
                    .Ascending(s => s.StartTimeUtc),
                new CreateIndexOptions { Name = "ux_slots_station_start", Unique = true }),
            cancellationToken: cancellationToken);

        // Reservation number is the friendly reference shown to prosumers.
        await Reservations.Indexes.CreateOneAsync(
            new CreateIndexModel<EnergyReservation>(
                Builders<EnergyReservation>.IndexKeys.Ascending(r => r.ReservationNo),
                new CreateIndexOptions { Name = "ux_reservations_no", Unique = true }),
            cancellationToken: cancellationToken);

        // Backs the prosumer booking history and dashboard counts.
        await Reservations.Indexes.CreateOneAsync(
            new CreateIndexModel<EnergyReservation>(
                Builders<EnergyReservation>.IndexKeys
                    .Ascending(r => r.ProsumerNic)
                    .Descending(r => r.ReservationStartUtc),
                new CreateIndexOptions { Name = "ix_reservations_nic_start" }),
            cancellationToken: cancellationToken);

        // Backs the station deactivation check and the operator dashboard.
        await Reservations.Indexes.CreateOneAsync(
            new CreateIndexModel<EnergyReservation>(
                Builders<EnergyReservation>.IndexKeys
                    .Ascending(r => r.StationId)
                    .Ascending(r => r.Status)
                    .Ascending(r => r.ReservationStartUtc),
                new CreateIndexOptions { Name = "ix_reservations_station_status_start" }),
            cancellationToken: cancellationToken);

        await CreateQrTokenIndexAsync(cancellationToken);
    }

    // Name of the unique index over the reservation QR tokens.
    private const string QrTokenIndexName = "ux_reservations_qrToken";

    /// <summary>
    /// Creates the unique index over QR tokens.
    ///
    /// This deliberately uses a partial filter rather than a sparse index. A
    /// sparse index only skips documents where the field is entirely absent,
    /// so as soon as two unapproved bookings both stored an explicit null
    /// token they collided and the second insert failed with a duplicate key
    /// error. A partial filter indexes only documents whose qrToken is a real
    /// string, which is exactly the set that has to be unique.
    ///
    /// If an older index with the same name but different options still
    /// exists, MongoDB refuses to recreate it, so it is dropped and rebuilt.
    /// </summary>
    private async Task CreateQrTokenIndexAsync(CancellationToken cancellationToken)
    {
        var model = new CreateIndexModel<EnergyReservation>(
            Builders<EnergyReservation>.IndexKeys.Ascending(r => r.QrToken),
            new CreateIndexOptions<EnergyReservation>
            {
                Name = QrTokenIndexName,
                Unique = true,
                PartialFilterExpression =
                    new BsonDocument("qrToken", new BsonDocument("$type", "string"))
            });

        try
        {
            await Reservations.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
        }
        catch (MongoCommandException ex) when (IsIndexConflict(ex))
        {
            // An index of this name already exists with different options, so
            // it is replaced with the corrected definition.
            await Reservations.Indexes.DropOneAsync(QrTokenIndexName, cancellationToken);
            await Reservations.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Recognises the "an index of this name already exists with different
    /// options" failure.
    ///
    /// Matching on the numeric code alone was not enough: MongoDB reports this
    /// as IndexOptionsConflict (85) or IndexKeySpecsConflict (86) depending on
    /// which part of the definition differs, and the deployment reported
    /// neither in a form the earlier check recognised, so the rebuild was
    /// silently skipped. The message is therefore checked as well.
    /// </summary>
    private static bool IsIndexConflict(MongoCommandException exception)
    {
        if (exception.Code is 85 or 86)
        {
            return true;
        }

        if (exception.CodeName is "IndexOptionsConflict" or "IndexKeySpecsConflict")
        {
            return true;
        }

        return exception.Message.Contains("same name as the requested index",
            StringComparison.OrdinalIgnoreCase);
    }
}
