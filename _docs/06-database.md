# Database Schema

📖 **Documentation Index:**
[README](../README.md) | [Getting Started](01-getting-started.md) | [Usage Guide](02-usage-guide.md) | [Configuration](03-configuration.md) | [Architecture](04-architecture.md) | [API Reference](05-api-reference.md) | Database | [Scraper](07-scraper.md)

HomeStoq uses **SQLite** — a file-based database that requires zero setup. This document describes the tables, columns, and relationships.

**Database file:** `data/homestoq.db` (created automatically on first run)

---

## Overview

Seven tables store all data:

```
┌─────────────┐       ┌─────────────┐       ┌─────────────┐
│  Inventory  │       │   History   │       │  Receipts   │
│  (current   │←──────│  (every     │←──────│  (receipt   │
│   stock)    │       │  change)    │       │   metadata)  │
└─────────────┘       └─────────────┘       └─────────────┘
                              ↑
                              │
                       ┌─────────────┐       ┌─────────────┐
                       │   AiCache   │       │  BuyLists   │
                       │  (AI res.   │       │  (Shopping  │
                       │   caching)  │       │    Buddy)   │
                       └─────────────┘       └──────┬──────┘
                                                    │
                                             ┌──────┴──────┐
                                             │ BuyListItems│
                                             └─────────────┘
                                                    │
                                             ┌──────┴──────┐
                                             │ BuyListMsg  │
                                             └─────────────┘
```

---

## Table: Inventory

**Purpose:** Current stock levels — what you have right now.

**Schema:**

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | INTEGER | PRIMARY KEY, AUTOINCREMENT | Unique identifier |
| `ItemName` | TEXT | UNIQUE, NOT NULL | Normalized item name (e.g., "Mjölk") |
| `Quantity` | REAL | NOT NULL | Current stock level |
| `Category` | TEXT | nullable | Category (e.g., "Mejeri") |
| `LastPrice` | REAL | nullable | Most recent known unit price |
| `Currency` | TEXT | nullable | Currency code (e.g., "SEK", "USD") |
| `UpdatedAt` | TEXT | NOT NULL | ISO 8601 timestamp |

**Example rows:**

| Id | ItemName | Quantity | Category | LastPrice | Currency | UpdatedAt |
|----|----------|----------|----------|-----------|----------|-----------|
| 1 | Mjölk | 2.0 | Mejeri | 15.50 | SEK | 2026-04-03T10:30:00Z |
| 2 | Ägg | 12.0 | Mejeri | 3.50 | SEK | 2026-04-02T14:15:00Z |
| 3 | Kaffe | 0.5 | Drycker | 85.00 | SEK | 2026-04-01T09:00:00Z |

**Notes:**
- `Quantity` is REAL (decimal) to support fractional units (e.g., 0.5 kg cheese)
- `ItemName` is normalized by Gemini (brand names removed, consistent casing)
- `UpdatedAt` changes on every modification

---

## Table: Receipts

**Purpose:** Metadata about scanned receipts.

**Schema:**

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | INTEGER | PRIMARY KEY, AUTOINCREMENT | Receipt ID |
| `Timestamp` | TEXT | NOT NULL | When scanned (ISO 8601) |
| `StoreName` | TEXT | nullable | Detected store name |
| `TotalAmountPaid` | REAL | nullable | Sum of all item prices |

**Example rows:**

| Id | Timestamp | StoreName | TotalAmountPaid |
|----|-------------|-----------|-----------------|
| 1 | 2026-04-03T10:30:00Z | ICA Supermarket | 485.50 |
| 2 | 2026-03-28T16:45:00Z | Willys | 320.00 |

**Relationship:**
- One Receipt → Many History entries (via `ReceiptId` FK)

---

## Table: History

**Purpose:** Every inventory change ever made — the "source of truth" for AI predictions.

**Schema:**

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | INTEGER | PRIMARY KEY, AUTOINCREMENT | Entry ID |
| `Timestamp` | TEXT | NOT NULL | When the change occurred |
| `ItemName` | TEXT | NOT NULL | Item that changed |
| `ExpandedName` | TEXT | nullable | Full product name from receipt |
| `Action` | TEXT | NOT NULL | Type of change: `Add` or `Remove` |
| `Quantity` | REAL | NOT NULL | Amount changed |
| `Price` | REAL | nullable | Unit price at time of change |
| `TotalPrice` | REAL | nullable | Quantity × Price |
| `Currency` | TEXT | nullable | Currency code |
| `Source` | TEXT | NOT NULL | What triggered the change: `Receipt`, `Voice`, or `Manual` |
| `ReceiptId` | INTEGER | FOREIGN KEY → Receipts.Id | Link to receipt (nullable) |

**Example rows:**

| Id | Timestamp | ItemName | ExpandedName | Action | Quantity | Price | Source | ReceiptId |
|----|-----------|----------|--------------|--------|----------|-------|--------|-----------|
| 1 | 2026-04-03T10:30:00Z | Mjölk | ICA Kärnmjölk 1L | Add | 2.0 | 15.50 | Receipt | 1 |
| 2 | 2026-04-03T10:30:00Z | Ägg | Willys Ägg 12-pack | Add | 12.0 | 3.50 | Receipt | 1 |
| 3 | 2026-04-03T19:00:00Z | Mjölk | null | Remove | 1.0 | null | Voice | null |
| 4 | 2026-04-02T14:15:00Z | Kaffe | null | Add | 1.0 | 85.00 | Manual | null |

**Source meanings:**
- `Receipt` — Item from scanned receipt
- `Voice` — Voice command processed
- `Manual` — Manual adjustment via web UI

**Why ExpandedName matters:**
- `ItemName` is normalized for inventory tracking ("Mjölk")
- `ExpandedName` preserves original receipt text ("ICA Kärnmjölk 1L")
- Useful for: price tracking, finding where you bought something

---

## Table: AiCache

**Purpose:** Cache AI responses to reduce API calls and cost.

**Schema:**

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | INTEGER | PRIMARY KEY, AUTOINCREMENT | Cache entry ID |
| `CacheKey` | TEXT | UNIQUE, NOT NULL | SHA256 hash of input |
| `Response` | TEXT | NOT NULL | Cached JSON response |
| `CreatedAt` | TEXT | NOT NULL | When cached |
| `ExpiresAt` | TEXT | NOT NULL | When to invalidate |

**Example:**

| Id | CacheKey | Response | CreatedAt | ExpiresAt |
|----|----------|----------|-----------|-----------|
| 1 | a3f5c8... | [{"itemName":"Mjölk",...}] | 2026-04-03T10:30:00Z | 2026-04-03T11:30:00Z |

**How it works:**
1. Hash the input (e.g., receipt image or voice text)
2. Check if hash exists in cache and hasn't expired
3. If yes, return cached response (no API call)
4. If no, call Gemini, save result to cache

**TTL (Time To Live):**
- Receipt OCR: 24 hours (receipts rarely change)
- Voice parsing: 1 hour (less common repeat)
- Shopping suggestions: 1 hour (based on changing data)

---

## Table: BuyLists

**Purpose:** Shopping list sessions, including saved lists and current drafts.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | INTEGER | Primary Key |
| `CreatedAt` | TEXT | When the list was created |
| `Status` | INTEGER | Status: `Draft` (0), `Active` (1), `Completed` (2), `Cancelled` (3), `Saved` (4) |
| `GeneratedContext` | TEXT | AI-generated greeting or context |
| `UserContext` | TEXT | User's specific needs for this session |
| `IsSaved` | BOOLEAN | Whether the user explicitly saved this list |
| `SavedName` | TEXT | Custom name for saved lists |

---

## Table: BuyListItems

**Purpose:** Individual items within a shopping list.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | INTEGER | Primary Key |
| `BuyListId` | INTEGER | Foreign Key → BuyLists.Id |
| `ItemName` | TEXT | Name of the item |
| `Quantity` | REAL | How many to buy |
| `Category` | TEXT | AI-assigned category |
| `IsChecked` | BOOLEAN | Whether it's been "checked off" while shopping |
| `IsDismissed` | BOOLEAN | Whether it was removed from the list |

---

## Table: BuyListMessages

**Purpose:** Conversation history for the "Shopping Buddy" chat.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | INTEGER | Primary Key |
| `BuyListId` | INTEGER | Foreign Key → BuyLists.Id |
| `Role` | TEXT | `user`, `assistant`, or `system` |
| `Content` | TEXT | The message text |
| `ActionsJson` | TEXT | JSON log of actions taken (e.g., "added Milk") |
| `Timestamp` | TEXT | When the message was sent |

---

## Relationships

### Receipts → History

One receipt generates multiple history entries:

```
Receipt (Id: 1)
├─→ History (Mjölk, +2, ReceiptId: 1)
├─→ History (Ägg, +12, ReceiptId: 1)
└─→ History (Bröd, +1, ReceiptId: 1)
```

### Inventory ← History

Inventory is the *current state*. History is the *log of changes*.

To reconstruct inventory at any point in time:
```sql
SELECT ItemName, 
       SUM(CASE WHEN Action='Add' THEN Quantity ELSE -Quantity END) as Stock
FROM History 
WHERE Timestamp <= '2026-04-01'
GROUP BY ItemName
```

---

## Data Types Explained

### Why TEXT for timestamps?

SQLite doesn't have a native datetime type. We store ISO 8601 strings:
- `2026-04-03T10:30:00Z` — unambiguous, sortable, readable
- Works with any programming language
- No timezone conversion issues

### Why REAL for quantities?

Not everything comes in whole numbers:
- 0.5 kg cheese
- 1.25 liters juice
- 0.75 lbs meat

REAL (decimal) handles this. INTEGER would lose precision.

### Why separate Currency?

You might shop in different currencies:
- Day-to-day: SEK (Sweden)
- Online order: EUR (Germany)
- Travel: USD (USA)

Keeping currency per transaction allows accurate price tracking.

---

## Indexes

For performance, these indexes are created automatically:

```sql
-- Fast inventory lookups
CREATE INDEX IX_Inventory_ItemName ON Inventory(ItemName);

-- Fast history queries (for AI predictions)
CREATE INDEX IX_History_ItemName ON History(ItemName);
CREATE INDEX IX_History_Timestamp ON History(Timestamp);
CREATE INDEX IX_History_Source ON History(Source);

-- Fast receipt lookups
CREATE INDEX IX_Receipts_Timestamp ON Receipts(Timestamp);

-- Fast cache lookups
CREATE INDEX IX_AiCache_CacheKey ON AiCache(CacheKey);
CREATE INDEX IX_AiCache_ExpiresAt ON AiCache(ExpiresAt);
```

---

## Backup and Restore

### Backup

The entire database is a single file. Copy it:

```bash
# Linux/Mac
cp data/homestoq.db backup-$(date +%Y%m%d).db

# Windows
copy data\homestoq.db backup-%date%.db
```

### Restore

```bash
# Stop HomeStoq first
cp backup-20260403.db data/homestoq.db
# Restart HomeStoq
```

### Auto-Backup Script

Add to cron (Linux/Mac) or Task Scheduler (Windows):

```bash
#!/bin/bash
# backup.sh - Run daily
cp /path/to/homestoq.db /path/to/backups/homestoq-$(date +%Y%m%d).db
# Keep only last 30 days
find /path/to/backups -name "homestoq-*.db" -mtime +30 -delete
```

---

## Query Examples

### Current Stock

```sql
SELECT ItemName, Quantity, Category 
FROM Inventory 
WHERE Quantity > 0
ORDER BY Category, ItemName;
```

### Low Stock Alert

```sql
SELECT ItemName, Quantity
FROM Inventory
WHERE Quantity <= 1
ORDER BY Quantity;
```

### Monthly Spending

```sql
SELECT 
    strftime('%Y-%m', Timestamp) as Month,
    SUM(TotalPrice) as TotalSpent
FROM History
WHERE Source = 'Receipt'
GROUP BY Month
ORDER BY Month DESC;
```

### Most Purchased Items

```sql
SELECT 
    ItemName,
    COUNT(*) as TimesPurchased,
    SUM(Quantity) as TotalQuantity
FROM History
WHERE Source = 'Receipt'
GROUP BY ItemName
ORDER BY TimesPurchased DESC
LIMIT 10;
```

### Price Trends for an Item

```sql
SELECT 
    Timestamp,
    Price,
    StoreName
FROM History h
JOIN Receipts r ON h.ReceiptId = r.Id
WHERE ItemName = 'Mjölk'
  AND Source = 'Receipt'
ORDER BY Timestamp;
```

---

## Schema Migrations

HomeStoq uses Entity Framework Core's migrations. To modify the schema:

```bash
# Create a migration
dotnet ef migrations add MigrationName --project src/HomeStoq.App

# Apply to database
dotnet ef database update --project src/HomeStoq.App
```

**Note:** For a home pantry app, destructive migrations (dropping columns) are generally safe during early development. In production, prefer additive changes.

---

## Database Size

Typical sizes for a household:

| Usage | Approximate Size |
|-------|-----------------|
| Empty database | 50 KB |
| 1 year of receipts | 2-5 MB |
| 5 years of receipts | 10-20 MB |
| With images stored | 50-200 MB |

SQLite handles up to 281 TB, so you'll run out of groceries before hitting limits.

---

## Direct Database Access

Want to query directly? Use any SQLite client:

```bash
# Command line
sqlite3 data/homestoq.db "SELECT * FROM Inventory;"

# GUI tools
# - DB Browser for SQLite (free)
# - TablePlus (paid)
# - DBeaver (free)
```

**Warning:** Direct modifications bypass business logic. Use the API for changes.

---

## See Also

- **[Architecture](04-architecture.md)** — How the database fits in
- **[API Reference](05-api-reference.md)** — How the API queries the database
- **[Development Guide](08-development.md)** — Working with the data model in code