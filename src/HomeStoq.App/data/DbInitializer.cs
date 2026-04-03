using Dapper;
using HomeStoq.Shared.Utils;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;

namespace HomeStoq.App.Data;

public class DbInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(ILogger<DbInitializer> logger)
    {
        _logger = logger;

        var dbPath = PathHelper.ResolveDatabasePath();

        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={dbPath}";
    }

    public void InitializeDatabase()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            _logger.LogInformation(
                "Initializing database at {ConnectionString}",
                _connectionString
            );
            connection.Execute(
                @"
                CREATE TABLE IF NOT EXISTS Inventory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ItemName TEXT UNIQUE NOT NULL,
                    Quantity REAL NOT NULL DEFAULT 0,
                    Category TEXT,
                    LastPrice REAL,
                    Currency TEXT,
                    UpdatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Receipts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    StoreName TEXT NOT NULL,
                    TotalAmountPaid REAL NOT NULL
                );

                CREATE TABLE IF NOT EXISTS History (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    ItemName TEXT NOT NULL,
                    ExpandedName TEXT,
                    Action TEXT NOT NULL,
                    Quantity REAL NOT NULL,
                    Price REAL,
                    TotalPrice REAL,
                    Currency TEXT,
                    Source TEXT NOT NULL,
                    ReceiptId INTEGER REFERENCES Receipts(Id)
                );

                CREATE TABLE IF NOT EXISTS AiCache (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CacheKey TEXT UNIQUE NOT NULL,
                    Response TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    ExpiresAt TEXT NOT NULL
                );
            "
            );

            // Migration check for ReceiptId and ExpandedName if History existed before
            var historyColumns = connection
                .Query<string>("SELECT name FROM pragma_table_info('History')")
                .Select(c => c.ToLower());
            if (!historyColumns.Contains("receiptid"))
            {
                _logger.LogInformation("Migrating History table: Adding ReceiptId column.");
                connection.Execute(
                    "ALTER TABLE History ADD COLUMN ReceiptId INTEGER REFERENCES Receipts(Id)"
                );
            }
            if (!historyColumns.Contains("expandedname"))
            {
                _logger.LogInformation("Migrating History table: Adding ExpandedName column.");
                connection.Execute("ALTER TABLE History ADD COLUMN ExpandedName TEXT");
            }

            var count = connection.QuerySingle<int>("SELECT COUNT(*) FROM Inventory");
            if (count == 0)
            {
                _logger.LogInformation("Database is empty. Seeding with initial data.");
                var seedFile = System.IO.Path.Combine(HomeStoq.Shared.Utils.PathHelper.RepoRoot, "data", "seed.sql");
                if (System.IO.File.Exists(seedFile))
                {
                    var sql = System.IO.File.ReadAllText(seedFile);
                    connection.Execute(sql);
                    _logger.LogInformation("Database seeded successfully.");
                }
                else
                {
                    _logger.LogWarning($"Seed file not found at {seedFile}");
                }
            }

            _logger.LogInformation("Database tables initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to initialize database.");
            throw;
        }
    }
}
