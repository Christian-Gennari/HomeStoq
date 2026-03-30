using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HomeStoq.Server.Repositories;
using HomeStoq.Server.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<InventoryRepository>();
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddHostedService<VoiceSyncWorker>();

var app = builder.Build();

// Static files
app.UseDefaultFiles();
app.UseStaticFiles();

// Endpoints
app.MapGet("/api/inventory", async (InventoryRepository repository) => 
    Results.Ok(await repository.GetInventoryAsync()));

app.MapPost("/api/inventory/update", async (ManualUpdateRequest request, InventoryRepository repository) =>
{
    await repository.UpdateInventoryItemAsync(request.ItemName, request.QuantityChange, request.Price, request.Currency, "Manual");
    return Results.Ok();
});

app.MapPost("/api/receipts/scan", async (IFormFile receiptImage, GeminiService gemini, InventoryRepository repository) =>
{
    using var stream = new MemoryStream();
    await receiptImage.CopyToAsync(stream);
    var items = await gemini.ProcessReceiptImageAsync(stream.ToArray(), receiptImage.ContentType);
    
    if (items == null) return Results.Problem("Gemini failed to process the image.");

    foreach (var item in items)
    {
        await repository.UpdateInventoryItemAsync(item.ItemName, item.Quantity, item.Price, source: "Receipt");
    }

    return Results.Ok(items);
}).DisableAntiforgery();

app.MapGet("/api/insights/shopping-list", async (InventoryRepository repository, GeminiService gemini) =>
{
    var history = await repository.GetHistoryAsync(30);
    var inventory = await repository.GetInventoryAsync();

    var historyJson = JsonSerializer.Serialize(history);
    var inventoryJson = JsonSerializer.Serialize(inventory);

    var cacheKey = ComputeHash($"{historyJson}|{inventoryJson}");
    var cachedResponse = await repository.GetAiCacheAsync(cacheKey);

    if (cachedResponse != null)
    {
        return Results.Ok(JsonSerializer.Deserialize<JsonElement>(cachedResponse));
    }

    var result = await gemini.GenerateShoppingListAsync(historyJson, inventoryJson);
    if (result != null)
    {
        await repository.SetAiCacheAsync(cacheKey, result, TimeSpan.FromHours(12));
        return Results.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    return Results.Problem("Gemini failed to generate shopping list.");
});

static string ComputeHash(string input)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
    return Convert.ToHexString(bytes);
}

app.Run();

public record ManualUpdateRequest(string ItemName, double QuantityChange, double? Price, string? Currency);