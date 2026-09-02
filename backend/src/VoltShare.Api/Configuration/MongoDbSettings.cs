// -----------------------------------------------------------------------------
// File        : MongoDbSettings.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Configuration
// Description : Strongly typed representation of the "MongoDb" section of
//               appsettings.json. Bound once at start-up and injected wherever
//               the database connection details are required.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

namespace VoltShare.Api.Configuration;

/// <summary>
/// Holds the MongoDB connection details read from configuration.
/// The real connection string is supplied through user secrets or an
/// environment variable so that credentials are never committed to Git.
/// </summary>
public class MongoDbSettings
{
    // Name of the configuration section this class is bound to.
    public const string SectionName = "MongoDb";

    // Full MongoDB connection string (Atlas SRV string or a local mongodb:// URI).
    public string ConnectionString { get; set; } = string.Empty;

    // Name of the database that holds all four VoltShare collections.
    public string DatabaseName { get; set; } = "VoltShareDb";
}
