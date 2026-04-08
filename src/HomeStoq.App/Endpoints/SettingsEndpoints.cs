using HomeStoq.Shared.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace HomeStoq.App.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/config/current - returns raw config.ini content
        app.MapGet("/api/config/current", (IConfiguration config, ILogger<Program> logger) =>
        {
            try
            {
                var configPath = PathHelper.ResolveConfigIni();
                if (!File.Exists(configPath))
                {
                    return Results.NotFound("config.ini not found");
                }

                var content = File.ReadAllText(configPath);
                return Results.Text(content, "text/plain");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to read config.ini");
                return Results.Problem("Failed to read configuration");
            }
        });

        // GET /api/config/future - returns raw config.ini.future content
        app.MapGet("/api/config/future", (ILogger<Program> logger) =>
        {
            try
            {
                var futurePath = PathHelper.ResolveFutureConfigPath();
                if (!File.Exists(futurePath))
                {
                    return Results.NotFound("No pending configuration");
                }

                var content = File.ReadAllText(futurePath);
                return Results.Text(content, "text/plain");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to read config.ini.future");
                return Results.Problem("Failed to read future configuration");
            }
        });

        // POST /api/config/future - saves raw content to config.ini.future
        app.MapPost("/api/config/future", async ([FromBody] string content, ILogger<Program> logger) =>
        {
            try
            {
                var futurePath = PathHelper.ResolveFutureConfigPath();
                var directory = Path.GetDirectoryName(futurePath);
                
                // Ensure data directory exists
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Validate that content is valid INI format (basic check)
                if (!IsValidIniContent(content))
                {
                    return Results.BadRequest("Invalid INI format. Please ensure the content follows INI syntax with [Sections] and Key=Value pairs.");
                }

                // Add header comment
                var header = $"# HomeStoq Future Configuration\n# Saved: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}\n# This file will be merged with config.ini on next restart\n# Values here take precedence over config.ini\n\n";
                
                var fullContent = header + content;
                await File.WriteAllTextAsync(futurePath, fullContent, Encoding.UTF8);

                logger.LogInformation("Future configuration saved to {Path}", futurePath);
                return Results.Ok(new { saved = true, path = futurePath });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save config.ini.future");
                return Results.Problem("Failed to save future configuration");
            }
        });

        // DELETE /api/config/future - deletes future config file
        app.MapDelete("/api/config/future", (ILogger<Program> logger) =>
        {
            try
            {
                var futurePath = PathHelper.ResolveFutureConfigPath();
                if (File.Exists(futurePath))
                {
                    File.Delete(futurePath);
                    logger.LogInformation("Future configuration deleted from {Path}", futurePath);
                }

                return Results.Ok(new { deleted = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete config.ini.future");
                return Results.Problem("Failed to delete future configuration");
            }
        });

        // GET /api/config/status - returns status of future config
        app.MapGet("/api/config/status", (ILogger<Program> logger) =>
        {
            try
            {
                var futurePath = PathHelper.ResolveFutureConfigPath();
                var exists = File.Exists(futurePath);
                DateTime? modified = null;
                
                if (exists)
                {
                    modified = File.GetLastWriteTimeUtc(futurePath);
                }

                return Results.Ok(new 
                { 
                    hasPendingChanges = exists,
                    modifiedAt = modified,
                    path = futurePath
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check config status");
                return Results.Problem("Failed to check configuration status");
            }
        });

        return app;
    }

    private static bool IsValidIniContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return true; // Empty content is valid (will just not override anything)
        }

        var lines = content.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith(';'))
            {
                continue;
            }

            // Check for section header
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                continue;
            }

            // Check for key=value pair
            if (trimmed.Contains('='))
            {
                continue;
            }

            // If we get here, line is invalid
            return false;
        }

        // Content is valid if it's empty or has at least one section or key=value
        return true;
    }
}
