# API Reference

📖 **Documentation Index:**
[README](../README.md) | [Getting Started](01-getting-started.md) | [Usage Guide](02-usage-guide.md) | [Configuration](03-configuration.md) | [Architecture](04-architecture.md) | API Reference | [Database](06-database.md)

This document describes all API endpoints available in HomeStoq. It's intended for developers integrating with or extending the system.

**Base URL:** `http://localhost:5000/api` (or your configured `HostUrl`)

---

## Overview

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/api/settings` | Get application settings |
| `GET` | `/api/inventory` | List current inventory |
| `POST` | `/api/inventory/update` | Update item quantity |
| `POST` | `/api/receipts/scan` | Scan a receipt image |
| `GET` | `/api/receipts` | List all receipts |
| `GET` | `/api/receipts/{id}/items` | Get items from a receipt |
| `GET` | `/api/insights/shopping-list` | Generate shopping suggestions |
| `POST` | `/api/voice/command` | Process voice command (scraper → API) |
| `POST` | `/api/chat` | AI chat with function calling |

---

## Authentication

Currently, HomeStoq has no authentication — it's designed to run on your private network. If you expose it to the internet, consider adding authentication at the reverse proxy level (nginx, Traefik, etc.).

---

## Endpoints

### GET /api/settings

Returns current application settings.

**Response:**
```json
{
  "language": "Swedish"
}
```

**Use case:** Frontend uses this to determine UI language and date formats.

---

### GET /api/inventory

Returns all items currently in inventory.

**Response:**
```json
[
  {
    "itemName": "Mjölk",
    "quantity": 2,
    "category": "Mejeri",
    "lastPrice": 15.50,
    "currency": "SEK",
    "updatedAt": "2026-04-03T10:30:00Z"
  }
]
```

**Fields:**
- `itemName` — Normalized item name
- `quantity` — Current stock level
- `category` — Item category (can be null)
- `lastPrice` — Most recent known price (can be null)
- `currency` — Price currency (can be null)
- `updatedAt` — ISO 8601 timestamp of last update

---

### POST /api/inventory/update

Manually adjust an item's quantity.

**Request:**
```json
{
  "itemName": "Mjölk",
  "quantityChange": -1,
  "price": 15.50,
  "currency": "SEK"
}
```

**Fields:**
- `itemName` (required) — Item to update
- `quantityChange` (required) — Amount to add (positive) or remove (negative)
- `price` (optional) — Unit price for this transaction
- `currency` (optional) — Currency code

**Response:**
- `200 OK` — Success
- `400 Bad Request` — Invalid input

**Behavior:**
- If item doesn't exist and `quantityChange` > 0, creates it
- If item doesn't exist and `quantityChange` < 0, returns error
- Creates a History entry for the change
- Updates `LastPrice` if price provided

---

### POST /api/receipts/scan

Upload and process a receipt image.

**Content-Type:** `multipart/form-data`

**Request:**
- Field: `receiptImage` — Image file (JPG, PNG, PDF)

**Response:**
```json
[
  {
    "itemName": "Mjölk",
    "quantity": 2,
    "price": 15.50,
    "currency": "SEK",
    "category": "Mejeri",
    "expandedName": "ICA Kärnmjölk 1L"
  }
]
```

**Fields:**
- `itemName` — Normalized name for inventory
- `quantity` — Quantity purchased
- `price` — Unit price
- `currency` — Currency code
- `category` — Suggested category
- `expandedName` — Full product name from receipt

**Behavior:**
- Saves image temporarily
- Sends to Gemini for OCR
- Creates Receipt record
- Updates inventory for each item
- Creates History entries linked to receipt
- Returns extracted items for confirmation

**Error cases:**
- `400 Bad Request` — Invalid image or no items found
- `500 Internal Server Error` — Gemini API failure

---

### GET /api/receipts

List all scanned receipts.

**Response:**
```json
[
  {
    "id": 1,
    "timestamp": "2026-04-03T10:30:00Z",
    "storeName": "ICA Supermarket",
    "totalAmountPaid": 485.50
  }
]
```

**Fields:**
- `id` — Receipt ID
- `timestamp` — When scanned
- `storeName` — Detected store name (can be null)
- `totalAmountPaid` — Sum of all item prices

**Sorting:** Newest first

---

### GET /api/receipts/{id}/items

Get individual items from a specific receipt.

**Parameters:**
- `id` (path) — Receipt ID

**Response:**
```json
[
  {
    "itemName": "Mjölk",
    "expandedName": "ICA Kärnmjölk 1L",
    "quantity": 2,
    "price": 15.50,
    "totalPrice": 31.00,
    "currency": "SEK"
  }
]
```

**Fields:**
- `itemName` — Normalized name
- `expandedName` — Full name from receipt
- `quantity` — Quantity
- `price` — Unit price
- `totalPrice` — Price × Quantity
- `currency` — Currency

---

### GET /api/insights/shopping-list

Generate AI shopping suggestions based on 30-day consumption history.

**Response:**
```json
[
  {
    "itemName": "Mjölk",
    "suggestedQuantity": 2,
    "reason": "You usually buy milk every 4 days, and it's been 5 days",
    "confidence": 0.85
  }
]
```

**Fields:**
- `itemName` — Item to buy
- `suggestedQuantity` — How much to get
- `reason` — Human-readable explanation
- `confidence` — AI certainty (0-1)

**Behavior:**
- Analyzes last 30 days of History
- Cross-references with current inventory
- Suggests items you might run out of soon
- Caches result for 1 hour

---

### POST /api/voice/command

Process a voice command from the scraper.

**Request:**
```json
{
  "text": "slut på mjölk"
}
```

**Response:**
- `200 OK` — Command parsed and executed
- `400 Bad Request` — Could not parse command

**Behavior:**
- Sends text to Gemini for parsing
- Gemini returns action (add/remove/set), item, quantity
- Updates inventory accordingly
- Creates History entry

**Supported Commands:**

| Input Pattern | Action | Example |
|--------------|--------|---------|
| "slut på [item]" | Remove 1 | "slut på mjölk" → -1 Mjölk |
| "köpte [n] [item]" | Add N | "köpte 5 ägg" → +5 Ägg |
| "använt allt [item]" | Set to 0 | "använt allt kaffe" → 0 Kaffe |
| "lägg till [item]" | Add 1 | "lägg till bröd" → +1 Bröd |
| "åt upp [n] [item]" | Remove N | "åt upp 2 bananer" → -2 Banan |

Language determined by `config.ini` `[App] Language` setting.

---

### POST /api/chat

AI chat with function calling for pantry queries.

**Request:**
```json
{
  "message": "How much milk do I have?",
  "history": [
    { "role": "user", "text": "Previous message" },
    { "role": "assistant", "text": "Previous response" }
  ]
}
```

**Fields:**
- `message` (required) — User's question
- `history` (optional) — Previous messages for context

**Response:**
```json
{
  "reply": "You have 2 liters of milk in stock.",
  "history": [
    { "role": "user", "text": "How much milk do I have?" },
    { "role": "assistant", "text": "You have 2 liters of milk in stock." }
  ]
}
```

**Available Tools:**

The AI can call these functions to query your data:

| Tool | Description | Parameters |
|------|-------------|------------|
| `GetStockLevel` | Get quantity of specific item | `itemName: string` |
| `GetFullInventory` | List all inventory items | none |
| `GetConsumptionHistory` | Get usage/purchase history | `days: int`, `category: string?` |

**Behavior:**
- Sends message to Gemini with tool definitions
- Gemini may call tools (multiple times if needed)
- Results fed back to Gemini
- Final text response returned

**Example conversation:**

```
User: "What's running low?"
→ AI calls GetFullInventory()
→ AI analyzes results
→ Response: "You're low on milk (1 left) and coffee (50g left)."
```

---

## Error Responses

All errors follow this format:

```json
{
  "error": "Description of what went wrong"
}
```

**Common Status Codes:**

| Code | Meaning |
|------|---------|
| `200 OK` | Success |
| `400 Bad Request` | Invalid input, missing fields |
| `404 Not Found` | Resource doesn't exist |
| `500 Internal Server Error` | Server-side error (check logs) |

---

## Rate Limiting

Currently no rate limiting is implemented — designed for private network use.

If you expose HomeStoq publicly, consider:
- Adding rate limiting middleware
- API key authentication
- Reverse proxy with rate limiting (nginx, etc.)

---

## CORS

CORS is configured to allow requests from any origin (designed for local network). If you deploy publicly, restrict this:

```csharp
// In Program.cs
app.UseCors(policy => 
    policy.WithOrigins("https://yourdomain.com"));
```

---

## WebSocket / Real-Time

Currently not implemented. The frontend polls for updates or refreshes manually.

If implementing real-time updates:
- Consider SignalR for inventory sync
- Or Server-Sent Events for simple push notifications

---

## Testing the API

### cURL Examples

**Get inventory:**
```bash
curl http://localhost:5000/api/inventory
```

**Update quantity:**
```bash
curl -X POST http://localhost:5000/api/inventory/update \
  -H "Content-Type: application/json" \
  -d '{"itemName":"Mjölk","quantityChange":-1}'
```

**Scan receipt:**
```bash
curl -X POST http://localhost:5000/api/receipts/scan \
  -F "receiptImage=@receipt.jpg"
```

**Chat:**
```bash
curl -X POST http://localhost:5000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message":"How much milk?"}'
```

---

## Client Libraries

No official client libraries yet. The API is simple enough for direct HTTP calls from:
- JavaScript `fetch()`
- Python `requests`
- Any HTTP client

If building a custom client, handle:
- JSON parsing
- Error status codes
- File upload for receipt scanning
