# HomeStoq: Future Improvements

A prioritized list of enhancements and future work for HomeStoq.

---

## Scraper Enhancements

### High Priority

#### Remote Debugging Mode (Anti-Detection)
**Issue:** Google can detect Playwright's automation signals at the process level.

**Proposed Solution:**
- Implement `chromium.connectOverCDP()` instead of launching a fresh browser
- User launches Chrome with `--remote-debugging-port=9222` before starting scraper
- Scraper attaches to existing Chrome instance, using real profile/cookies/IP
- This would make detection nearly impossible

**Status:** Not started

---

#### Scraper Configuration UI
**Issue:** Currently, changing scraper settings requires editing `config.ini` manually.

**Proposed Solution:**
- Add a `/settings` page in the web UI
- Allow configuring `ActiveHours`, `PollIntervalSeconds`, `KeepListName` at runtime
- Persist changes back to `config.ini`

**Status:** Not started

---

### Medium Priority

#### Multiple Google Account Support
**Issue:** Currently only one Google account is supported per scraper instance.

**Proposed Solution:**
- Allow configuring multiple `KeepListName` entries to span different accounts
- Each account would have its own browser profile directory
- Run multiple scraper instances or multiplex within one

**Status:** Not started

---

#### Scraper Dashboard
**Issue:** No visibility into scraper health, last poll time, or processed items.

**Proposed Solution:**
- Add a `/api/scraper/status` endpoint
- Expose: last poll timestamp, items processed this session, active hours status
- Display in web UI "System" tab

**Status:** Not started

---

## General Enhancements

### Completed

#### AI Chat with Function Calling
**Status:** ✅ Complete

The pantry chatbot is fully implemented with:
- `IChatClient` via `Microsoft.Extensions.AI` + `Google.GenAI`
- Function invocation middleware (`.UseFunctionInvocation()`)
- Three registered tools: `GetStockLevel`, `GetFullInventory`, `GetConsumptionHistory`
- `/api/chat` endpoint with conversation history
- Alpine.js slide-over chat UI with message history

#### Receipts History & Chain-of-Thought OCR
**Status:** ✅ Complete

- `Receipts` table with `ReceiptId` FK on `History`
- Chain-of-thought prompt: `ReceiptText` → `ExpandedName` → `ItemName`
- `/api/receipts/scan` creates Receipt first, then links items
- Receipts history view with expandable item details
- `ExpandedName` column for preserving full product names

### Medium Priority

#### Push Notifications
**Issue:** Users have no way to know if voice commands are being processed without checking the web UI.

**Proposed Solution:**
- Integrate with Home Assistant or a notification service (Pushover, Gotify)
- Send push when item is processed or if scraper encounters an error

**Status:** Not started

---

#### Receipt Scanner Mobile Optimizations
**Issue:** The receipt upload flow could be smoother on mobile devices.

**Proposed Solution:**
- Add drag-and-drop for file upload
- Improve camera capture UX
- Add image cropping/rotation before upload

**Status:** Not started

---

#### Historical Analytics Dashboard
**Issue:** Currently, only 30-day prediction is shown. No historical trends.

**Proposed Solution:**
- Add "Trends" tab showing:
  - Items consumed per week/month
  - Spending over time
  - Most frequently purchased items
- Use existing `History` table data

**Status:** Not started

---

## Technical Debt

### Low Priority

#### Unit Tests for GeminiService
**Issue:** No test coverage for AI parsing logic.

**Proposed Solution:**
- Mock Gemini API responses
- Test voice command parsing (Swedish/English)
- Test receipt OCR normalization
- Test shopping list generation

**Status:** Not started

---

#### Playwright E2E Tests
**Issue:** Manual testing required for scraper UI interactions.

**Proposed Solution:**
- Add E2E tests that launch KeepScraper against a test Google account
- Verify checkbox clicking and deletion work correctly

**Status:** Not started

---

#### Docker Compose Health Checks
**Issue:** No way to know if API or scraper is unhealthy via `docker ps`.

**Proposed Solution:**
- Add `HEALTHCHECK` directives to Dockerfile
- Expose `/health` endpoint on API
- Monitor scraper via periodic heartbeat log

**Status:** Not started
