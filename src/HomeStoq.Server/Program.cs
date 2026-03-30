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

app.Run();