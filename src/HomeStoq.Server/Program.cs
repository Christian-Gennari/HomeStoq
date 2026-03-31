using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HomeStoq.Server.Repositories;
using HomeStoq.Server.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddIniFile("config.ini", optional: true);

builder.Services.AddSingleton<InventoryRepository>();
builder.Services.AddHttpClient<GeminiService>();

var app = builder.Build();

// Static files
app.UseDefaultFiles();
app.UseStaticFiles();

// Endpoints
app.MapGet("/api/inventory", async (InventoryRepository repository, ILogger<Program> logger) => 
{
    logger.LogInformation("GET /api/inventory requested.");
    var inventory = await repository.GetInventoryAsync();
    return Results.Ok(inventory);
});

app.MapPost("/api/inventory/update", async (ManualUpdateRequest request, InventoryRepository repository, ILogger<Program> logger) =>
{
    logger.LogInformation("POST /api/inventory/update for {ItemName} ({Change})", request.ItemName, request.QuantityChange);
    await repository.UpdateInventoryItemAsync(request.ItemName, request.QuantityChange, request.Price, request.Currency, "Manual");
    return Results.Ok();
});

app.MapPost("/api/receipts/scan", async (IFormFile receiptImage, GeminiService gemini, InventoryRepository repository, ILogger<Program> logger) =>
{
    logger.LogInformation("POST /api/receipts/scan: Received file {FileName} ({ContentType})", receiptImage.FileName, receiptImage.ContentType);
    using var stream = new MemoryStream();
    await receiptImage.CopyToAsync(stream);

    var inventory = await repository.GetInventoryAsync();
    var itemNames = inventory.Select(i => i.ItemName).ToList();

    var items = await gemini.ProcessReceiptImageAsync(stream.ToArray(), receiptImage.ContentType, itemNames);

    if (items == null)
    {
        logger.LogWarning("Gemini failed to process the receipt image.");
        return Results.Problem("Gemini failed to process the image.");
    }

    logger.LogInformation("Gemini identified {Count} items from receipt.", items.Count);
    foreach (var item in items)
    {
        await repository.UpdateInventoryItemAsync(item.ItemName, item.Quantity, item.Price, source: "Receipt");
    }

    return Results.Ok(items);
}).DisableAntiforgery();

app.MapGet("/api/insights/shopping-list", async (InventoryRepository repository, GeminiService gemini, ILogger<Program> logger) =>
{
    logger.LogInformation("GET /api/insights/shopping-list requested.");
    var history = await repository.GetHistoryAsync(30);
    var inventory = await repository.GetInventoryAsync();

    var historyJson = JsonSerializer.Serialize(history);
    var inventoryJson = JsonSerializer.Serialize(inventory);

    var cacheKey = ComputeHash($"{historyJson}|{inventoryJson}");
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
});

app.MapPost("/api/voice/command", async ([FromBody] VoiceCommandRequest? request, GeminiService gemini, InventoryRepository repository, ILogger<Program> logger) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.Text))
    {
        logger.LogWarning("POST /api/voice/command: Empty or missing text");
        return Results.BadRequest("Text is required");
    }
    
    logger.LogInformation("POST /api/voice/command: {Text}", request.Text);
    
    var inventory = await repository.GetInventoryAsync();
    var itemNames = inventory.Select(i => i.ItemName).ToList();
    
    var parsed = await gemini.ParseVoiceCommandAsync(request.Text, itemNames);
    
    if (parsed == null || string.IsNullOrWhiteSpace(parsed.ItemName))
    {
        logger.LogWarning("Could not parse voice command: {Text}", request.Text);
        return Results.BadRequest("Could not parse voice command");
    }
    
    if (!string.Equals(parsed.Action, "Add", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(parsed.Action, "Remove", StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("Unknown action: {Action} for command: {Text}", parsed.Action, request.Text);
        return Results.BadRequest("Unknown action");
    }
    
    var isRemove = string.Equals(parsed.Action, "Remove", StringComparison.OrdinalIgnoreCase);
    var quantityChange = isRemove ? -parsed.Quantity : parsed.Quantity;
    
    logger.LogInformation("Applying voice action: {Action} {Quantity} {Item}", parsed.Action, parsed.Quantity, parsed.ItemName);
    
    await repository.UpdateInventoryItemAsync(parsed.ItemName, quantityChange, source: "Voice");
    
    return Results.Ok();
});

static string ComputeHash(string input)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
    return Convert.ToHexString(bytes);
}

app.Run();

public record ManualUpdateRequest(string ItemName, double QuantityChange, double? Price, string? Currency);
public record VoiceCommandRequest(string Text);