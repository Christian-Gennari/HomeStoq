# AGENTS.md — HomeStoq Development Guide

Critical, repo-specific guidance for AI agents working on HomeStoq.

---

## 🔒 CRITICAL: Never Commit `.env`

**The .env file contains real API keys. It MUST NEVER be committed.**

- `.env` is in `.gitignore` — respect this absolutely
- Always edit `.env.example` for template changes, never `.env`
- If you accidentally stage `.env`, unstage immediately: `git restore --staged .env`
- Real keys in git history require key revocation (see _docs/01-getting-started.md)

---

## Project Structure

**Multi-project .NET solution:**

```
src/
├── HomeStoq.App/              # Main API (ASP.NET Core 10 Minimal APIs)
│   └── Services/              # AI provider factories, hybrid client
├── HomeStoq.Shared/           # Shared DTOs, utilities
└── HomeStoq.Plugins/
    └── GoogleKeepScraper/     # Voice command scraper (separate process)
```

**Key architectural constraint:** The scraper runs as a **separate process** that polls Google Keep and calls the main API. It's not a library — it's a standalone executable.

---

## Development Commands

**Primary workflow (Docker):**
```bash
npm run dev           # Start API + Scraper in Docker
npm run docker:build  # Rebuild images after .cs changes
npm run docker:clean  # Full reset (removes volumes)
```

**Local development (no Docker):**
```bash
npm run dev:local     # Runs both API and scraper locally
npm run api:local     # API only (dotnet run)
npm run scraper:local # Scraper only
```

**Never run `dotnet build` directly in src/ — always use npm scripts** (they handle Shared project dependency order).

---

## Configuration: Single Source of Truth

**Two files, clear separation:**

| File | Purpose | Example |
|------|---------|---------|
| `config.ini` | All app settings (port, AI provider, scraper timing) | `Provider=Gemini`, `HostUrl=http://*:5050` |
| `.env` | Secrets only (API keys, Google creds) | `GEMINI_API_KEY=...` |

**Critical rule:** Never put application settings in `.env`. Docker containers mount `config.ini` as read-only; they don't read `.env` for settings.

---

## Hybrid AI Architecture (Non-Obvious)

**Two different AI systems with different rules:**

| Feature | Provider | Configuration |
|---------|----------|---------------|
| **Vision (Receipt OCR)** | Always Gemini (native SDK) | `[AI.Vision]` fallback chain |
| **General (Chat/Voice/Shopping)** | Configurable: Gemini or OpenRouter | `[AI]` Provider= setting |

**Implications:**
- `GEMINI_API_KEY` is **always required** (even with `Provider=OpenRouter`)
- Vision uses native `Google.GenAI` SDK, not OpenAI-compatible endpoint
- General operations use `Microsoft.Extensions.AI.OpenAI` with either provider

---

## Pre-Commit Safety

Add this to `.git/hooks/pre-commit` to prevent accidental .env commits:

```bash
#!/bin/bash
if git diff --cached --name-only | grep -q "^\.env$"; then
    echo "ERROR: Attempting to commit .env file!"
    exit 1
fi
```

---

## Common Pitfalls

1. **Don't use `localhost` in `config.ini` HostUrl for Docker** — always use `http://*:PORT`
2. **Scraper requires Chrome installed** — won't work without Chrome binary (uses CDP on port 9222)
3. **Model names are provider-specific** — `gemini-2.5-flash-lite` ≠ `google/gemini-2.5-flash-lite:free`
4. **2FA + auto-login = spam** — don't set GOOGLE_USERNAME/PASSWORD if account has 2FA

---

## Testing Receipt Scanning

The only way to test vision/OCR:
1. Ensure `GEMINI_API_KEY` is set in `.env`
2. Upload receipt via web UI at `http://localhost:5050`
3. Check logs: look for "Vision request succeeded with model X"

---

## Documentation Reference

| Question | See |
|----------|-----|
| Full config options | `_docs/03-configuration.md` |
| AI architecture details | `_docs/04-architecture.md` |
| Troubleshooting | `_docs/01-getting-started.md` |
| Scraper internals | `_docs/07-scraper.md` |
