# HomeStoq Usage Guide

📖 **Documentation Index:**
[README](../README.md) | [Getting Started](01-getting-started.md) | Usage Guide | [Configuration](03-configuration.md) | [Scraper](07-scraper.md)

Welcome! This guide covers the everyday workflows for using HomeStoq effectively.

---

## The Philosophy: Zero Friction

HomeStoq is designed to **stay out of your way**. You shouldn't manually type every item you buy. Instead:

- **Scan receipts** with your phone camera
- **Talk to Google** when you use things up
- **Ask the AI** when you wonder what's left

That's it. Let the system do the work.

---

## 🛒 Workflow 1: The Grocery Run (Receipt Scanning)

When you get home from the store, scan your receipt. That's it.

### How to Do It

1. **Open HomeStoq on your phone** (e.g., `http://192.168.1.50`)
2. **Go to the Scan tab**
3. **Choose your method:**
   - 📷 **Take Photo** — Best for physical receipts (opens camera directly)
   - 📁 **Upload** — For digital receipts or photos you already took
4. **Tap "Process with Gemini"**

### What Happens Behind the Scenes

1. **Gemini reads the receipt** using chain-of-thought reasoning:
   - First, it extracts raw text from the image
   - Then expands truncated names ("ORG 2.5L W MILK" → "Organic 2.5L Whole Milk")
   - Finally normalizes to generic names ("Organic 2.5L Whole Milk" → "Mjölk")
2. **Creates a Receipt record** in the database
3. **Updates your inventory** — all items added instantly
4. **Saves to History** — so the AI learns your patterns

> 💡 **Tip:** Lay receipts flat with good lighting. One receipt at a time works best.

---

## 🗣️ Workflow 2: The "Used It" (Voice Commands)

When you finish something — milk, eggs, coffee — just tell Google.

### Setup (One-Time)

1. Make sure the scraper is running: `npm run dev`
2. Verify `BrowserMode=RemoteDebugging` and `KeepListName` in `config.ini` 
3. Chrome should open and you should be logged into Google Keep (see [Getting Started](01-getting-started.md))

### Daily Use

Just say to your Google Assistant:

| You Say | What Happens |
|---------|--------------|
| *"Hey Google, add 'slut på mjölk' to my inköpslistan"* | Milk stock decreases by 1 |
| *"Hey Google, add 'köpte 5 ägg' to my inköpslistan"* | Eggs increase by 5 |
| *"Hey Google, add 'använt allt kaffe' to my inköpslistan"* | Coffee set to 0 |
| *"Hey Google, add 'lägg till bröd' to my inköpslistan"* | Bread added to inventory |

### How It Actually Works

Don't worry about the magic, but if you're curious:

1. **Google Assistant** adds text to your Google Keep list
2. **HomeStoq scraper** polls your Keep list every ~45 seconds (randomized)
3. **Gemini AI** parses the text to understand what you mean
4. **Inventory updates** — stock changed instantly
5. **Cleanup** — item is checked off and deleted from your Keep list

> ⚠️ **Important:** Use a dedicated Google account for HomeStoq, not your main 
> account. Configure `KeepListName` in `config.ini` (not `.env`). This protects 
> your primary account from any automation flags. Share the list with your main 
> account if needed.

### Timing Details

- **Poll interval:** ~45 seconds (±15 seconds random jitter)
- **Active hours:** Default 07:00-23:00 (configurable)
- **Outside active hours:** Scraper sleeps, resumes at 07:00
- **Processing time:** Usually under 1 minute total (voice → updated inventory)

---

## 🤖 Workflow 3: The Pantry Chat (Ask Anything)

Wondering what you have? Just ask.

### How to Use It

1. **Click the Chat button** in the navigation bar (right side)
2. **Type naturally** — no special syntax needed
3. **Get instant answers** — the AI queries your actual inventory

### What You Can Ask

| Question | What You Get |
|----------|--------------|
| *"How much milk do I have?"* | Current stock level |
| *"What did I buy last week?"* | Recent purchases from history |
| *"Show me my full inventory"* | Complete pantry list |
| *"What's running low?"* | Items at 0 or 1 quantity |
| *"How much coffee this month?"* | Consumption statistics |
| *"What vegetables do I have?"* | Items filtered by category |

### How It Works

The chatbot uses **function calling** — it decides which database query to run based on your question:

- Asks about stock → queries inventory table
- Asks about history → queries history table
- Composes a natural answer from the results

The AI responds in the language configured in your `config.ini` (Swedish or English).

> 💡 **Tip:** Be specific. "How much milk?" works better than "What do I have?"

---

## 📋 Workflow 4: The Kitchen Audit (Manual Control)

Sometimes you just need to check or fix something quickly.

### Stock Tab

- See everything you have, grouped by category
- **Tap + or -** to adjust quantities
- **Search** to find specific items
- **"+ Add Item"** to track something new manually

### When to Use Manual Mode

- Adding items not on receipts (gifts, garden produce, etc.)
- Fixing mistakes from voice commands
- Quick inventory checks

---

## 📋 Workflow 5: The Next Shop (Shopping Buddy)

Before shopping, use the **Shopping Buddy** to build your list.

### How to Use It

1. **Go to the Shopping tab**
2. **Choose your starting point:**
   - 🤖 **"Generate Suggestions"** — Let the AI analyze your habits and current stock to suggest what you need.
   - 💬 **"Chat with Buddy"** — Tell the AI what you're planning: "I'm making lasagna this weekend" or "We need staples for the week."
3. **Refine your list:**
   - The AI will suggest items (e.g., "I've added minced meat and pasta to your list").
   - It cross-references your inventory: "You already have onions, so I didn't add those."
   - You can add/remove items manually or through chat.
4. **Save and Shop:**
   - Once ready, tap **"Save List"** to keep it for later.
   - When at the store, enter **"Shopping Mode"** to check off items as you buy them.

### Why It's Better Than a Static List

- **Context-Aware:** Knows what you have and what you typically buy.
- **Recipe Planning:** "I'm making tacos" → AI adds tortillas, salsa, and beef.
- **Conversational:** Just talk to it like a helper.
- **Persistent:** Save lists for different purposes (Weekly staples, Party prep, etc.).

> 💡 **Tip:** Tell the Shopping Buddy your meal plans for the week, and it will handle the ingredient check for you.


---

## 🧾 Workflow 6: Receipt History

Review all your grocery purchases.

### How

1. **Go to the Receipts tab**
2. **Browse receipts** — store name, date, total amount
3. **Click to expand** — see individual items, original receipt text, and expanded names

### Why It's Useful

- Track spending over time
- Find where you bought something
- See the original receipt text vs. normalized names

---

## Tips for Success

### Better Receipt Scanning

- 📄 **Flat surface:** Lay receipts flat, not crumpled
- 💡 **Good lighting:** Avoid shadows and glare
- 📱 **Focus:** Make sure text is sharp and readable
- 🔄 **One at a time:** Scan individually for best accuracy

### Better Voice Commands

- **Keep it simple:** "slut på mjölk" beats "we totally ran out of that delicious organic whole milk"
- **Quantity helps:** "köpte 5 ägg" is clearer than just "ägg"
- **Actions work:** "slut på" (out of), "köpte" (bought), "använt" (used) all work

### Better Item Names

Don't worry about brands. The system normalizes:
- "ICA Kärnmjölk 1L" → "Mjölk"
- "Starbucks Pike Place Roast" → "Kaffe"
- "Lök Gul 1kg Klass 1" → "Lök"

This makes historical tracking accurate across different stores and brands.

### Better Chat Questions

- ✅ "How much milk do I have?"
- ✅ "What vegetables are running low?"
- ✅ "Did I buy coffee last week?"
- ❌ "What do I have?" (too vague)
- ❌ "Tell me about my pantry" (the AI will ask what you want to know)

---

## Troubleshooting Common Issues

### Voice Commands Not Working

**Symptom:** You say "slut på mjölk" but inventory doesn't update.

**Check:**
1. Is the scraper running? (`npm run scraper` in terminal)
2. Is Chrome open and logged into Keep?
3. Is the item appearing in your Google Keep list?
4. Are you within ActiveHours? (Default: 07:00-23:00)
5. Does `KeepListName` in config.ini exactly match your list name?

**Debug:**
- Watch the scraper logs — do you see "Processing: slut på mjölk"?
- If yes, check if the API call succeeds (200 OK)
- If no, check if the scraper is finding your list

### Receipt Scanning Fails

**Symptom:** "AI analysis failed" or empty results.

**Check:**
1. Is your `GEMINI_API_KEY` valid and not expired?
2. Have you hit rate limits? (Free tier allows limited requests)
3. Is the receipt image clear and readable?

**Try:**
- Different lighting
- Different angle
- Manual focus on phone camera

### Chat Not Responding

**Symptom:** Messages don't get replies.

**Check:**
1. Is the API running? (`npm run api`)
2. Is your Gemini API key valid?
3. Check browser console for JavaScript errors

---

## Getting Help

If you're stuck:

1. 📖 Check the [Getting Started Guide](01-getting-started.md)
2. 🔧 Review [Configuration Options](03-configuration.md)
3. 🐛 Look at [GitHub Issues](https://github.com/Christian-Gennari/HomeStoq/issues)
4. 📝 [Create a new issue](https://github.com/Christian-Gennari/HomeStoq/issues/new) with:
   - What you tried
   - What you expected
   - What actually happened
   - Relevant log output
