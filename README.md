# HomeStoq 🏠🍎

**HomeStoq** is a lightweight, AI-powered pantry management system designed to run on a local network. It helps you track inventory effortlessly using receipt scanning, voice commands via Google Keep, predictive analysis to generate smart shopping lists, and a conversational AI chatbot to query your pantry.

---

### 📖 Quick Links
- **[Practical Usage Guide (USAGE.md)](USAGE.md)** — *Start here for daily workflows!*
- **[Technical Specification (_docs/spec.md)](_docs/spec.md)** — *Deep dive into the architecture.*

---

## 🚀 Features

-   **Stock Tracking**: Real-time view of your pantry items with manual override.
-   **Receipt OCR**: Snap a photo of your grocery receipt or upload a PDF/Image, and HomeStoq (powered by Gemini) will automatically extract items, quantities, and prices to update your inventory.
-   **Voice Sync**: Integrate with Google Keep using a C# Playwright scraper. Simply say "slut på ägg" or "köpte mjölk" to your Google Nest Mini, and the keep-scraper service will process the change from your "inköpslistan" list.
-   **AI Pantry Chatbot**: Ask questions about your pantry in natural language. The chatbot uses function calling to query stock levels, consumption history, and more — all through a sleek slide-over chat UI.
-   **Smart Shopping List**: Predictive analysis based on your 30-day consumption history and current stock levels to suggest what you need to buy next.
-   **Receipts History**: View all past scanned receipts with expandable item details and total spent.
-   **Privacy-First**: Runs locally in Docker with a SQLite database.

## 🛠 Tech Stack

-   **Backend**: ASP.NET Core 10 (Minimal APIs)
-   **Database**: SQLite (Entity Framework Core)
-   **AI Engine**: Google Gemini (OCR, Parsing, Prediction, Chat) via `Google.GenAI` + `Microsoft.Extensions.AI` (`IChatClient`)
-   **Frontend**: Vanilla HTML5, CSS3, JavaScript with Alpine.js (lightweight reactivity)
-   **Voice Queue**: Google Keep (via C# Playwright scraper)
-   **Containerization**: Docker (Alpine Linux)

## ⚙️ Setup & Installation

The easiest way to get started is using the provided `npm` scripts.

### 1. Initial Setup
Run the setup script to initialize your `.env` file and check for required tools:
```bash
npm run setup
```
**Action Required:** Open the newly created `.env` file and add your `GEMINI_API_KEY`.

### 2. Running the Application

| Workflow | Command | Description |
| :--- | :--- | :--- |
| **Local Development** | `npm run dev` | Start both API and Scraper locally (fastest) |
| **Hybrid Development**| `npm run dev:docker` | API in Docker + Scraper locally |
| **API Only (Local)**  | `npm run api` | Start only the backend locally |
| **API Only (Docker)** | `npm run api:docker` | Start only the backend in Docker |
| **Scraper Only**      | `npm run scraper`| Start only the voice scraper |

#### First-time Scraper Login:
When you first run the scraper, a browser window opens. Log into Google Keep manually. Your session is saved to `browser-profile/` so you only need to log in once. If the browser doesn't open or you need to install it, run:
```bash
npm run playwright:install
```

> [!CAUTION]
> **Use a dedicated Google account:** To avoid any risk of your primary Google account being flagged for automated activity (crawling), it is **highly recommended** to create a new, dedicated Google account specifically for HomeStoq. You can then share your "inköpslistan" list with this new account.

### 3. Other Commands
- `npm run stop`: Stop all Docker containers.
- `npm run clean`: Remove all build artifacts (`bin`, `obj`, `out`).
- `npm run help`: Show all available commands and notes.

---

## 📋 Prerequisites

-   [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
-   [Node.js](https://nodejs.org/) (for project scripts)
-   [Docker](https://www.docker.com/) (optional, for containerization)
-   [Google AI Studio API Key](https://aistudio.google.com/) (for Gemini)
-   [Google Account](https://myaccount.google.com/) (for Google Keep/Voice Sync)


## 📖 Usage

For a detailed, day-to-day guide on how to use HomeStoq effectively, check out our **[Practical Usage Guide (USAGE.md)](USAGE.md)**.

1.  **Inventory**: Use the "Stock" tab to see what you have. Use the `+` and `-` buttons for manual adjustments.
2.  **Scan Receipts**: Go to the "Scan" tab. You can either take a photo directly or upload a file (PDF/Image). Tap **Process with Gemini**, and Gemini will process the receipt and update your inventory.
3.  **Voice Commands**: Add items to your Google Keep list (default: "inköpslistan") like "slut på mjölk" or "köpte 5 äpplen". The keep-scraper polls every ~45 seconds (with jitter), parses the text via the API, updates the stock, and deletes the processed item from your list.
4.  **AI Chat**: Click the "Chat" button in the nav to open the pantry assistant. Ask about stock levels, consumption history, or anything related to your pantry — the AI uses function calling to query your data in real time.
5.  **Shopping List**: Click "Generate List" in the "List" tab to see AI-generated suggestions based on your history.
6.  **Receipts History**: View all your past scanned receipts, expand to see individual items and total spent.

## 📝 License
MIT License. See [LICENSE](LICENSE) for details.
