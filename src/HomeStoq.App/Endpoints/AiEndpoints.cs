using HomeStoq.App.Repositories;
using HomeStoq.App.Services;
using HomeStoq.App.Utils;
using HomeStoq.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json;

namespace HomeStoq.App.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat", async (ChatRequestDto request, GeminiService gemini) => 
        {
            var response = await gemini.ChatAsync(request.Message, request.History);
            return Results.Ok(response);
        });

        app.MapGet(
            "/api/insights/shopping-list",
            async (InventoryRepository repository, GeminiService gemini, ILogger<GeminiService> logger) =>
            {
                logger.LogInformation("GET /api/insights/shopping-list requested.");
                var history = await repository.GetHistoryAsync(30);
                var inventory = await repository.GetInventoryAsync();

                var historyJson = JsonSerializer.Serialize(history);
                var inventoryJson = JsonSerializer.Serialize(inventory);

                var cacheKey = HashHelper.ComputeHash($"{historyJson}|{inventoryJson}");
                var cachedResponse = await repository.GetAiCacheAsync(cacheKey);

                if (cachedResponse != null)
                {
                    logger.LogInformation("Returning cached shopping list suggestions.");
                    return Results.Ok(JsonSerializer.Deserialize<JsonElement>(cachedResponse));
                }

                logger.LogInformation("Generating new shopping list suggestions via Gemini...");
                var result = await gemini.GenerateShoppingListAsync(historyJson, inventoryJson);
                if (result != null)
                {
                    await repository.SetAiCacheAsync(cacheKey, result, TimeSpan.FromHours(12));
                    return Results.Ok(JsonSerializer.Deserialize<JsonElement>(result));
                }

                logger.LogWarning("Gemini failed to generate shopping list suggestions.");
                return Results.Problem("Gemini failed to generate shopping list.");
            }
        );

        app.MapPost(
            "/api/voice/command",
            async (
                [FromBody] VoiceCommandRequestDto? request,
                GeminiService gemini,
                InventoryRepository repository,
                ILogger<GeminiService> logger
            ) =>
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Text))
                {
                    logger.LogWarning("POST /api/voice/command: Empty or missing text");
                    return Results.BadRequest("Text is required");
                }

                logger.LogInformation("POST /api/voice/command: {Text}", request.Text);

                var inventory = await repository.GetInventoryAsync();
                var itemNames = inventory.Select(i => i.ItemName).ToList();

                var parsedList = await gemini.ParseVoiceCommandAsync(request.Text, itemNames);

                if (parsedList == null || !parsedList.Any())
                {
                    logger.LogWarning("Could not parse voice command: {Text}", request.Text);
                    return Results.BadRequest("Could not parse voice command");
                }

                foreach (var parsed in parsedList)
                {
                    if (string.IsNullOrWhiteSpace(parsed.ItemName))
                        continue;

                    if (
                        !string.Equals(parsed.Action, "Add", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(parsed.Action, "Remove", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        logger.LogWarning(
                            "Unknown action: {Action} for command: {Text}",
                            parsed.Action,
                            request.Text
                        );
                        continue;
                    }

                    var isRemove = string.Equals(
                        parsed.Action,
                        "Remove",
                        StringComparison.OrdinalIgnoreCase
                    );
                    var quantityChange = isRemove ? -parsed.Quantity : parsed.Quantity;

                    logger.LogInformation(
                        "Applying voice action: {Action} {Quantity} {Item}",
                        parsed.Action,
                        parsed.Quantity,
                        parsed.ItemName
                    );

                    await repository.UpdateInventoryItemAsync(
                        parsed.ItemName,
                        quantityChange,
                        source: "Voice",
                        category: parsed.Category
                    );
                }

                return Results.Ok();
            }
        );

        return app;
    }
}
