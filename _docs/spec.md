# Technical Specification: HomeStoq

## Version 7.0

**Tech Stack:** ASP.NET Core 10 (Minimal APIs), Vanilla JS/HTML/CSS (Frontend), Docker (Alpine), SQLite (Database), Google Keep (Voice Queue), C# Playwright Scraper (Local Browser Automation), Gemini 3.1 Flash (AI/OCR).

---

## 1. Core Components

The application is built as a lightweight ASP.NET Core 10 application using Minimal APIs (`dotnet new web`), running in a Docker container on the local network. Voice commands are handled by a local C# Playwright scraper that automates Google Keep in a visible browser.

### 1.1. Local Scraper (No user interaction required after first login)

**GoogleKeepScraper (C# Playwright):** A .NET Worker that runs locally via `npm run scraper` (or `dotnet run --project src/HomeStoq.Plugins/HomeStoq.Plugins.GoogleKeepScraper`). Launches a visible Chromium browser, persists the session in `browser-profile/`. On first run, the user logs into Google Keep manually. Subsequent runs reuse the saved session.

**Polling Behavior:**
- Polls every ~45 seconds (with ±15s random jitter to avoid pattern detection)
- Only runs during configurable **Active Hours** (default: `07-23`)
- Outside active hours, the scraper sleeps for 30-minute intervals before re-checking

**Processing Flow (per unchecked item):**
1. Extracts the item text from the checklist
2. POSTs `{"Text": "item text"}` to the C# backend at `/api/voice/command`
3. On HTTP 200: clicks the checkbox, then opens the "More" menu and selects **"Delete ticked items"** to permanently remove completed items
4. Supports both English and Swedish Google Keep UI

**Anti-Detection Measures:**
- WebGL vendor/renderer spoofing (mocks Intel GPU)
- Full `window.chrome` object mock (including `loadTimes`, `csi`, `runtime`)
- Navigator property overrides (`plugins`, `languages`, `hardwareConcurrency`, `deviceMemory`, `connection`)
- Random behavioral "noise" actions (10% chance per cycle): tab switching, scrolling, hovering over notes
- Persistent browser context with human-like timing

### 1.2. Configuration

**`.env`** — Secrets only:
- `GEMINI_API_KEY`

**`config.ini`** — Non-secret settings:
```ini
[App]
Language=Swedish

[Voice]
KeepListName=inköpslistan, inköpslista

[API]
BaseUrl=http://localhost:5000/api/voice/command

[Scraper]
ActiveHours=07-23
PollIntervalSeconds=45
PollIntervalJitterSeconds=15

HostUrl=http://*:5000
```

| Setting | Description | Values |
| :--- | :--- | :--- |
| `Language` | Language for all AI parsing (voice, receipt, shopping list) | `Swedish` or `English` (default) |
| `KeepListName` | Google Keep list name(s) to monitor (comma-separated) | String (default: `inköpslistan`) |
| `BaseUrl` | API endpoint for keep-scraper | `http://localhost:5000/api/voice/command` |
| `ActiveHours` | Scraper active hours (24h format) | `HH-HH` (default: `07-23`) |
| `PollIntervalSeconds` | Base polling interval | Seconds (default: `45`) |
| `PollIntervalJitterSeconds` | Random jitter added to interval | Seconds (default: `15`) |
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

### 1.5. Stealth & Anti-Detection

The keep-scraper implements multiple layers to avoid being flagged as a bot by Google:

#### Browser Fingerprint Spoofing
The scraper injects a JavaScript initialization script that patches the browser's runtime environment to appear as a genuine Chrome installation:

| Property | What it does | Why it matters |
| :--- | :--- | :--- |
| `navigator.webdriver` | Returns `undefined` instead of `true` | Selenium/Playwright set this to `true` automatically |
| WebGL `getParameter(37445/37446)` | Returns real Intel GPU vendor/renderer strings | Bot detectors check for "SwiftShader" or "llvmpipe" |
| `window.chrome` object | Full mock including `loadTimes()`, `csi()`, `runtime` | Chrome-only API that real browsers expose |
| `navigator.plugins` | Returns standard 3-plugin array with `item()`/`namedItem()` methods | Bot detectors check plugin count and methods |
| `navigator.languages` | Returns `['en-US', 'en']` | Consistent Accept-Language header |
| `navigator.hardwareConcurrency` | Returns `8` | Real machines report actual core counts |
| `navigator.deviceMemory` | Returns `8` | Real machines report actual memory |

#### Behavioral Noise
To break predictable 24/7 polling patterns, the scraper introduces human-like randomness:

- **Active Hours**: Only polls between configured hours (default `07-23`). Outside these hours, it sleeps for 30-minute intervals.
- **Random Delay Jitter**: Each poll cycle adds ±15 seconds of random delay to the base 45-second interval.
- **Random Actions**: 10% chance per cycle to perform one of:
  - Navigating to "Reminders" tab and back to "Notes"
  - Scrolling up and down the page
  - Hovering over random note cards

#### Session Management
- Persistent browser context (`browser-profile/`) saves cookies and localStorage
- First run requires manual login; subsequent runs reuse the session
- If session expires, the scraper detects it and waits for manual re-login

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
- **keep-scraper:** Runs locally via `npm run scraper` (or `dotnet run --project src/HomeStoq.Plugins/HomeStoq.Plugins.GoogleKeepScraper`). Uses a visible Chromium browser with persistent session.

Secrets are passed via `.env`.

```bash
docker compose up -d --build
```

Access the UI at `http://localhost`.

---

## 6. Project Structure

HomeStoq/
├── src/
│   ├── HomeStoq.App/             # Main API and Web App
│   │   ├── Program.cs
│   │   ├── Services/
│   │   │   └── GeminiService.cs
│   │   └── Repositories/
│   │       └── InventoryRepository.cs
│   ├── HomeStoq.Contracts/       # Shared models and communication contracts
│   │   ├── InventoryItem.cs
│   │   ├── HistoryEntry.cs
│   │   └── VoiceCommandRequest.cs
│   └── HomeStoq.Plugins/         # Container for plugins and scrapers
│       └── HomeStoq.Plugins.GoogleKeepScraper/
│           ├── Program.cs
│           └── GoogleKeepScraperWorker.cs
├── browser-profile/
...
├── _docs/
├── config.ini
├── docker-compose.yml
├── Dockerfile
├── .env-example
├── .gitignore
└── README.md
```
