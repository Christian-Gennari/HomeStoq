using HomeStoq.App.Repositories;
using HomeStoq.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HomeStoq.App.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/settings",
            (IConfiguration config) =>
            {
                return Results.Ok(new { Language = config["App:Language"] ?? "English" });
            }
        );

        app.MapGet(
            "/api/inventory",
            async (InventoryRepository repository, ILogger<InventoryRepository> logger) =>
            {
                logger.LogInformation("GET /api/inventory requested.");
                var inventory = await repository.GetInventoryAsync();
                return Results.Ok(inventory);
            }
        );

        app.MapPost(
            "/api/inventory/update",
            async (ManualUpdateRequestDto request, InventoryRepository repository, ILogger<InventoryRepository> logger) =>
            {
                logger.LogInformation(
                    "POST /api/inventory/update for {ItemName} ({Change})",
                    request.ItemName,
                    request.QuantityChange
                );
                await repository.UpdateInventoryItemAsync(
                    request.ItemName,
                    request.QuantityChange,
                    request.Price,
                    request.Currency,
                    "Manual"
                );
                return Results.Ok();
            }
        );

        return app;
    }
}
