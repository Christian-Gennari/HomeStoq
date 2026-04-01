# Technical Specification: HomeStoq

## Version 7.0

**Tech Stack:** ASP.NET Core 10 (Minimal APIs), Vanilla JS/HTML/CSS (Frontend), Docker (Alpine), SQLite (Database), Google Keep (Voice Queue), C# Playwright Scraper (Local Browser Automation), Gemini 3.1 Flash (AI/OCR).

---

## 1. Core Components

The application is built as a lightweight ASP.NET Core 10 application using Minimal APIs (`dotnet new web`), running in a Docker container on the local network. Voice commands are handled by a local C# Playwright scraper that automates Google Keep in a visible browser.

### 1.1. Local Scraper (No user interaction required after first login)

**keep-scraper (C# Playwright):** A .NET Worker that runs locally via `dotnet run --project src/KeepScraper`. Launches a visible Chromium browser, persists the session in `browser-profile/`. On first run, the user logs into Google Keep manually. Subsequent runs reuse the saved session. Polls every 10 seconds for unchecked items in a list named `KeepListName` (from `config.ini`, default: `"inköpslistan"`). For each unchecked item, it POSTs `{"Text": "item text"}` to the C# backend at `/api/voice/command`. On HTTP 200, it clicks the checkbox to mark the item done.

### 1.2. Configuration

**`.env`** — Secrets only:
- `GEMINI_API_KEY`

**`config.ini`** — Non-secret settings:
```ini
[App]
Language=Swedish

[Voice]
KeepListName=inköpslistan

[API]
BaseUrl=http://localhost:5000/api/voice/command
HostUrl=http://*:5000
```

| Setting | Description | Values |
| :--- | :--- | :--- |
| `Language` | Language for all AI parsing (voice, receipt, shopping list) | `Swedish` or `English` (default) |
| `KeepListName` | Google Keep list name to monitor | `inköpslistan` |
| `BaseUrl` | API endpoint for keep-scraper | `http://localhost:5000/api/voice/command` |
| `HostUrl` | Browser access URL and Server Bind URL | `http://*:5000` |

### 1.2.1. Advanced Overrides (Environment Variables)
- `DATABASE_PATH`: Overrides the default SQLite location (`data/homestoq.db`).
- `GEMINI_API_KEY`: Required for AI features.

The .NET backend reads `config.ini` via `AddIniFile()` and `.env` via default environment variable configuration. The Playwright scraper reads env vars passed by docker-compose.

### 1.3. Core Services (Business Logic)

**InventoryRepository:** Abstraction layer over SQLite. Uses `data/homestoq.db` by default (overridable via `DATABASE_PATH` env var). Handles all reads and writes to the three tables: `Inventory`, `History`, and `AiCache`.

**GeminiService:** Handles all communication with Google AI Studio. All prompts are language-aware based on the `Language` setting:

- **Voice Parsing:** Text → JSON Action. Swedish mode outputs Swedish item names; English mode outputs English.
- **Receipt OCR:** Image (Base64) → JSON Array. Swedish mode returns Swedish names, preserves brand names, maps English categories to Swedish. Passes existing inventory for name matching.
- **Predictive Analysis:** History JSON → Purchase suggestions. Swedish mode returns Swedish item names and reasons.

### 1.4. Frontend / Dashboard (User Interface)

The web UI is served statically from `wwwroot`. Built entirely in Vanilla JavaScript, HTML5, and CSS3. Accessible via `HostUrl` (default: `http://localhost:5000`).

- **View 1: Inventory & Manual Control.** Displays current stock with search/filter. Allows manual quantity adjustments with optimistic UI updates. Animated item transitions and toast notifications.
- **View 2: Receipt Upload.** File input for camera or upload. Sends image asynchronously via `fetch()` to the Minimal API endpoint. Shows progress bar during processing.
- **View 3: Smart Shopping List.** Displays AI suggestions based on 30-day history and current stock. Shows reason per suggestion.

---

## 2. API Endpoints (Minimal APIs in Program.cs)

Endpoints are defined directly in `Program.cs` for maximum performance and minimal boilerplate:

| Method | Endpoint                      | Description                                                                                                                       |
| ------ | ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `GET`  | `/api/inventory`              | Returns current stock                                                                                                             |
| `POST` | `/api/inventory/update`       | Manual adjustment of an item (+/-). Price optional.                                                                               |
| `POST` | `/api/receipts/scan`          | Accepts an image file (multipart/form-data), sends to Gemini for OCR (incl. price extraction), passes existing inventory for name matching, mass-updates inventory and history |
| `GET`  | `/api/insights/shopping-list` | Returns an AI-generated shopping list based on the last 30 days of history                                                        |
| `POST` | `/api/voice/command`          | Accepts `{"Text": "..."}` from keep-scraper, parses via Gemini (language-aware), updates inventory. Returns 200 OK or 400 Bad Request.             |

---

## 3. Receipt Scanning Code Structure (Example)

Integrating Gemini with images in C# requires converting the image to Base64 and sending it with the correct mime type in the JSON payload. The receipt scanner passes existing inventory names for matching.

```csharp
// Program.cs (Minimal API endpoint)
app.MapPost("/api/receipts/scan", async (IFormFile receiptImage, GeminiService gemini, InventoryRepository repository) =>
{
    using var stream = new MemoryStream();
    await receiptImage.CopyToAsync(stream);

    var inventory = await repository.GetInventoryAsync();
    var itemNames = inventory.Select(i => i.ItemName).ToList();

    var items = await gemini.ProcessReceiptImageAsync(stream.ToArray(), receiptImage.ContentType, itemNames);
    // Add items to SQLite via InventoryRepository here...
    return Results.Ok(items);
}).DisableAntiforgery();
```

---

## 4. Database Model: SQLite

Three tables. Schema is created on startup if it does not exist.

### Inventory

| Column      | Type       | Notes                              |
| ----------- | ---------- | ---------------------------------- |
| `Id`        | INTEGER PK | Auto-increment                     |
| `ItemName`  | TEXT       | Unique, normalized name            |
| `Quantity`  | REAL       | Current stock level                |
| `LastPrice` | REAL       | Most recent known price (nullable) |
| `Currency`  | TEXT       | e.g. `SEK`, `USD` (nullable)       |
| `UpdatedAt` | TEXT       | ISO 8601 timestamp                 |

### History _(Critical for prediction)_

Every receipt scan, voice command, or manual update appends a row here. The AI uses this data to identify consumption patterns and generate shopping list suggestions.

| Column       | Type       | Notes                        |
| ------------ | ---------- | ---------------------------- |
| `Id`         | INTEGER PK | Auto-increment               |
| `Timestamp`  | TEXT       | ISO 8601                     |
| `ItemName`   | TEXT       |                              |
| `Action`     | TEXT       | `Add` or `Remove`            |
| `Quantity`   | REAL       |                              |
| `Price`      | REAL       | Unit price (nullable)        |
| `TotalPrice` | REAL       | Price × Quantity (nullable)  |
| `Currency`   | TEXT       | nullable                     |
| `Source`     | TEXT       | `Receipt`, `Voice`, `Manual` |

### AiCache

Caches AI responses to avoid redundant Gemini calls for identical inputs.

| Column      | Type       | Notes                                       |
| ----------- | ---------- | ------------------------------------------- |
| `Id`        | INTEGER PK | Auto-increment                              |
| `CacheKey`  | TEXT       | Hash of input (e.g. SHA256 of history JSON) |
| `Response`  | TEXT       | Raw JSON response from Gemini               |
| `CreatedAt` | TEXT       | ISO 8601                                    |
| `ExpiresAt` | TEXT       | ISO 8601 — TTL for invalidation             |

---

## 5. Deployment via Docker

**OS:** Any Linux distro or Windows with Docker Desktop.

**Dockerfile:** Use `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` for runtime and `mcr.microsoft.com/dotnet/sdk:10.0-alpine` for build.

The SQLite database file is persisted via a mounted volume so data survives container restarts.

**Services:**
- **homestoq:** The ASP.NET Core backend serving the web UI and API on port 5000.
- **keep-scraper:** Runs locally via `dotnet run --project src/KeepScraper`. Uses a visible Chromium browser with persistent session.

Secrets are passed via `.env`.

```bash
docker compose up -d --build
```

Access the UI at `http://localhost`.

---

## 6. Project Structure

```
HomeStoq/
├── src/
│   ├── HomeStoq.Server/
│   │   ├── Program.cs
│   │   ├── Services/
│   │   │   └── GeminiService.cs
│   │   ├── Repositories/
│   │   │   └── InventoryRepository.cs
│   │   ├── Models/
│   │   │   ├── InventoryItem.cs
│   │   │   ├── HistoryEntry.cs
│   │   │   └── AiCacheEntry.cs
│   │   └── wwwroot/
│   │       ├── index.html
│   │       ├── app.js
│   │       └── style.css
│   └── KeepScraper/
│       ├── Program.cs
│       ├── KeepScraperWorker.cs
│       └── HomeStoq.KeepScraper.csproj
├── browser-profile/
├── _docs/
├── config.ini
├── docker-compose.yml
├── Dockerfile
├── .env-example
├── .gitignore
└── README.md
```
