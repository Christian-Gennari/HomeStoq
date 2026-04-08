# Development Guide

📖 **Documentation Index:**
[README](../README.md) | [Getting Started](01-getting-started.md) | [Configuration](03-configuration.md) | [Architecture](04-architecture.md) | [Scraper](07-scraper.md) | Development

This guide is for developers who want to understand, modify, or extend HomeStoq.

---

## Project Structure

```
HomeStoq/
├── src/
│   ├── HomeStoq.App/                    # Main web application
│   │   ├── Program.cs                   # Entry point, routing, DI setup
│   │   ├── Endpoints/                   # API endpoint modules
│   │   │   ├── InventoryEndpoints.cs
│   │   │   ├── ReceiptEndpoints.cs
│   │   │   ├── AiEndpoints.cs
│   │   │   └── ShoppingListEndpoints.cs # Shopping Buddy API
│   │   ├── Services/
│   │   │   ├── GeminiService.cs        # All AI interactions
│   │   │   └── PromptProvider.cs       # AI prompt templates
│   │   ├── Repositories/
│   │   │   └── InventoryRepository.cs  # Database access layer
│   │   ├── Models/
│   │   │   └── Entities.cs             # EF Core entities
│   │   ├── Data/
│   │   │   ├── PantryDbContext.cs      # Database context
│   │   │   └── DbInitializer.cs        # Seed data
│   │   └── wwwroot/                    # Frontend files
│   │       ├── index.html
│   │       ├── js/                     # Main logic and i18n
│   │       ├── css/                    # Shared styles
│   │       └── features/               # Modularized feature folders
│   │           ├── inventory/          # JS, CSS for stock view
│   │           ├── scan/               # JS, CSS for receipt scanning
│   │           ├── receipts/           # JS, CSS for receipt history
│   │           ├── chat/               # JS, CSS for pantry chat
│   │           └── shopping/           # JS, CSS for Shopping Buddy
│   ├── HomeStoq.Shared/                 # Shared library
│   │   ├── DTOs/                        # Data Transfer Objects
│   │   └── Utils/
│   │       └── PathHelper.cs
│   └── HomeStoq.Plugins/
│       └── HomeStoq.Plugins.GoogleKeepScraper/
│           ├── Program.cs
│           ├── GoogleKeepScraperWorker.cs
│           └── Services/
│               ├── IBrowserService.cs
│               ├── CdpBrowserService.cs
│               ├── PlaywrightBrowserService.cs
│               ├── IKeepListProcessor.cs
│               ├── KeepListProcessor.cs
│               ├── ChromeLocator.cs
│               └── BrowserUtils.cs
├── data/                                # SQLite database
├── _docs/                               # Documentation
├── config.ini                           # Configuration
└── package.json                         # npm scripts
```

---

## Building & Development

HomeStoq is designed to be developed entirely within Docker. This ensures a consistent environment and removes the need for local .NET installations.

### Build & Run (Docker — Recommended)

```bash
# Start the full stack with hot-reloading
npm run dev

# Rebuild all containers from scratch (after .cs changes)
npm run docker:build

# Stop all containers
npm run docker:down

# Clean the environment (removes volumes and builds)
npm run docker:clean
```

> **Ports during development:** HomeStoq runs on port 5050 (default), noVNC on 6080. Check `config.ini` if you changed the API port.

**Hot Reloading:** Both the main App and the Scraper use `dotnet watch` inside their containers. Any changes to C#, CSS, or JS files will trigger an automatic reload.

### Understanding Docker Base Image Choices

**Why Alpine for API but Ubuntu for Scraper?**

```dockerfile
# API Dockerfile - Minimal, secure
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine

# Scraper Dockerfile - Full ecosystem needed
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble
```

**API uses Alpine because:**
- Simple HTTP server, no special native dependencies
- SQLite works fine with `musl libc`
- Smaller attack surface (~100MB vs ~1.1GB)
- Standard for production .NET containers

**Scraper uses Ubuntu Noble (24.04) because:**
- Chrome requires `glibc` (not available in Alpine's `musl`)
- X11/Xvfb are Ubuntu/Debian packages
- Playwright officially supports Ubuntu-based images
- Browser automation needs full Linux desktop libraries

### Build & Run (Local - Not Recommended)

```bash
# Start API + Scraper locally
npm run dev:local

# Start just the API
npm run api:local

# Start just the scraper
npm run scraper:local
```

---

## Key Concepts

### Minimal APIs

HomeStoq uses ASP.NET Core's Minimal APIs — endpoints are defined directly in `Program.cs`:

```csharp
app.MapGet("/api/inventory", async (InventoryRepository repo) =>
{
    var items = await repo.GetInventoryAsync();
    return Results.Ok(items);
});
```

**Benefits:**
- Less boilerplate
- Easier to follow request flow
- Performance equivalent to controllers

### Dependency Injection

Services are registered in `Program.cs`:

```csharp
builder.Services.AddSingleton<InventoryRepository>();
builder.Services.AddSingleton<GeminiService>();
```

And injected where needed:

```csharp
app.MapPost("/api/chat", async (GeminiService gemini, ChatRequestDto request) =>
{
    var reply = await gemini.ChatAsync(request);
    return Results.Ok(reply);
});
```

### Repository Pattern

All database access goes through `InventoryRepository`:

```csharp
public class InventoryRepository
{
    private readonly PantryDbContext _db;
    
    public async Task<List<InventoryItem>> GetInventoryAsync()
    {
        return await _db.Inventory
            .OrderBy(i => i.Category)
            .ThenBy(i => i.ItemName)
            .ToListAsync();
    }
}
```

**Benefits:**
- Centralized query logic
- Easy to test (mock the repository)
- Consistent error handling

---

## Adding New Features

### 1. Add a New API Endpoint

Edit `src/HomeStoq.App/Program.cs`:

```csharp
// Add new endpoint
app.MapGet("/api/stats/consumption", async (InventoryRepository repo) =>
{
    var stats = await repo.GetConsumptionStatsAsync(days: 30);
    return Results.Ok(stats);
});
```

Or create a separate endpoint module (cleaner):

```csharp
// src/HomeStoq.App/Endpoints/StatsEndpoints.cs
public static class StatsEndpoints
{
    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stats/consumption", async (InventoryRepository repo) =>
        {
            // ...
        });
        
        return app;
    }
}

// In Program.cs
app.MapStatsEndpoints();
```

### 2. Add a Database Migration

```bash
# Create migration
dotnet ef migrations add AddConsumptionStats --project src/HomeStoq.App

# Apply to database
dotnet ef database update --project src/HomeStoq.App

# Remove last migration (if mistake)
dotnet ef migrations remove --project src/HomeStoq.App
```

**Important:** For SQLite, some migrations are destructive (can't alter columns). Plan accordingly.

### 3. Modify the Frontend

Edit files in `src/HomeStoq.App/wwwroot/`:

```html
<!-- index.html -->
<div x-show="view === 'new-feature'">
    <!-- New view content -->
</div>
```

```javascript
// app.js
function pantryApp() {
    return {
        // Add new state
        newFeatureData: [],
        
        // Add new method
        async loadNewFeature() {
            const res = await fetch('/api/new-feature');
            this.newFeatureData = await res.json();
        }
    };
}
```

### 4. Add a New Config Option

1. **Add to `config.ini`:**
   ```ini
   [App]
   NewFeature=enabled
   ```

2. **Read in `Program.cs`:**
   ```csharp
   var newFeature = builder.Configuration["App:NewFeature"] ?? "disabled";
   builder.Services.AddSingleton(new NewFeatureOptions { Enabled = newFeature == "enabled" });
   ```

3. **Inject where needed:**
    ```csharp
    app.MapGet("/api/new-feature", (NewFeatureOptions options) =>
    {
        if (!options.Enabled) return Results.NotFound();
        // ...
    });
    ```

### Handling Cross-Platform Paths (PathHelper)

The `PathHelper` utility detects Docker containers and adjusts paths:

```csharp
private static string DetectRepoRoot()
{
    // Check for Docker container
    if (File.Exists("/.dockerenv"))
        return "/app";

    // Use assembly metadata for local development
    return typeof(PathHelper)
        .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(a => a.Key == "RepoRoot")
        .Value!;
}
```

**Why this matters:**
- Windows builds embed Windows paths in assembly metadata
- Docker containers run on Linux and need `/app` paths
- The `/.dockerenv` file is a standard Docker indicator
- This allows the same code to work locally and in containers

---

## Working with the Scraper

### Adding a New Browser Action

Edit `src/HomeStoq.Plugins/HomeStoq.Plugins.GoogleKeepScraper/Services/KeepListProcessor.cs`:

```csharp
private async Task DoSomethingNewAsync(IPage page)
{
    var element = page.GetByText("New Button").First;
    if (await element.IsVisibleAsync())
    {
        await BrowserUtils.MoveMouseToElementAsync(page, element);
        await element.ClickAsync();
        await BrowserUtils.HumanDelayAsync(1000, 200);
    }
}
```

### Modifying Polling Behavior

Edit `src/HomeStoq.Plugins/HomeStoq.Plugins.GoogleKeepScraper/GoogleKeepScraperWorker.cs`:

The `ExecuteAsync` method contains the main loop. Modify the polling logic there.

### Adding a New Browser Mode

1. Implement `IBrowserService`:
   ```csharp
   public class MyBrowserService : IBrowserService
   {
       // Implement all interface methods
   }
   ```

2. Register in `Program.cs`:
   ```csharp
   if (browserMode == "my-mode")
       services.AddSingleton<IBrowserService, MyBrowserService>();
   ```

---

## Code Style Guidelines

### C#

- Use implicit usings (configured in `.csproj`)
- Prefer `var` when type is obvious
- Use file-scoped namespaces
- Async methods should end in `Async`
- Cancellation tokens: pass them through, don't ignore

Example:
```csharp
namespace HomeStoq.App.Services;

public class MyService
{
    public async Task<Result> DoSomethingAsync(string input, CancellationToken ct = default)
    {
        var result = await _client.GetAsync(input, ct);
        return result;
    }
}
```

### JavaScript

- Use `const` or `let`, never `var`
- Async/await for asynchronous operations
- Destructuring where it improves readability
- Comments for complex logic

Example:
```javascript
async function loadData() {
    const res = await fetch('/api/data');
    const { items, total } = await res.json();
    return { items, total };
}
```

---

## Testing

### Current State

HomeStoq has minimal test coverage. Priority areas for testing:

1. **GeminiService** — Mock API responses, test parsing logic
2. **InventoryRepository** — Test database operations
3. **Scraper** — Hard to test (depends on Google Keep UI)

### Running Tests (When Added)

```bash
# Run all tests
dotnet test

# Run with verbosity
dotnet test --verbosity normal

# Run specific test
dotnet test --filter "FullyQualifiedName~GeminiService"
```

### Test Structure (Recommended)

```
tests/
├── HomeStoq.App.Tests/
│   ├── Services/
│   │   └── GeminiServiceTests.cs
│   └── Repositories/
│       └── InventoryRepositoryTests.cs
└── HomeStoq.Plugins.GoogleKeepScraper.Tests/
    └── Services/
        └── BrowserUtilsTests.cs
```

---

## Debugging

### Enable Debug Logging

Add to `.env`:
```bash
LOG_LEVEL=Debug
```

Or set environment variable:
```bash
# Windows
$env:LOG_LEVEL="Debug"

# Linux/Mac
export LOG_LEVEL=Debug
```

### Debug the Scraper

The scraper outputs detailed logs. Watch for:
- `[INFO]` — Normal operation
- `[WARN]` — Something unexpected but continuing
- `[ERROR]` — Something failed

### Debug the API

Use browser DevTools Network tab to inspect:
- Request/response payloads
- Headers
- Timing

Or use curl:
```bash
curl -v http://localhost:5050/api/inventory
```

> Replace 5050 with your configured port from `config.ini`.

---

## Contributing

### Before You Start

1. Check [GitHub Issues](https://github.com/Christian-Gennari/HomeStoq/issues) for existing work
2. Open an issue to discuss major changes
3. Fork the repository

### Pull Request Process

1. Create a feature branch: `git checkout -b feature/my-feature`
2. Make your changes
3. Test locally: `npm run dev` and verify
4. Commit with clear messages
5. Push to your fork
6. Open a Pull Request

### PR Guidelines

- Keep changes focused (one feature per PR)
- Update documentation if needed
- Add tests for new functionality
- Ensure `dotnet build` passes with no warnings

---

## Common Tasks

### Reset Database

```bash
# Stop HomeStoq
rm data/homestoq.db

# Restart — database will be recreated
npm run dev
```

### Clear AI Cache

```bash
# Delete cache entries older than now
sqlite3 data/homestoq.db "DELETE FROM AiCache WHERE ExpiresAt < datetime('now');"
```

### View Raw Database

```bash
sqlite3 data/homestoq.db ".tables"
sqlite3 data/homestoq.db "SELECT * FROM Inventory;"
```

### Simulate Voice Command (Testing)

```bash
curl -X POST http://localhost:5050/api/voice/command \
  -H "Content-Type: application/json" \
  -d '{"text":"slut på mjölk"}'
```

> Replace 5050 with your configured port from `config.ini`.

---

## Resources

### Documentation
- [Minimal APIs in ASP.NET Core](https://docs.microsoft.com/aspnet/core/fundamentals/minimal-apis)
- [Entity Framework Core](https://docs.microsoft.com/ef/core/)
- [Playwright for .NET](https://playwright.dev/dotnet/)
- [Alpine.js](https://alpinejs.dev/)

### Tools
- [SQLite Browser](https://sqlitebrowser.org/) — GUI for database
- [Playwright Inspector](https://playwright.dev/dotnet/docs/inspector) — Debug browser automation

---

## Questions?

- Open a [GitHub Issue](https://github.com/Christian-Gennari/HomeStoq/issues)
- Check existing [GitHub Issues](https://github.com/Christian-Gennari/HomeStoq/issues) for answers
