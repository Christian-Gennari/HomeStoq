# 📖 HomeStoq Usage Guide

Welcome to **HomeStoq**! This guide explains how to use the system in your daily life to keep your pantry organized and your shopping list smart.

---

## 🏗️ The Core Philosophy
HomeStoq is designed to be **unobtrusive**. You shouldn't have to manually type in every item you buy. Instead, use your phone's camera for receipts, your voice assistant for usage, and the AI chatbot to query your pantry.

---

## 🛒 1. The "Grocery Run" (Receipt Scanning)
When you come home from the store, don't manually enter your items. Use the **Scan** tab.

### How to use it:
1. Open HomeStoq on your phone (e.g., `http://192.168.1.50`).
2. Go to the **Scan** tab.
3. Choose your method:
    - **📷 Capture Photo**: Best for physical receipts. This opens your phone's camera directly.
    - **📁 Upload Document**: Best for digital receipts (PDFs) or photos you've already taken (JPG/PNG).
4. Tap **Process with Gemini**.

### What happens:
- **Gemini AI** reads the receipt using chain-of-thought reasoning: first extracting raw text, then expanding truncated names, and finally normalizing to a generic category.
- It **normalizes** names (e.g., *"Organic 2.5L Whole Milk"* becomes just *"Mjölk"*).
- A **Receipt record** is created in the database, and all items are linked to it.
- Your inventory is updated instantly.
- A history entry is created so the AI can learn your consumption patterns.

---

## 🗣️ 2. The "Used It" (Voice Sync)
When you finish a carton of milk or use the last of the eggs, just tell your voice assistant.

> [!CAUTION]
> **Use a dedicated Google account:** To avoid any risk of your primary Google account being flagged for automated activity, it is **highly recommended** to create a new, dedicated Google account specifically for HomeStoq. You can then share your "inköpslistan" list with this new account to maintain sync with your phone/Nest device.

### How to set it up:
- Items are read from your **Google Keep** list named **"inköpslistan"** (default).
- Say: *"Hey Google, add 'slut på ägg' to my inköpslistan."*
- **Optional**: To use a custom list, edit `KeepListName` in `config.ini` and say: *"Hey Google, add 'slut på ägg' to my [YourListName] list."*

### Practical Examples (Swedish):
- **"Slut på mjölk"** → Decreases Mjölk by 1.
- **"Köpte 5 ägg"** → Increases Ägg by 5.
- **"Använt allt kaffe"** → Sets Kaffe to 0.
- **"Lägg till bröd"** → Adds Bröd to your inventory.

### Timing:
The keep-scraper microservice polls your Google Keep every **~45 seconds** (with ±15s random jitter). It only runs during active hours (default: 07-23) to avoid 24/7 bot patterns. Once processed, the item is automatically checked off **and deleted** from your list to keep it clean.

---

## 🤖 3. The "Pantry Chat" (AI Assistant)
Ask questions about your pantry in natural language. The AI chatbot has direct access to your inventory data through function calling.

### How to use it:
1. Click the **Chat** button in the navigation bar (highlighted in dark).
2. A slide-over chat panel opens on the right side.
3. Type your question in natural language and press Enter or click Send.

### What you can ask:
- **"How much milk do I have?"** → The AI queries stock levels via function calling.
- **"What did I buy last week?"** → The AI checks consumption history.
- **"Show me my full inventory"** → The AI retrieves the complete pantry list.
- **"What's running low?"** → The AI cross-references stock levels to identify low items.
- **"How much coffee have I consumed this month?"** → The AI filters history by category and date range.

### How it works:
The chatbot uses **Microsoft.Extensions.AI** with automatic function invocation. When you ask a question, the AI decides which tools to call (`GetStockLevel`, `GetFullInventory`, or `GetConsumptionHistory`), executes them against your SQLite database, and uses the results to compose an informed answer — all in a single request.

---

## 📋 4. The "Kitchen Audit" (Manual Control)
Sometimes you just need to check what's in the pantry or fix a small mistake.

### How to use it:
- Use the **Stock** tab to see your current inventory.
- Tap **+** or **-** to quickly adjust quantities.
- Use the **"+ Add Item"** button to track something new that wasn't on a receipt.

---

## 💡 5. The "Next Shop" (Smart List)
Before you head to the store, check the **List** tab.

### How it works:
- Tap **"Generate List"**.
- Gemini looks at your **last 30 days of history** and your **current stock levels**.
- It suggests what you are likely to run out of soon, even if you still have some left.
- It provides a **Reason** for each suggestion (e.g., *"You usually buy milk every 4 days, and it's been 5 days"*).

---

## 🧾 6. Receipts History
Review all your past grocery purchases in one place.

### How to use it:
- Go to the **Receipts** tab.
- Each receipt card shows the store name, date, and total amount.
- Click a receipt to expand it and see all individual items purchased, including the original receipt text and the AI-expanded product name.

---

## 🌟 Tips for Success

### Better Receipt OCR
- **Flat Surface**: Lay the receipt flat.
- **Lighting**: Avoid harsh shadows.
- **Focus**: Ensure the text is sharp.
- **One at a time**: Scan each receipt individually for better accuracy.

### Better Voice Commands
- Stick to simple "Item + Quantity" or "Action + Item" phrases.
- Gemini is smart, but "we are totally out of those delicious red apples" might be less reliable than "used all apples."

### Item Naming
- Don't worry about brand names. HomeStoq tries to keep things simple (e.g., "Bröd" instead of "Sunbeam Toaster Bread"). This makes historical tracking much more accurate.

### Better Chat Queries
- Be specific: "How much milk?" works better than "What do I have?"
- The AI responds in the same language configured in `config.ini` (Swedish or English).

---

## ❓ Troubleshooting
- **Items not updating?** Check the logs in your Docker container or console.
- **Voice sync not starting?** Make sure the scraper is running (`npm run scraper` or `dotnet run --project src/HomeStoq.Plugins/HomeStoq.Plugins.GoogleKeepScraper`). If Google requires re-login, log in again in the browser window.
- **Voice sync not working?** Verify the `KeepListName` in `config.ini` matches your Google Keep list name (default: "inköpslistan").
- **OCR failing?** Ensure your Gemini API key is valid and has not reached its quota.
- **Scraper not polling during expected hours?** Check the `ActiveHours` setting in `config.ini`. The scraper only operates between the configured start and end hours (default: 07-23).
- **Chat not responding?** Check that the AI service is running and your API key is valid. The chat requires an active connection to Gemini.
