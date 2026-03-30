using HomeStoq.Server.Repositories;
using HomeStoq.Server.Services;

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

app.Run();

public record ManualUpdateRequest(string ItemName, double QuantityChange, double? Price, string? Currency);