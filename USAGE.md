# 📖 HomeStoq Usage Guide

Welcome to **HomeStoq**! This guide explains how to use the system in your daily life to keep your pantry organized and your shopping list smart.

---

## 🏗️ The Core Philosophy
HomeStoq is designed to be **unobtrusive**. You shouldn't have to manually type in every item you buy. Instead, use your phone's camera for receipts and your voice assistant for usage.

---

## 🛒 1. The "Grocery Run" (Receipt Scanning)
When you come home from the store, don't manually enter your items. Use the **Scan** tab.

### How to use it:
1. Open HomeStoq on your phone (e.g., `http://192.168.1.50:8080`).
2. Go to the **Scan** tab.
3. Choose your method:
    - **📷 Take Photo**: Best for physical receipts. This opens your phone's camera directly.
    - **📁 Upload File**: Best for digital receipts (PDFs) or photos you've already taken (JPG/PNG).
4. Tap **Scan & Analyze**.

### What happens:
- **Gemini AI** reads the receipt or document, extracting item names, quantities, and prices.
- It **normalizes** names (e.g., *"Organic 2.5L Whole Milk"* becomes just *"Milk"*).
- Your inventory is updated instantly.
- A history entry is created so the AI can learn your consumption patterns.

---

## 🗣️ 2. The "Used It" (Voice Sync)
When you finish a carton of milk or use the last of the eggs, just tell your voice assistant.

### How to set it up:
- Tasks are read from your **default Google Tasks list** (no setup required).
- Say: *"Hey Google, add 'used the last milk' to my tasks."*
- **Optional**: To use a custom list, set `GOOGLE_TASKS_LIST_NAME=YourListName` in your `.env` file and say: *"Hey Google, add 'used the last milk' to my [YourListName] list."*

### Practical Examples:
- **"Used 1 milk"** → Decreases Milk by 1.
- **"Bought 5 apples"** → Increases Apples by 5.
- **"Finished the eggs"** → Sets Eggs to 0.
- **"Need more coffee"** → Adds Coffee to your shopping list (AI will prioritize this).

### Timing:
The HomeStoq server polls your Google Tasks every **10 seconds**. Once processed, the task will be automatically deleted from your list.

---

## 📋 3. The "Kitchen Audit" (Manual Control)
Sometimes you just need to check what's in the pantry or fix a small mistake.

### How to use it:
- Use the **Stock** tab to see your current inventory.
- Tap **+** or **-** to quickly adjust quantities.
- Use the **"Add Item Manually"** field at the bottom to track something new that wasn't on a receipt.

---

## 💡 4. The "Next Shop" (Smart List)
Before you head to the store, check the **Smart List** tab.

### How it works:
- Tap **"Analyze Patterns"**.
- Gemini looks at your **last 30 days of history** and your **current stock levels**.
- It suggests what you are likely to run out of soon, even if you still have some left.
- It provides a **Reason** for each suggestion (e.g., *"You usually buy milk every 4 days, and it's been 5 days"*).

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
- Don't worry about brand names. HomeStoq tries to keep things simple (e.g., "Bread" instead of "Sunbeam Toaster Bread"). This makes historical tracking much more accurate.

---

## ❓ Troubleshooting
- **Items not updating?** Check the logs in your Docker container or console.
- **Voice sync slow?** Ensure your `key.json` is correctly placed and Google Tasks API is enabled.
- **Voice sync not working?** Verify the `GOOGLE_TASKS_LIST_NAME` in your `.env` matches your list name (or leave it unset to use the default list).
- **OCR failing?** Ensure your Gemini API key is valid and has not reached its quota.
