using HomeStoq.App.Data;
using HomeStoq.App.Models;
using HomeStoq.Shared.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;

namespace HomeStoq.App.Data;

public class DbInitializer
{
    private readonly PantryDbContext _context;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(PantryDbContext context, ILogger<DbInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public void InitializeDatabase()
    {
        try
        {
            _logger.LogInformation("Initializing database via EF Core...");
            
            // Ensure the database and tables are created
            _context.Database.EnsureCreated();

            // Seeding logic
            if (!_context.Inventory.Any())
            {
                _logger.LogInformation("Database is empty. Seeding with initial data.");
                var seedFile = Path.Combine(PathHelper.RepoRoot, "data", "seed.sql");
                if (File.Exists(seedFile))
                {
                    var sql = File.ReadAllText(seedFile);
                    // EF Core can execute raw SQL for seeding
                    _context.Database.ExecuteSqlRaw(sql);
                    _logger.LogInformation("Database seeded successfully.");
                }
                else
                {
                    _logger.LogWarning($"Seed file not found at {seedFile}");
                }
            }

            _logger.LogInformation("Database initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to initialize database.");
            throw;
        }
    }
}
