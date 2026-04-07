# HomeStoq

HomeStoq is a pantry management system that runs on your home network. It tracks what you have, what you need, and updates automatically through receipt scanning and voice commands.

**The problem it solves:** Manually tracking groceries is tedious and you forget what's in the pantry. HomeStoq automates this by extracting items from receipt photos and listening to your existing voice assistant.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)
[![.NET 10](https://img.shields.io/badge/.NET-10-blue)](https://dotnet.microsoft.com/)

---

## Quick Start

```bash
# 1. Setup
npm run setup
# Edit .env and add your GEMINI_API_KEY

# 2. Start
npm run dev

# 3. Open browser
curl http://localhost:5050
```

First time: Chrome opens automatically — log into Google Keep once.

> Default port is `5050` to avoid permission issues. Change in `config.ini` if needed.

📖 **[Getting Started Guide](_docs/01-getting-started.md)**

---

## How It Works

```
┌─────────────────────────────────────────────────────────────┐
│  Input Methods                                              │
├─────────────────┬─────────────────┬─────────────────────────┤
│  Phone camera   │  Voice command  │  Web browser            │
│  (receipt)      │  (Google Keep)  │  (manual/chat)          │
└────────┬────────┴────────┬────────┴────────────┬────────────┘
         │                 │                     │
         ▼                 ▼                     ▼
    ┌────────────────────────────────────────────────────┐
    │           HomeStoq Core (ASP.NET Core)             │
    │  - AI extracts receipt items (Gemini Vision)       │
    │  - Parses voice commands from Keep list            │
    │  - Chat interface for questions & shopping lists   │
    └─────────────────────┬──────────────────────────────┘
                          │
                          ▼
              ┌───────────────────────┐
              │  SQLite Database      │
              │  (local storage)      │
              └───────────────────────┘
```

**Voice integration:** The scraper monitors your Google Keep shopping list. When you say "Hey Google, add milk to my shopping list", it extracts the item and updates your inventory. Since it uses your actual Chrome browser via Chrome DevTools Protocol, it avoids bot detection.

---

## Use Cases

| Situation | Action | Result |
|-----------|--------|--------|
| After shopping | Scan receipt photo | Items added to inventory |
| Running low | "Hey Google, we're out of coffee" | Stock decreases |
| Before shopping | Chat: "What should I buy?" | AI suggests based on habits |
| Checking stock | Ask: "How much rice is left?" | Instant quantity |

📖 **[Usage Guide](_docs/02-usage-guide.md)** for detailed workflows

---

## Documentation

| Guide | Contents |
|-------|----------|
| [01 - Getting Started](_docs/01-getting-started.md) | Installation, setup, troubleshooting |
| [02 - Usage Guide](_docs/02-usage-guide.md) | Daily workflows, voice commands |
| [03 - Configuration](_docs/03-configuration.md) | All config.ini options |
| [04 - Architecture](_docs/04-architecture.md) | System design |
| [05 - API Reference](_docs/05-api-reference.md) | Endpoints |
| [06 - Database](_docs/06-database.md) | Schema |
| [07 - Scraper Deep-Dive](_docs/07-scraper.md) | CDP mode, anti-detection |
| [08 - Development](_docs/08-development.md) | Building and contributing |

---

## Tech Stack

- **Backend:** ASP.NET Core 10 (Minimal APIs)
- **Frontend:** Vanilla HTML/CSS/JS + Alpine.js
- **Database:** SQLite + Entity Framework
- **AI:** Google Gemini (vision + chat)
- **Browser Automation:** Chrome DevTools Protocol
- **Deployment:** Docker or bare metal

---

## Roadmap

Planned features in [GitHub Issues](https://github.com/Christian-Gennari/HomeStoq/issues):

- Push notifications for voice command processing
- Analytics dashboard (historical trends)
- Mobile-optimized receipt scanning UI
- Scraper health monitoring

---

## License

MIT License — see [LICENSE.md](LICENSE.md)

Built in Sweden by [Christian Gennari](https://dev.cgennari.com)
