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
# Optional: Add GOOGLE_USERNAME/PASSWORD for auto-login

# 2. Start (Full Docker Stack)
npm run dev

# 3. Open dashboard
http://localhost:5050
```

> **First time?** If you didn't provide credentials in `.env`, open `http://localhost:6080` to log into Google Keep inside the scraper container.

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
    ┌──────────────────────────┐      ┌──────────────────────────┐
    │   HomeStoq App (Docker)  │◄─────┤  Keep Scraper (Docker)   │
    │  - AI Receipt Processing │      │  - Xvfb Virtual Display  │
    │  - Chat & Shopping Lists │      │  - Anti-detection Headed │
    └────────────┬─────────────┘      └─────────────┬────────────┘
                 │                                  │
                 ▼                                  ▼
    ┌──────────────────────────┐      ┌──────────────────────────┐
    │     SQLite Database      │      │     Chrome Profile       │
    │    (Persistent Vol)      │      │    (Persistent Vol)      │
    └──────────────────────────┘      └──────────────────────────┘
```

**Anti-Detection Engine:** The scraper runs a real, "Headed" Chrome instance inside a virtual desktop (Xvfb). This makes the automation indistinguishable from a human user. It supports **Automatic Login** via environment variables and provides a **noVNC web interface** (port 6080) for manual 2FA verification.

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
