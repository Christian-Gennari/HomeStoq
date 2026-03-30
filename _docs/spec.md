# Technical Specification: HomeStoq

## Version 3.2

**Tech Stack:** ASP.NET Core 10 (Minimal APIs), Vanilla JS/HTML/CSS (Frontend), Docker (Alpine), SQLite (Database), Google Tasks (Voice Queue), Gemini 1.5 Flash (AI/OCR).

---

## 1. Core Components

The application is built as a lightweight ASP.NET Core 10 application using Minimal APIs (`dotnet new web`), running in a Docker container on the local network.

### 1.1. Background Services (No user interaction required)

**VoiceSyncWorker:** Runs in a loop (e.g. every 10 seconds) as a `BackgroundService`. Fetches new tasks from Google Tasks (uses `@default` list by default, configurable via `GOOGLE_TASKS_LIST_NAME` environment variable), sends the text to `GeminiService` for parsing (Item + Action), updates the database, then deletes the task.

### 1.2. Core Services (Business Logic)

**InventoryRepository:** Abstraction layer over SQLite. Handles all reads and writes to the three tables: `Inventory`, `History`, and `AiCache`.

**GeminiService:** Handles all communication with Google AI Studio. Has three distinct responsibilities (prompts):

- **Voice Parsing:** Text → JSON Action (e.g. "used the last milk" → Remove Milk)
- **Receipt OCR:** Image (Base64) → JSON Array (extracts items from a receipt, maps them to standardized names, parses price if available)
- **Predictive Analysis:** History JSON → Purchase suggestions based on consumption patterns

### 1.3. Frontend / Dashboard (User Interface)

The web UI is intentionally minimal and served statically from `wwwroot`. Built entirely in Vanilla JavaScript, HTML5, and CSS3 — no heavy frameworks (React/Angular) to keep complexity low. Accessible via local IP (e.g. `http://192.168.1.x:8080`).

- **View 1: Inventory & Manual Control.** Displays current stock with simple DOM manipulation. Allows manual quantity adjustments. Price is optional when updating manually.
- **View 2: Receipt Upload.** A standard HTML form (`<input type="file" accept="image/*" capture="environment">`) to photograph or upload receipts. Sends the image asynchronously via `fetch()` to the Minimal API endpoint.
- **View 3: Smart Shopping List.** Displays AI suggestions based on what is low and historical consumption patterns. Shows estimated total cost based on historical prices.

---

## 2. API Endpoints (Minimal APIs in Program.cs)

Endpoints are defined directly in `Program.cs` for maximum performance and minimal boilerplate:

| Method | Endpoint                      | Description                                                                                                                       |
| ------ | ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `GET`  | `/api/inventory`              | Returns current stock                                                                                                             |
| `POST` | `/api/inventory/update`       | Manual adjustment of an item (+/-). Price optional.                                                                               |
| `POST` | `/api/receipts/scan`          | Accepts an image file (multipart/form-data), sends to Gemini for OCR (incl. price extraction), mass-updates inventory and history |
| `GET`  | `/api/insights/shopping-list` | Returns an AI-generated shopping list based on the last 30 days of history                                                        |

---

## 3. Receipt Scanning Code Structure (Example)

Integrating Gemini with images in C# requires converting the image to Base64 and sending it with the correct mime type in the JSON payload.

```csharp
// Program.cs (Minimal API endpoint)
app.MapPost("/api/receipts/scan", async (IFormFile receiptImage, GeminiService gemini) =>
{
    using var stream = new MemoryStream();
    await receiptImage.CopyToAsync(stream);
    var items = await gemini.ProcessReceiptImageAsync(stream.ToArray(), receiptImage.ContentType);
    // Add items to SQLite via InventoryRepository here...
    return Results.Ok(items);
}).DisableAntiforgery(); // Simple local API


// GeminiService.cs
public async Task<List<PantryItem>> ProcessReceiptImageAsync(byte[] imageBytes, string mimeType = "image/jpeg")
{
    var base64Image = Convert.ToBase64String(imageBytes);
    var prompt = @"You are a system that reads grocery receipts.
    Analyze the image and list all relevant food items with their prices.
    Ignore deposits, plastic bags, discounts, and totals.
    Map items to generic names (e.g. 'Organic Free Range Eggs 12pk' -> 'Eggs').
    Extract the price for each item if available (e.g. '2.99').
    Respond ONLY with a JSON array in this format:
    [ { ""ItemName"": ""Eggs"", ""Quantity"": 1, ""Price"": 2.99 }, { ""ItemName"": ""Milk"", ""Quantity"": 2, ""Price"": null } ]";

    var requestBody = new
    {
        contents = new[]
        {
            new
            {
                parts = new object[]
                {
                    new { text = prompt },
                    new { inlineData = new { mimeType = mimeType, data = base64Image } }
                }
            }
        }
    };

    // Send to https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent
    // Deserialize response to List<PantryItem> and pass to InventoryRepository.
}
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

**Example rows:**

| Timestamp        | ItemName | Action | Quantity | Price | TotalPrice | Currency | Source  |
| ---------------- | -------- | ------ | -------- | ----- | ---------- | -------- | ------- |
| 2026-03-29 18:30 | Milk     | Add    | 2        | 24.90 | 49.80      | SEK      | Receipt |
| 2026-03-29 18:30 | Rigatoni | Add    | 1        | 19.90 | 19.90      | SEK      | Receipt |
| 2026-03-31 08:15 | Milk     | Remove | 1        |       |            |          | Voice   |
| 2026-04-02 19:00 | Rigatoni | Remove | 1        |       |            |          | Manual  |

### AiCache

Caches AI responses to avoid redundant Gemini calls for identical inputs (e.g. repeated shopping list requests within the same day).

| Column      | Type       | Notes                                       |
| ----------- | ---------- | ------------------------------------------- |
| `Id`        | INTEGER PK | Auto-increment                              |
| `CacheKey`  | TEXT       | Hash of input (e.g. SHA256 of history JSON) |
| `Response`  | TEXT       | Raw JSON response from Gemini               |
| `CreatedAt` | TEXT       | ISO 8601                                    |
| `ExpiresAt` | TEXT       | ISO 8601 — TTL for invalidation             |

---

## 5. Deployment via Docker (Old Laptop)

**OS:** Any Linux distro or Windows with Docker Desktop.

**Dockerfile:** Use `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` for runtime and `mcr.microsoft.com/dotnet/sdk:10.0-alpine` for build.

The SQLite database file is persisted via a mounted volume so data survives container restarts.

```bash
docker run -d \
  -p 8080:8080 \
  -v /path/to/creds:/app/creds \
  -v /path/to/data:/app/data \
  -e GOOGLE_APPLICATION_CREDENTIALS=/app/creds/key.json \
  -e GEMINI_API_KEY=your_key \
  -e DATABASE_PATH=/app/data/homestoq.db \
  homestoq
```

---

## 6. Project Structure

```
HomeStoq/
├── src/
│   └── HomeStoq.Server/
│       ├── Program.cs
│       ├── Services/
│       │   ├── GeminiService.cs
│       │   └── VoiceSyncWorker.cs
│       ├── Repositories/
│       │   └── InventoryRepository.cs
│       ├── Models/
│       │   └── PantryItem.cs
│       └── wwwroot/
│           ├── index.html
│           ├── app.js
│           └── style.css
├── _docs/
├── Dockerfile
├── .gitignore
└── README.md
```
