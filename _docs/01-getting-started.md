# Getting Started with HomeStoq

👋 Welcome! This guide will take you from zero to a working HomeStoq setup in about 5 minutes.

📖 **Documentation Index:**
[README](../README.md) | Getting Started | [Usage Guide](02-usage-guide.md) | [Configuration](03-configuration.md) | [Architecture](04-architecture.md) | [Scraper](07-scraper.md)

---

## What You'll Need

Before we start, make sure you have:

- A computer that can run 24/7 (Raspberry Pi, old laptop, NAS, or server)
- **Docker and Docker Compose** installed
- [Node.js](https://nodejs.org/) installed (for our helper scripts)
- A [Google AI Studio](https://aistudio.google.com/) API key (free tier works fine)
- A Google account (preferably a dedicated one just for HomeStoq)

---

## Step-by-Step Setup

### Step 1: Get the Code

```bash
git clone https://github.com/Christian-Gennari/HomeStoq.git
cd HomeStoq
```

### Step 2: Run Setup

Our setup script checks prerequisites and creates your configuration files:

```bash
npm run setup
```

### Step 3: Add Your Credentials (API Key Required)

Open `.env` and add your API keys:

```bash
# REQUIRED for ALL setups (receipt scanning always uses Gemini)
GEMINI_API_KEY=your_gemini_key_here

# REQUIRED only when Provider=OpenRouter in config.ini
# Get key at: https://openrouter.ai/keys
# OPENROUTER_API_KEY=sk-or-v1-your_openrouter_key_here

# Google Keep Login - OPTIONAL and NOT recommended with 2FA
# If you have 2FA enabled, SKIP these and use manual login instead
# (see Step 5 below). Adding credentials with 2FA causes constant
# approval notifications on your phone.
# GOOGLE_USERNAME=your_email@gmail.com
# GOOGLE_PASSWORD=your_password
```

**Important API Key Rules:**
- **GEMINI_API_KEY is ALWAYS required** — Receipt scanning uses Gemini regardless of provider setting
- **OPENROUTER_API_KEY only when `Provider=OpenRouter`** — Required for chat/voice when using OpenRouter
- Both keys can coexist — The active provider determines which is used for general AI

**2FA Users:** Leave `GOOGLE_USERNAME` and `GOOGLE_PASSWORD` blank/commented. 
You'll log in manually via noVNC in Step 5.

**Want to try OpenRouter?** After setup, edit `config.ini`:
```ini
[AI]
Provider=OpenRouter
# Make sure OPENROUTER_API_KEY is set in .env!
```
Then restart: `npm run stop && npm run dev`

### Step 4: Start Everything

```bash
npm run dev
```

This will build and start the Docker containers.

### Step 5: Log into Google Keep (If needed)

If you didn't provide Google credentials in Step 3 (recommended for 2FA users):

1. Open `http://localhost:6080` in your browser.
2. It will **automatically redirect** to the noVNC interface (`vnc_auto.html`).
3. You'll see the Chrome window running inside the container.
4. Log into Google Keep manually in the browser.
5. The session will be saved to the `chrome-profile` volume and persist across restarts.

> **Note:** The session persists because we use a Docker volume (`chrome-profile`) 
> to store Chrome's user data between container restarts.

### Step 6: Verify It Works

1. Open `http://localhost:5050` in your browser.
2. You should see the HomeStoq dashboard.

🎉 **You're done!** HomeStoq is now running.

---

## Configuration: Single Source of Truth

All application settings are now in `config.ini` (not environment variables):

- **Port:** `HostUrl` in `[App]` section (default: `http://*:5050`)
- **Browser Mode:** `BrowserMode` in `[GoogleKeepScraper]` section
- **Headless:** `Headless` in `[GoogleKeepScraper]` section
- **List Names:** `KeepListName` in `[GoogleKeepScraper]` section

**Why this matters:**
- One file controls all settings for both Docker and local development
- No configuration drift between environments
- Dockerfiles no longer hardcode ports or browser modes
- Easy to version control and backup

Changes to `config.ini` take effect after restarting the containers:
```bash
npm run stop && npm run dev
```

---

## First Voice Command

Let's test the voice integration:

1. Say to your Google Assistant: *"Hey Google, add 'slut på mjölk' to my inköpslistan"*
2. Wait ~45 seconds
3. Check HomeStoq — your milk stock should decrease by 1

If it doesn't work immediately, don't worry. Check the [troubleshooting section](#troubleshooting) below.

---

## Configuration Basics

Your main settings file is `config.ini`. Here are the most important options:

```ini
[App]
Language=Swedish              # Swedish or English

[GoogleKeepScraper]
KeepListName=inköpslistan     # Your Google Keep list name
ActiveHours=07-23             # When the scraper runs (24h format)
BrowserMode=RemoteDebugging   # RemoteDebugging (default) or Playwright
```

📖 See [Configuration Guide](03-configuration.md) for all options.

---

## Common Commands

| Command | What It Does |
|---------|--------------|
| `npm run dev` | Start API + Scraper locally |
| `npm run api` | Start just the API |
| `npm run scraper` | Start just the scraper |
| `npm run stop` | Stop Docker containers |
| `npm run clean` | Remove build artifacts |
| `npm run help` | Show all available commands |

---

## Troubleshooting

### "2FA spam on my phone"

**Problem:** You get constant Google 2FA approval notifications when running the scraper.

**Cause:** You added `GOOGLE_USERNAME` and `GOOGLE_PASSWORD` to `.env` but your 
account has 2FA enabled. Every container restart triggers a login attempt.

**Solution:**
1. Stop HomeStoq: `npm run stop`
2. Edit `.env` and comment out the Google credentials:
   ```bash
   # GOOGLE_USERNAME=...
   # GOOGLE_PASSWORD=...
   ```
3. Restart: `npm run dev`
4. Open `http://localhost:6080` and log in **once** via noVNC
5. The session will persist; no more 2FA spam

### "Chrome doesn't open"

**Problem:** Chrome isn't launching when you run the scraper.

**Solutions:**
1. Make sure Google Chrome is installed
2. Check that `BrowserMode=RemoteDebugging` in `config.ini`
3. Try the fallback mode: change to `BrowserMode=Playwright`

### "Chrome opens but scraper can't connect"

**Problem:** You see Chrome, but logs show connection errors.

**Solutions:**
1. Wait 10-15 seconds after Chrome opens — it needs time to start the debug server
2. Check if port 9222 is already in use: `netstat -an | grep 9222`
3. The scraper will automatically try the next available port, but it takes longer

### "Login required every time"

**Problem:** You have to log into Google Keep every time you restart.

**Solutions:**
1. **CDP Mode:** Login should persist in `%LocalAppData%/HomeStoq/chrome-profile`
2. **Playwright Mode:** Login persists in `browser-profile/` directory
3. Make sure you're not in incognito/private mode
4. Check that the profile directory is writable

### "Voice commands not working"

**Problem:** You say "slut på mjölk" but HomeStoq doesn't update.

**Diagnostic steps:**
1. Check scraper is running: look for `[INFO] Connected to Chrome via CDP` in logs
2. Check you're within `ActiveHours` (default: 07:00-23:00)
3. Verify `KeepListName` in `config.ini` exactly matches your Google Keep list name
4. Look at scraper logs — is it finding your list? Processing items?
5. Try a manual test: add text directly to the Keep list, see if scraper picks it up

### "AI API errors"

**Problem:** Receipt scanning or chat returns API errors.

**Solutions:**

**For ALL providers (Gemini AND OpenRouter):**
1. ✅ Verify `GEMINI_API_KEY` is correct in `.env` — **This is ALWAYS required for receipt scanning**
2. Check logs for specific error messages
3. Restart containers: `npm run stop && npm run dev`

**For Gemini provider (default):**
- Rate limits? Try a different model in `config.ini`:
  ```ini
  [AI]
  GeminiModel=gemini-2.5-flash  # Higher rate limits than flash-lite
  ```
- Vision errors? Check `AI.Vision` fallback models in `config.ini`

**For OpenRouter provider (if configured):**
- Ensure `OPENROUTER_API_KEY` is set in `.env`
- Check if you're hitting free tier limits (20 RPM / 200 requests/day)
- Switch back to Gemini if needed:
  ```ini
  [AI]
  Provider=Gemini
  ```

**Receipt scanning specifically not working?**
- This **always** uses Gemini, even when `Provider=OpenRouter`
- Check `GEMINI_API_KEY` is set correctly
- Vision uses a model fallback chain — check logs for which models were tried
- Add more fallback models in `config.ini` if needed:
  ```ini
  [AI.Vision]
  FallbackModels=gemini-2.5-flash-lite,gemini-2.5-flash,gemini-2.5-pro
  ```

### "Permission denied" or "port already in use"

**Problem:** HomeStoq can't bind to port (default is 5050).

**Solutions:**
1. Change `HostUrl` in `config.ini`: `HostUrl=http://*:8080` (or any available port)
2. On Linux/Mac: run with `sudo` for ports below 1024
3. Check what's using the port: `lsof -i :5050`

---

## Next Steps

Now that you're set up:

- 📖 Learn the [daily workflows](02-usage-guide.md) (scanning receipts, voice commands, etc.)
- ⚙️ Explore [configuration options](03-configuration.md)
- 🔧 Understand the [system architecture](04-architecture.md)
- 🐛 Having issues? Check the [Scraper Troubleshooting](07-scraper.md#troubleshooting)

---

## Still Stuck?

If you've tried the above and still have issues:

1. Check the logs: `npm run scraper` shows detailed output
2. Enable debug logging: set `LOG_LEVEL=Debug` in `.env`
3. [Open a GitHub Issue](https://github.com/Christian-Gennari/HomeStoq/issues) with:
   - What you tried
   - Relevant log snippets
   - Your OS and HomeStoq version
