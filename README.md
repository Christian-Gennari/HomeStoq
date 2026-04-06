# HomeStoq 🏠🍎

**Your AI-powered pantry assistant that just works.**

Track groceries by scanning receipts, managing inventory with your voice, and getting smart shopping suggestions — all running privately on your home network.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)
[![.NET 10](https://img.shields.io/badge/.NET-10-blue)](https://dotnet.microsoft.com/)

---

## What is HomeStoq?

HomeStoq is a **local-first pantry management system** that makes grocery tracking effortless:

- 📸 **Scan receipts** — Take a photo, AI extracts items automatically
- 🗣️ **Voice commands** — "Hey Google, we're out of milk" → inventory updated
- 🤖 **Ask your pantry** — "How much coffee left?" → instant answer
- 📝 **Shopping Buddy** — Conversational AI that helps you build lists, plan meals, and suggests what to buy based on your habits

Everything runs on **your hardware**, **your network**. No cloud subscriptions, no data mining.

---

## Quick Start (5 minutes)

```bash
# 1. Setup
npm run setup
# Edit .env and add your GEMINI_API_KEY

# 2. Start
npm run dev

# 3. Open browser
curl http://localhost
```

**First time only:** Chrome opens automatically — log into Google Keep once. Done!

📖 **[Full Getting Started Guide](_docs/01-getting-started.md)**

---

## How It Works

```
You
├─→ Phone camera ──→ Receipt scan ──→┐
├─→ Voice to Google ──→ Keep list ──→┼──→ HomeStoq Core ──→ Database
└─→ Browser ──→ Web UI ─────────────→┘        ↑
                                              │
                                         Scraper (keeps in sync)
```

**The magic:** The scraper connects to your *real* Chrome browser (not a fake one), making it virtually invisible to Google's bot detection.

---

## Everyday Use

| Situation | What You Do | What Happens |
|-----------|-------------|--------------|
| **Grocery run** | Scan receipt with phone | Items appear in inventory automatically |
| **Empty milk carton** | Tell Google "slut på mjölk" | Stock decreases by 1 |
| **Before shopping** | Chat with Shopping Buddy | AI helps you build a list and plan meals |
| **Wondering what's left** | Ask chat "How much coffee?" | Instant answer from your data |

📖 **[Usage Guide](_docs/02-usage-guide.md)** for detailed workflows

---

## Documentation

| Guide | What's Inside |
|-------|---------------|
| [01 - Getting Started](_docs/01-getting-started.md) | Installation, first setup, troubleshooting |
| [02 - Usage Guide](_docs/02-usage-guide.md) | Daily workflows, tips, voice commands |
| [03 - Configuration](_docs/03-configuration.md) | All config.ini options explained |
| [04 - Architecture](_docs/04-architecture.md) | How the system fits together |
| [05 - API Reference](_docs/05-api-reference.md) | Endpoints for developers |
| [06 - Database](_docs/06-database.md) | Schema and data model |
| [07 - Scraper Deep-Dive](_docs/07-scraper.md) | CDP mode, anti-detection, troubleshooting |
| [08 - Development](_docs/08-development.md) | Building, extending, contributing |

---

## Tech Stack (For the Curious)

- **Backend:** ASP.NET Core 10 with Minimal APIs
- **Frontend:** Vanilla HTML/CSS/JS + Alpine.js
- **Database:** SQLite with Entity Framework
- **AI:** Google Gemini (vision + chat)
- **Voice Bridge:** Chrome DevTools Protocol (your real browser)
- **Platform:** Docker (Alpine) or bare metal

---

## Roadmap

See [GitHub Issues](https://github.com/Christian-Gennari/HomeStoq/issues) for planned features:
- Push notifications when voice commands process
- Historical analytics dashboard
- Mobile-optimized receipt scanning
- Scraper health dashboard

---

## License

MIT License — see [LICENSE.md](LICENSE.md)

Built with ☕ in Sweden by [Christian Gennari](https://dev.cgennari.com)
