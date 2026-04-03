using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HomeStoq.Contracts;
using HomeStoq.Contracts.SharedUtils;
using HomeStoq.App.Repositories;
using HomeStoq.App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Google.GenAI;

// Load environment variables from .env if present
DotNetEnv.Env.Load(PathHelper.ResolveEnvFile());

// Apply HostUrl from config.ini before the web host builder reads ASPNETCORE_URLS
// This ensures config.ini takes precedence over any existing environment variable
var configIniPath = PathHelper.ResolveConfigIni();
if (File.Exists(configIniPath))
{
    var hostUrl = System
        .Text.RegularExpressions.Regex.Match(File.ReadAllText(configIniPath), @"HostUrl\s*=\s*(.+)")
        .Groups[1]
        .Value.Trim();
    if (!string.IsNullOrEmpty(hostUrl))
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", hostUrl);
    }
}

var builder = WebApplication.CreateBuilder(args);

// Add config.ini as a configuration source
builder.Configuration.AddIniFile(configIniPath, optional: true, reloadOnChange: true);

// Register AI Client
var apiKey = builder.Configuration["GEMINI_API_KEY"] ?? throw new InvalidOperationException("GEMINI_API_KEY not configured");
var modelId = builder.Configuration["AI:Model"] ?? "gemini-3.1-flash-lite-preview";
var googleClient = new Client(apiKey: apiKey);
builder.Services.AddSingleton<IChatClient>(sp =>
    googleClient.AsIChatClient(modelId)
        .AsBuilder()
        .UseFunctionInvocation()
        .Build());

builder.Services.AddSingleton<InventoryRepository>();
builder.Services.AddSingleton<GeminiService>();
builder.Services.AddHttpClient();

var app = builder.Build();

// Static files
app.UseDefaultFiles();
app.UseStaticFiles();

// Endpoints
app.MapGet(
    "/api/settings",
    (IConfiguration config) =>
    {
        return Results.Ok(new { Language = config["App:Language"] ?? "English" });
    }
);

app.MapGet(
    "/api/inventory",
    async (InventoryRepository repository, ILogger<Program> logger) =>
    {
        logger.LogInformation("GET /api/inventory requested.");
        var inventory = await repository.GetInventoryAsync();
        return Results.Ok(inventory);
    }
);

app.MapPost(
    "/api/inventory/update",
    async (ManualUpdateRequest request, InventoryRepository repository, ILogger<Program> logger) =>
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

app.MapPost(
        "/api/receipts/scan",
        async (
            IFormFile receiptImage,
            GeminiService gemini,
            InventoryRepository repository,
            ILogger<Program> logger,
            IConfiguration config
        ) =>
        {
            logger.LogInformation(
                "POST /api/receipts/scan: Received file {FileName} ({ContentType})",
                receiptImage.FileName,
                receiptImage.ContentType
            );
            using var stream = new MemoryStream();
            await receiptImage.CopyToAsync(stream);

            var inventory = await repository.GetInventoryAsync();
            var itemNames = inventory.Select(i => i.ItemName).ToList();

            var items = await gemini.ProcessReceiptImageAsync(
                stream.ToArray(),
                receiptImage.ContentType,
                itemNames
            );

            if (items == null || !items.Any())
            {
                logger.LogWarning("Gemini failed to process the receipt image.");
                return Results.Problem("Gemini failed to process the image.");
            }

            var storeName = ResolveStoreName(receiptImage.FileName, config["App:Language"] ?? "English");
            var totalAmount = items.Sum(i => i.Price ?? 0);
            var receiptId = await repository.CreateReceiptAsync(storeName, totalAmount);

            logger.LogInformation("Gemini identified {Count} items from receipt. Saved as Receipt #{Id}", items.Count, receiptId);
            
            foreach (var item in items)
            {
                await repository.UpdateInventoryItemAsync(
                    item.ItemName,
                    item.Quantity,
                    item.Price,
                    source: "Receipt",
                    category: item.Category,
                    receiptId: receiptId,
                    expandedName: item.ExpandedName
                );
            }

            return Results.Ok(items);
        }
    )
    .DisableAntiforgery();

app.MapGet("/api/receipts", async (InventoryRepository repository) => 
{
    var receipts = await repository.GetReceiptsAsync();
    return Results.Ok(receipts);
});

app.MapGet("/api/receipts/{id}/items", async (long id, InventoryRepository repository) => 
{
    var items = await repository.GetReceiptItemsAsync(id);
    return Results.Ok(items);
});

app.MapPost("/api/chat", async (ChatRequest request, GeminiService gemini) => 
{
    var response = await gemini.ChatAsync(request.Message, request.History);
    return Results.Ok(response);
});

app.MapGet(
    "/api/insights/shopping-list",
    async (InventoryRepository repository, GeminiService gemini, ILogger<Program> logger) =>
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
    }
);

app.MapPost(
    "/api/voice/command",
    async (
        [FromBody] VoiceCommandRequest? request,
        GeminiService gemini,
        InventoryRepository repository,
        ILogger<Program> logger
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

static string ResolveStoreName(string fileName, string language)
{
    var unknownFallback = language == "Swedish" ? "Okänd" : "Unknown";

    if (string.IsNullOrWhiteSpace(fileName)) return unknownFallback;

    var name = Path.GetFileNameWithoutExtension(fileName).Trim();

    name = System.Text.RegularExpressions.Regex.Replace(name, @"[\s_\-]+", " ");

    name = System.Text.RegularExpressions.Regex.Replace(name, @"\d{4}[-_]\d{2}[-_]\d{2}.*$", "").Trim();

    name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+\d+$", "").Trim();

    if (string.IsNullOrWhiteSpace(name) || name.Length < 3)
        return unknownFallback;

    var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (words.Length > 6)
        name = string.Join(" ", words.Take(6));

    return name;
}

static string ComputeHash(string input)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
    return Convert.ToHexString(bytes);
}

app.Run();
