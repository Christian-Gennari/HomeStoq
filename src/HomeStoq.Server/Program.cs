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

app.MapGet("/", () => "Hello World!");

app.Run();