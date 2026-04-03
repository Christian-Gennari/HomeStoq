# Architecture Overview

📖 **Documentation Index:**
[README](../README.md) | [Getting Started](01-getting-started.md) | [Usage Guide](02-usage-guide.md) | [Configuration](03-configuration.md) | Architecture | [API Reference](05-api-reference.md) | [Scraper](07-scraper.md)

This document explains how HomeStoq fits together. It's written for anyone curious about the system design — you don't need to be a developer to understand it.

---

## The Big Picture

HomeStoq connects four things:

1. **You** — via phone, voice, or browser
2. **Google Keep** — voice command entry point
3. **The Scraper** — bridges Keep to HomeStoq
4. **HomeStoq Core** — inventory, AI, web interface

```
You
├─→ Phone camera ──→ Receipt scan ──→┐
├─→ Voice to Google ──→ Keep list ──→┼──→ HomeStoq Core ──→ Database
└─→ Browser ──→ Web UI ─────────────→┘        ↑
                                              │
                                         Scraper (keeps in sync)
```

---

## The Core Components

### 1. HomeStoq.App (The Web Application)

**What it does:**
- Serves the web interface you see in your browser
- Provides API endpoints for the scraper and frontend
- Manages all AI interactions
- Handles the database

**Tech details:**
- Built with ASP.NET Core 10
- Uses "Minimal APIs" — lightweight, fast
- Serves static files (HTML, CSS, JS) from `wwwroot/`
- Communicates with Gemini AI for parsing and chat

**Where it runs:**
- On your local machine or home server
- Accessible at `http://localhost` (or your configured host)

---

### 2. GoogleKeepScraper (The Voice Bridge)

**What it does:**
- Watches your Google Keep lists
- Detects new voice commands
- Sends them to the API
- Cleans up processed items

**Two modes of operation:**

#### Remote Debugging Mode (Default)

Uses Chrome DevTools Protocol (CDP) to connect to your actual Chrome browser.

**How it works:**
1. Scraper launches Chrome with a special debug flag
2. Connects to Chrome's remote control interface
3. Navigates to Google Keep
4. Checks your lists every ~45 seconds

**Why it's special:**
- It's controlling your *real* Chrome
- Uses your actual cookies, history, IP address
- Nearly invisible to Google's bot detection

**Profile location:** `%LocalAppData%/HomeStoq/chrome-profile`

#### Playwright Mode (Fallback)

Uses an isolated browser controlled by Playwright.

**How it works:**
1. Scraper launches its own Chromium browser
2. Injects anti-detection scripts
3. Same polling behavior as CDP mode

**When to use:**
- Chrome not installed
- CDP mode having issues
- Testing environment

**Profile location:** `browser-profile/` in the project directory

---

### 3. The Database (SQLite)

**What it stores:**
- Current inventory (what you have now)
- History (everything that's ever happened)
- Receipts (metadata about scanned receipts)
- AI cache (to avoid redundant API calls)

**Why SQLite:**
- Zero setup required
- Single file — easy to back up
- Sufficient for home use (handles thousands of items easily)
- No separate database server to maintain

**Location:** `data/homestoq.db` (created automatically)

---

### 4. The AI Integration (Google Gemini)

HomeStoq uses Google's Gemini AI for:

| Task | What Gemini Does |
|------|-----------------|
| **Receipt OCR** | Reads receipt images, extracts items, prices, stores |
| **Voice Parsing** | Understands "slut på mjölk" → decrease milk by 1 |
| **Shopping Suggestions** | Analyzes 30 days of history, predicts needs |
| **Pantry Chat** | Answers questions about inventory |

**How it's integrated:**
- Uses `Google.GenAI` SDK
- Wrapped with `Microsoft.Extensions.AI` for flexibility
- Supports both Swedish and English
- Function calling enables chat to query your actual database

---

## Data Flow Examples

### Scanning a Receipt

```
1. You take photo of receipt
        ↓
2. Browser sends image to API
        ↓
3. API sends image to Gemini
        ↓
4. Gemini extracts: [{"name": "Mjölk", "qty": 2, "price": 15.50}]
        ↓
5. API updates database:
   - Inventory: add 2 Mjölk
   - History: record purchase
   - Receipts: save receipt metadata
        ↓
6. Browser shows extracted items
        ↓
7. You confirm, items added to inventory
```

### Voice Command

```
1. You say "slut på mjölk" to Google Assistant
        ↓
2. Google adds text to your Keep list
        ↓
3. Scraper polls Keep, finds new item
        ↓
4. Scraper sends text to API: /api/voice/command
        ↓
5. API asks Gemini: what does this mean?
        ↓
6. Gemini returns: {"action": "remove", "item": "Mjölk", "qty": 1}
        ↓
7. API updates inventory (decrease milk by 1)
        ↓
8. Scraper marks item done in Keep, cleans up
```

### Chat Query

```
1. You type "How much coffee?" in chat
        ↓
2. Browser sends to API: /api/chat
        ↓
3. API sends to Gemini with available tools
        ↓
4. Gemini decides: I need GetStockLevel("Kaffe")
        ↓
5. API executes database query
        ↓
6. Result sent back to Gemini
        ↓
7. Gemini composes: "You have 250g of coffee left."
        ↓
8. Reply shown in chat
```

---

## The Frontend (What You See)

Built with simplicity in mind:

- **No build step** — Just HTML, CSS, JavaScript files
- **Alpine.js** — Lightweight reactivity (2KB)
- **No framework bloat** — Fast to load, easy to modify

### Views

| Tab | Purpose |
|-----|---------|
| **Stock** | Current inventory, manual adjustments, search |
| **Scan** | Receipt upload and processing |
| **Receipts** | History of all scanned receipts |
| **List** | AI-generated shopping suggestions |

Plus a **Chat slide-over** accessible from any tab.

---

## Security & Privacy

### What Stays Local

✅ **All your data:**
- Inventory database
- Receipt images (if you keep them)
- Shopping history
- Voice command logs

✅ **Your configuration:**
- API keys (in `.env`)
- Settings (in `config.ini`)

### What Goes to External Services

⚠️ **Gemini API:**
- Receipt images (for OCR)
- Voice command text (for parsing)
- Chat messages (for responses)

⚠️ **Google Keep:**
- Voice commands you add (temporarily, until processed)

**Mitigations:**
- Use a dedicated Google account (not your main)
- No personal data in voice commands (just "slut på mjölk")
- Gemini API can be monitored and rate-limited

---

## Scalability & Performance

HomeStoq is designed for **single-home use**:

- SQLite handles 10,000+ items easily
- AI calls are cached to minimize API usage
- Frontend is lightweight (works on old phones)
- Scraper polls with jitter to avoid being a burden

**Not designed for:**
- Multiple simultaneous users (though it works)
- Commercial use
- Thousands of receipts per day

For a home pantry, it's more than fast enough.

---

## Why These Choices?

### Why SQLite over PostgreSQL/MySQL?

- **Zero setup** — Works immediately
- **Single file** — Easy backups
- **Sufficient** — Pantries don't need complex queries
- **Portable** — Move the whole app by copying files

### Why Minimal APIs over MVC?

- **Less code** — Easier to understand
- **Faster** — No framework overhead
- **Simpler** — One file for routing, handlers, startup

### Why CDP over pure Playwright?

- **Stealth** — Much harder to detect
- **Realistic** — Uses your actual browser
- **Trust** — Shares your IP reputation with Google

### Why Alpine.js over React/Vue?

- **Tiny** — 2KB vs 100KB+
- **No build** — Edit files directly
- **Simple** — HTML stays readable
- **Fast** — No virtual DOM overhead

---

## Modifying the System

Want to extend HomeStoq? Here are the common extension points:

### Add a New API Endpoint

Edit `src/HomeStoq.App/Program.cs` — add a new `app.MapPost()` or `app.MapGet()`.

### Change the UI

Edit files in `src/HomeStoq.App/wwwroot/`:
- `index.html` — Page structure
- `app.js` — JavaScript logic
- `style.css` — Appearance

### Add a New Config Option

1. Add to `config.ini`
2. Read in `Program.cs` via `builder.Configuration`
3. Pass to services via DI

### Modify AI Behavior

Edit `src/HomeStoq.App/Services/PromptProvider.cs` — contains all AI prompts.

---

## See Also

- **[Getting Started](01-getting-started.md)** — Set up your own instance
- **[Configuration Guide](03-configuration.md)** — All settings explained
- **[Scraper Deep-Dive](07-scraper.md)** — How the voice bridge works
- **[Development Guide](08-development.md)** — Building and contributing
