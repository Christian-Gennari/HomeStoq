const i18n = {
  English: {
    "nav.stock": "Stock",
    "nav.scan": "Scan",
    "nav.receipts": "Receipts",
    "nav.list": "Shopping Lists",
    "nav.chat": "Chat",
    "pulse.total": "Total Items",
    "pulse.low": "Low Stock",
    "pulse.cats": "Categories",
    "inv.search": "Find an item...",
    "inv.add": "+ Add Item",
    "inv.refreshing": "Refreshing your pantry...",
    "inv.empty.title": "Your pantry is empty",
    "inv.empty.desc": "Start by scanning a receipt or adding an item manually.",
    "inv.updated": "Last updated:",
    "scan.title": "Scan Receipt",
    "scan.desc":
      "Our AI identifies items, quantities, and prices from any grocery receipt.",
    "scan.photo": "Capture Photo",
    "scan.doc": "Upload Document",
    "scan.ready": "Ready to scan:",
    "scan.btn": "Process with Gemini",
    "scan.btn.active": "Analyzing...",
    "scan.results": "Recent Scan Results",
    "receipts.desc": "Your purchase history from scanned receipts.",
    "receipts.empty": "No receipts scanned yet.",
    "list.title": "Predictive Suggestions",
    "list.desc": "Consumption-based logic trained on your history.",
    "list.btn": "Generate List",
    "list.btn.active": "Analyzing history...",
    "list.empty": "No suggestions yet. Scan more receipts to train the AI.",
    "list.qty": "Qty:",
    "prompt.add": "Enter item name:",
    "modal.cancel": "Cancel",
    "modal.confirm": "Add",
    "toast.err.stock": "Error loading stock",
    "toast.err.update": "Failed to update quantity",
    "toast.err.scan": "AI analysis failed",
    "toast.err.list": "Could not generate list",
    "toast.scan.success": "Successfully scanned {0} items",
    "category.other": "Other",
    "chat.title": "Pantry Chat",
    "chat.placeholder": "Ask your pantry...",
    "chat.send": "Send",
    "shop.greeting": "Hey! I've been keeping an eye on your pantry.",
    "shop.followUpPlaceholder": "Reply to the AI...",
    "shop.commit": "Start Shopping 🛒",
    "shop.complete": "Done Shopping ✓",
    "shop.showDismissed": "Show dismissed ({0})",
    "shop.hideDismissed": "Hide dismissed",
    "shop.addCustom": "+ Add custom item",
    "shop.copied": "List copied to clipboard!",
    "shop.empty": "No shopping list yet. Generate one to get started!",
    "shop.generate": "Generate New List",
    "shop.generating": "Thinking...",
    "shop.replied": "Reply sent!",
    "shop.itemAdded": "Item added",
    "shop.itemUpdated": "Item updated",
    "shop.listCommitted": "Shopping list ready!",
    "shop.listCompleted": "Great job! List archived.",
    "shop.needNow": "Need Now",
    "shop.soon": "Soon",
    "shop.maybe": "Maybe",
    "shop.history": "Past Lists",
    "shop.noHistory": "No completed lists yet.",
  },
  Swedish: {
    "nav.stock": "Skafferi",
    "nav.scan": "Skanna",
    "nav.receipts": "Kvitton",
    "nav.list": "Inköpslistor",
    "nav.chat": "Chatt",
    "pulse.total": "Totalt antal",
    "pulse.low": "Behöver fyllas på",
    "pulse.cats": "Kategorier",
    "inv.search": "Sök efter en vara...",
    "inv.add": "+ Lägg till",
    "inv.refreshing": "Uppdaterar skafferiet...",
    "inv.empty.title": "Ditt skafferi är tomt",
    "inv.empty.desc":
      "Börja med att skanna ett kvitto eller lägg till manuellt.",
    "inv.updated": "Uppdaterad:",
    "scan.title": "Skanna kvitto",
    "scan.desc":
      "Vår AI identifierar varor, antal och priser från dina kvitton.",
    "scan.photo": "Ta en bild",
    "scan.doc": "Ladda upp",
    "scan.ready": "Redo att skanna:",
    "scan.btn": "Analysera med Gemini",
    "scan.btn.active": "Analyserar...",
    "scan.results": "Senaste skanning",
    "receipts.desc": "Din köphistorik från skannade kvitton.",
    "receipts.empty": "Inga kvitton skannade ännu.",
    "list.title": "Smarta förslag",
    "list.desc":
      "Förslag baserade på din historik från tidigare inköp.",
    "list.btn": "Skapa lista",
    "list.btn.active": "Analyserar historik...",
    "list.empty": "Inga förslag ännu. Skanna fler kvitton för att träna AI:n.",
    "list.qty": "Antal:",
    "prompt.add": "Ange varans namn:",
    "modal.cancel": "Avbryt",
    "modal.confirm": "Lägg till",
    "toast.err.stock": "Kunde inte hämta skafferiet",
    "toast.err.update": "Kunde inte uppdatera antal",
    "toast.err.scan": "AI-analysen misslyckades",
    "toast.err.list": "Kunde inte skapa förslag",
    "toast.scan.success": "Skannade {0} varor",
    "category.other": "Övrigt",
    "chat.title": "Skafferichatt",
    "chat.placeholder": "Fråga skafferiet...",
    "chat.send": "Skicka",
    "shop.greeting": "Hej! Jag har hållit koll på ditt skafferi.",
    "shop.followUpPlaceholder": "Svara AI:n...",
    "shop.commit": "Börja Handla 🛒",
    "shop.complete": "Klar ✓",
    "shop.showDismissed": "Visa avvisade ({0})",
    "shop.hideDismissed": "Dölj avvisade",
    "shop.addCustom": "+ Lägg till egen vara",
    "shop.copied": "Listan kopierad till urklipp!",
    "shop.empty": "Ingen inköpslista än. Skapa en för att börja!",
    "shop.generate": "Skapa Ny Lista",
    "shop.generating": "Funderar...",
    "shop.replied": "Svar skickat!",
    "shop.itemAdded": "Vara tillagd",
    "shop.itemUpdated": "Vara uppdaterad",
    "shop.listCommitted": "Inköpslistan redo!",
    "shop.listCompleted": "Bra jobbat! Listan arkiverad.",
    "shop.needNow": "Behövs Nu",
    "shop.soon": "Snart",
    "shop.maybe": "Kanske",
    "shop.history": "Tidigare Listor",
    "shop.noHistory": "Inga färdiga listor än.",
  },
};

function pantryApp() {
  return {
    view: "inventory",
    items: [],
    search: "",
    loading: false,

    // Settings State
    language: "English",

    // Receipt State
    selectedFile: null,
    scanning: false,
    scanResults: [],

    // Receipts History
    receipts: [],
    receiptItems: [],
    expandedReceiptId: null,

    // Shopping / Buy List Factory State
    shopSubtab: 'create', // 'create' | 'view'
    savedLists: [],       // Array of saved list summaries
    buyList: {
      id: null,
      status: null, // 'draft', 'active', 'completed', 'saved'
      savedName: "",
      messages: [],        // Chat conversation history [{ role, content, timestamp, actions }]
      items: [],           // Current shopping list items
      pendingActions: [],  // AI proposed changes awaiting confirmation
      suggestedReplies: [], // Quick reply buttons from AI
      isLoading: false,    // AI is thinking
      sessionMode: 'brainstorming', // 'brainstorming', 'reviewing'
      chatInput: "",       // Current user input
      customName: "",     // For saving
    },

    // Old shopping state (to be removed)
    suggestions: [],
    loadingSuggestions: false,

    // Chat State
    showChat: false,
    chatInput: "",
    chatHistory: [],
    chatLoading: false,

    // UI State
    toasts: [],

    // Modal State
    showAddModal: false,
    newItemName: "",

    async init() {
      await this.fetchSettings();
      this.refreshInventory();

      // Watch view changes to refresh data
      this.$watch("view", (value) => {
        if (value === "inventory") this.refreshInventory();
        if (value === "receipts_history") this.loadReceipts();
        if (value === "shopping") this.loadCurrentBuyList();
      });
    },

    async fetchSettings() {
      try {
        const res = await fetch("/api/settings");
        if (res.ok) {
          const data = await res.json();
          if (data.language === "Swedish") {
            this.language = "Swedish";
          }
        }
      } catch (e) {
        console.error("Failed to load settings", e);
      }
    },

    isLowStock(item) {
      if (item.quantity == 0) return "low-stock";
      if (item.quantity == 1) return "low-stock-warning";
      return "";
    },

    getLocale() {
      return this.language === "Swedish" ? "sv-SE" : "en-US";
    },

    getCurrency() {
      return this.language === "Swedish" ? "SEK" : "USD";
    },

    t(key, ...args) {
      const dict = i18n[this.language] || i18n["English"];
      let text = dict[key] || key;
      args.forEach((arg, i) => {
        text = text.replace(`{${i}}`, arg);
      });
      return text;
    },

    get categorizedItems() {
      const filtered = this.items.filter((i) =>
        i.itemName.toLowerCase().includes(this.search.toLowerCase()),
      );

      // Group by category
      const groups = filtered.reduce((acc, item) => {
        const cat = item.category || this.t("category.other");
        if (!acc[cat]) acc[cat] = [];
        acc[cat].push(item);
        return acc;
      }, {});

      // Sort categories alphabetically
      return Object.keys(groups)
        .sort()
        .reduce((acc, key) => {
          acc[key] = groups[key].sort((a, b) =>
            a.itemName.localeCompare(b.itemName),
          );
          return acc;
        }, {});
    },

    async refreshInventory() {
      this.loading = true;
      try {
        const res = await fetch("/api/inventory");
        if (!res.ok) throw new Error("Failed to load inventory");
        this.items = await res.json();
      } catch (err) {
        this.addToast(this.t("toast.err.stock"), "error");
      } finally {
        this.loading = false;
      }
    },

    async updateQty(itemName, change) {
      const item = this.items.find((i) => i.itemName === itemName);
      if (!item && change < 0) return;

      const oldQty = item ? item.quantity : 0;
      const newQty = Math.max(0, oldQty + change);

      if (item) item.quantity = newQty;
      else if (change > 0) {
        this.items.push({
          itemName,
          quantity: newQty,
          category: "Other",
          updatedAt: new Date().toISOString(),
        });
      }

      try {
        const res = await fetch("/api/inventory/update", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ itemName, quantityChange: change }),
        });
        if (!res.ok) throw new Error("Update failed");
        if (!item) this.refreshInventory();
      } catch (err) {
        if (item) item.quantity = oldQty;
        this.addToast(this.t("toast.err.update"), "error");
      }
    },

    async loadReceipts() {
      try {
        const res = await fetch("/api/receipts");
        if (!res.ok) throw new Error("Failed to load receipts");
        this.receipts = await res.json();
      } catch (e) {
        console.error("Failed to load receipts", e);
        this.addToast("Failed to load receipts", "error");
      }
    },

    async toggleReceipt(id) {
      if (this.expandedReceiptId === id) {
        this.expandedReceiptId = null;
        this.receiptItems = [];
      } else {
        this.expandedReceiptId = id;
        try {
          const res = await fetch(`/api/receipts/${id}/items`);
          if (res.ok) this.receiptItems = await res.json();
        } catch (e) {
          console.error("Failed to load receipt items", e);
        }
      }
    },

    toggleChat() {
      this.showChat = !this.showChat;
      if (this.showChat && this.chatHistory.length === 0) {
        const initialMsg = this.language === "Swedish" 
          ? "Hej! Jag är din skafferi-assistent. Hur kan jag hjälpa dig idag?"
          : "Hello! I'm your pantry assistant. How can I help you today?";
        this.chatHistory.push({ id: Date.now(), role: "assistant", content: initialMsg });
      }
    },

    async sendMessage() {
      if (!this.chatInput.trim() || this.chatLoading) return;

      const userMsg = this.chatInput.trim();
      this.chatInput = "";
      
      // Update local history immediately for UX
      this.chatHistory.push({ id: Date.now(), role: "user", content: userMsg });
      this.chatLoading = true;

      this.$nextTick(() => {
        const box = this.$refs.chatBox;
        if (box) box.scrollTop = box.scrollHeight;
      });

      try {
        const res = await fetch("/api/chat", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ 
            message: userMsg, 
            // Send history BEFORE this new message (server adds it)
            // Map to 'text' property which aligns with ChatMessage.Text
            history: this.chatHistory.slice(0, -1).map(m => ({ 
              role: m.role, 
              text: m.content 
            })) 
          })
        });

        if (res.ok) {
          const data = await res.json();
          
          // Re-sync local history with the server's official version
          if (data.history) {
            this.chatHistory = data.history
              .filter(m => (m.role || m.Role) !== 'system')
              .map((m, i) => ({
                id: Date.now() + i,
                role: m.role || m.Role,
                content: m.text || m.Text || ""
              }));
          }

          if (data.reply && (data.reply.toLowerCase().includes("uppdaterat") || data.reply.toLowerCase().includes("updated"))) {
            this.refreshInventory();
          }
        } else {
          throw new Error("Chat failed");
        }
      } catch (e) {
        console.error("Chat error:", e);
        this.chatHistory.push({ 
          id: Date.now(), 
          role: "assistant", 
          content: this.language === "Swedish" ? "Tyvärr uppstod ett fel. Försök igen." : "Sorry, I encountered an error. Please try again." 
        });
      } finally {
        this.chatLoading = false;
        this.$nextTick(() => {
          const box = this.$refs.chatBox;
          if (box) box.scrollTop = box.scrollHeight;
        });
      }
    },

    async manualAdd() {
      this.newItemName = "";
      this.showAddModal = true;
    },

    async submitAddItem() {
      if (!this.newItemName.trim()) return;
      await this.updateQty(this.newItemName.trim(), 1);
      this.showAddModal = false;
      this.newItemName = "";
    },

    // ========== Conversational Buy List Methods ==========

    async loadCurrentBuyList() {
      try {
        const res = await fetch("/api/shopping-list/current");
        if (!res.ok) throw new Error("Failed to load buy list");
        const data = await res.json();
        
        if (data.hasList) {
          this.buyList.id = data.id;
          this.buyList.status = data.status;
          this.buyList.items = data.items || [];
          // Load conversation from messages if available
          if (data.messages) {
            this.buyList.messages = data.messages;
          }
        } else {
          // Reset if no list
          this.resetBuyList();
        }
      } catch (e) {
        console.error("Failed to load buy list", e);
      }
    },

    resetBuyList() {
      this.buyList.id = null;
      this.buyList.status = null;
      this.buyList.savedName = "";
      this.buyList.messages = [];
      this.buyList.items = [];
      this.buyList.pendingActions = [];
      this.buyList.suggestedReplies = [];
      this.buyList.sessionMode = 'brainstorming';
      this.buyList.chatInput = "";
      this.buyList.customName = "";
    },

    async startNewList() {
      this.resetBuyList();
      
      // Create initial greeting message
      const greeting = this.language === "Swedish" 
        ? "Hej! Jag kan hjälpa dig skapa din inköpslista. Vill du att jag föreslår varor baserat på dina tidigare mönster, eller vill du starta från scratch?"
        : "Hey! I can help you create your shopping list. Would you like me to suggest items based on your pantry patterns, or start from scratch?";
      
      this.buyList.messages.push({
        role: "assistant",
        content: greeting,
        timestamp: new Date().toISOString(),
        actions: []
      });
      
      this.buyList.suggestedReplies = this.language === "Swedish"
        ? ["Föreslå baserat på mönster", "Starta tom", "Jag har en plan"]
        : ["Suggest based on patterns", "Start empty", "I have a plan"];
    },

    async sendChatMessage(message = null) {
      const msg = message || this.buyList.chatInput;
      if (!msg || !msg.trim() || this.buyList.isLoading) return;
      
      // Clear input if using the bound input
      if (!message) {
        this.buyList.chatInput = "";
      }
      
      // Add user message immediately for UX
      this.buyList.messages.push({
        role: "user",
        content: msg,
        timestamp: new Date().toISOString()
      });
      
      this.buyList.isLoading = true;
      this.buyList.suggestedReplies = []; // Clear old suggestions
      
      try {
        // If no list ID yet, create one first
        if (!this.buyList.id) {
          const createRes = await fetch("/api/shopping-list/generate", { method: "POST" });
          if (!createRes.ok) throw new Error("Failed to create list");
          const createData = await createRes.json();
          this.buyList.id = createData.id;
          this.buyList.items = createData.items || [];
          // Don't auto-add greeting - wait for first chat response
        }
        
        // Send chat message
        const res = await fetch(`/api/shopping-list/${this.buyList.id}/chat`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ 
            message: msg,
            language: this.language
          }),
        });
        
        if (!res.ok) throw new Error("Chat failed");
        const data = await res.json();
        
        // Add AI response
        this.buyList.messages.push({
          role: "assistant",
          content: data.reply,
          timestamp: new Date().toISOString(),
          actions: data.actions || [],
          requiresConfirmation: data.requiresConfirmation
        });
        
        // Update state
        this.buyList.pendingActions = data.actions || [];
        this.buyList.suggestedReplies = data.suggestedReplies || [];
        this.buyList.items = data.currentItems || this.buyList.items;
        
        // Auto-apply if no confirmation needed
        if (!data.requiresConfirmation && data.actions && data.actions.length > 0) {
          await this.applyActions(data.actions);
        }
        
      } catch (e) {
        console.error("Chat failed", e);
        this.buyList.messages.push({
          role: "assistant",
          content: this.language === "Swedish" 
            ? "Tyvärr, jag kunde inte processa det. Försök igen."
            : "Sorry, I couldn't process that. Please try again.",
          timestamp: new Date().toISOString()
        });
      } finally {
        this.buyList.isLoading = false;
      }
      
      // Scroll to bottom
      this.$nextTick(() => {
        const chatBox = this.$refs.chatThread;
        if (chatBox) chatBox.scrollTop = chatBox.scrollHeight;
      });
    },

    async confirmActions(accept) {
      if (!this.buyList.pendingActions.length) return;
      
      try {
        const res = await fetch(`/api/shopping-list/${this.buyList.id}/confirm`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            accept: accept,
            actions: this.buyList.pendingActions
          }),
        });
        
        if (!res.ok) throw new Error("Confirmation failed");
        const data = await res.json();
        
        this.buyList.items = data.currentItems || this.buyList.items;
        this.buyList.pendingActions = [];
        
        // Add confirmation to chat
        if (accept) {
          this.buyList.messages.push({
            role: "assistant",
            content: this.language === "Swedish" ? "✓ Ändringar tillagda!" : "✓ Changes applied!",
            timestamp: new Date().toISOString()
          });
        } else {
          this.buyList.messages.push({
            role: "assistant",
            content: this.language === "Swedish" ? "Okej, jag struntade i det." : "Okay, I ignored that.",
            timestamp: new Date().toISOString()
          });
        }
      } catch (e) {
        console.error("Confirm actions failed", e);
      }
    },

    async applyActions(actions) {
      // Silently apply actions without user confirmation (for simple adds)
      try {
        const res = await fetch(`/api/shopping-list/${this.buyList.id}/confirm`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            accept: true,
            actions: actions
          }),
        });
        
        if (res.ok) {
          const data = await res.json();
          this.buyList.items = data.currentItems || this.buyList.items;
        }
      } catch (e) {
        console.error("Apply actions failed", e);
      }
    },

    async saveList(autoName) {
      if (!this.buyList.id || !this.buyList.items.length) return;
      
      const name = autoName 
        ? null 
        : (this.buyList.customName || null);
      
      try {
        const res = await fetch(`/api/shopping-list/${this.buyList.id}/save`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            autoName: autoName,
            customName: name
          }),
        });
        
        if (!res.ok) throw new Error("Save failed");
        const data = await res.json();
        
        this.buyList.savedName = data.name;
        this.buyList.status = "saved";
        this.buyList.sessionMode = "reviewing";
        
        this.addToast(
          this.language === "Swedish" 
            ? `✓ Listan sparad som "${data.name}"`
            : `✓ List saved as "${data.name}"`,
          "success"
        );
      } catch (e) {
        console.error("Save failed", e);
        this.addToast("Failed to save list", "error");
      }
    },

    async removeItem(itemId) {
      // Manual removal from list
      const item = this.buyList.items.find(i => i.id === itemId);
      if (!item) return;
      
      try {
        const res = await fetch(`/api/shopping-list/${this.buyList.id}/items/${itemId}`, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ isDismissed: true }),
        });
        
        if (res.ok) {
          item.isDismissed = true;
        }
      } catch (e) {
        console.error("Remove failed", e);
      }
    },

    copyList() {
      const items = this.buyList.items.filter(i => !i.isDismissed);
      const text = items.map(i => `- ${i.itemName} x${i.quantity}`).join("\n");
      navigator.clipboard.writeText(text);
      this.addToast(this.t("shop.copied"), "success");
    },

    async dismissItem(itemId) {
      const item = this.buyList.items.find(i => i.id === itemId);
      if (!item) return;
      
      item.isDismissed = true;
      item.isChecked = false;
      
      try {
        const res = await fetch(`/api/shopping-list/${this.buyList.id}/items/${itemId}`, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ isDismissed: true, isChecked: false }),
        });
        if (!res.ok) throw new Error("Dismiss failed");
      } catch (e) {
        console.error("Dismiss failed", e);
        item.isDismissed = false;
      }
    },

    async restoreItem(itemId) {
      const item = this.buyList.items.find(i => i.id === itemId);
      if (!item) return;
      
      item.isDismissed = false;
      item.isChecked = true;
      
      try {
        const res = await fetch(`/api/shopping-list/${this.buyList.id}/items/${itemId}`, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ isDismissed: false, isChecked: true }),
        });
        if (!res.ok) throw new Error("Restore failed");
      } catch (e) {
        console.error("Restore failed", e);
        item.isDismissed = true;
        item.isChecked = false;
      }
    },

    async addCustomItem() {
      if (!this.buyList.newCustomName.trim()) return;
      
      try {
        const res = await fetch(`/api/shopping-list/${this.buyList.id}/items`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            itemName: this.buyList.newCustomName.trim(),
            quantity: this.buyList.newCustomQty,
          }),
        });
        
        if (!res.ok) throw new Error("Add failed");
        const data = await res.json();
        
        this.buyList.items.push(data);
        this.buyList.newCustomName = "";
        this.buyList.newCustomQty = 1;
        this.buyList.showAddCustom = false;
        this.addToast(this.t("shop.itemAdded"), "success");
      } catch (e) {
        console.error("Add custom failed", e);
      }
    },

    async commitList() {
      try {
        const res = await fetch(`/api/shopping-list/${this.buyList.id}/commit`, {
          method: "POST",
        });
        if (!res.ok) throw new Error("Commit failed");
        
        this.buyList.status = "active";
        this.addToast(this.t("shop.listCommitted"), "success");
      } catch (e) {
        console.error("Commit failed", e);
      }
    },

    async completeList() {
      try {
        const res = await fetch(`/api/shopping-list/${this.buyList.id}/complete`, {
          method: "POST",
        });
        if (!res.ok) throw new Error("Complete failed");
        
        this.addToast(this.t("shop.listCompleted"), "success");
        this.loadCurrentBuyList(); // Refresh (should show no list)
      } catch (e) {
        console.error("Complete failed", e);
      }
    },

    async loadHistory() {
      try {
        const res = await fetch("/api/shopping-list/history");
        if (!res.ok) throw new Error("Failed to load history");
        this.buyList.history = await res.json();
      } catch (e) {
        console.error("Load history failed", e);
      }
    },

    get dismissedItems() {
      return this.buyList.items.filter(i => i.isDismissed);
    },

    get dismissedCount() {
      return this.buyList.items.filter(i => i.isDismissed).length;
    },

    get activeItems() {
      return this.buyList.items.filter(i => !i.isDismissed);
    },

    get needNowItems() {
      return this.activeItems.filter(i => 
        (i.aiOriginalReasoning || "").includes("[Need Now]") ||
        (i.aiOriginalReasoning || "").includes("[Behövs Nu]")
      );
    },

    get soonItems() {
      return this.activeItems.filter(i => 
        (i.aiOriginalReasoning || "").includes("[Soon]") ||
        (i.aiOriginalReasoning || "").includes("[Snart]")
      );
    },

    get maybeItems() {
      return this.activeItems.filter(i => 
        (i.aiOriginalReasoning || "").includes("[Maybe]") ||
        (i.aiOriginalReasoning || "").includes("[Kanske]") ||
        (!i.aiOriginalReasoning)
      );
    },

    // ========== End Buy List Methods ==========

    handleFile(e) {
      this.selectedFile = e.target.files[0];
      this.scanResults = [];
    },

    async uploadReceipt() {
      if (!this.selectedFile) return;
      this.scanning = true;

      const formData = new FormData();
      formData.append("receiptImage", this.selectedFile);

      try {
        const res = await fetch("/api/receipts/scan", {
          method: "POST",
          body: formData,
        });
        if (!res.ok) throw new Error("Scan failed");

        this.scanResults = await res.json();
        this.addToast(this.t("toast.scan.success", this.scanResults.length));
        this.selectedFile = null;
        this.refreshInventory();
      } catch (err) {
        this.addToast(this.t("toast.err.scan"), "error");
      } finally {
        this.scanning = false;
      }
    },

    async fetchSuggestions() {
      // Legacy method - redirects to new buy list system
      this.view = "shopping";
      await this.loadCurrentBuyList();
    },

    formatQty(qty) {
      return Number.isInteger(qty) ? qty : qty.toFixed(1);
    },

    formatDate(dateStr) {
      if (!dateStr) return "Never";
      const date = new Date(dateStr);
      return date.toLocaleDateString(this.getLocale(), {
        month: "short",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit"
      });
    },

    formatCurrency(val) {
      return new Intl.NumberFormat(this.getLocale(), {
        style: "currency",
        currency: this.getCurrency(),
      }).format(val);
    },

    addToast(message, type = "info") {
      const id = Date.now();
      this.toasts.push({ id, message, type });
      setTimeout(() => this.removeToast(id), 4000);
    },

    removeToast(id) {
      this.toasts = this.toasts.filter((t) => t.id !== id);
    },

    // ========== Saved Lists Management ==========

    switchShopSubtab(tab) {
      this.shopSubtab = tab;
      if (tab === 'view') {
        this.loadSavedLists();
      }
    },

    async loadSavedLists() {
      try {
        const res = await fetch("/api/shopping-list/saved/all");
        if (!res.ok) throw new Error("Failed to load saved lists");
        this.savedLists = await res.json();
      } catch (e) {
        console.error("Failed to load saved lists", e);
        this.savedLists = [];
      }
    },

    async useSavedList(listId) {
      const list = this.savedLists.find(l => l.id === listId);
      if (!list) return;

      // Copy to clipboard
      const text = list.items.map(i => `- ${i.itemName} x${i.quantity}`).join("\n");
      navigator.clipboard.writeText(text);
      
      this.addToast(
        this.language === 'Swedish' 
          ? `✓ "${list.name}" kopierad till urklipp`
          : `✓ "${list.name}" copied to clipboard`,
        "success"
      );
    },

    async editSavedList(listId) {
      // Load the saved list into create mode
      const list = this.savedLists.find(l => l.id === listId);
      if (!list) return;

      // Switch to create tab
      this.shopSubtab = 'create';
      
      // Reset current buyList and populate with saved list data
      this.buyList.id = listId;
      this.buyList.savedName = list.name;
      this.buyList.status = 'saved';
      this.buyList.items = list.items.map(i => ({
        id: i.id || Date.now() + Math.random(),
        itemName: i.itemName,
        quantity: i.quantity,
        isChecked: true,
        isDismissed: false
      }));
      
      // Add a message showing we loaded the list
      this.buyList.messages = [{
        role: 'assistant',
        content: this.language === 'Swedish'
          ? `Jag har laddat "${list.name}". Du kan nu redigera den eller lägga till fler varor.`
          : `I've loaded "${list.name}". You can now edit it or add more items.`,
        timestamp: new Date().toISOString()
      }];
    },

    async deleteSavedList(listId) {
      const list = this.savedLists.find(l => l.id === listId);
      if (!list) return;

      // Confirm deletion
      const confirmed = confirm(
        this.language === 'Swedish'
          ? `Är du säker på att du vill ta bort "${list.name}"?`
          : `Are you sure you want to delete "${list.name}"?`
      );

      if (!confirmed) return;

      try {
        const res = await fetch(`/api/shopping-list/${listId}`, {
          method: "DELETE"
        });
        
        if (!res.ok) throw new Error("Delete failed");
        
        // Remove from local array
        this.savedLists = this.savedLists.filter(l => l.id !== listId);
        
        this.addToast(
          this.language === 'Swedish'
            ? `✓ "${list.name}" borttagen`
            : `✓ "${list.name}" deleted`,
          "success"
        );
      } catch (e) {
        console.error("Delete failed", e);
        this.addToast(
          this.language === 'Swedish'
            ? "Kunde inte ta bort listan"
            : "Could not delete list",
          "error"
        );
      }
    },
  };
}
