const i18n = {
  English: {
    "nav.stock": "Stock",
    "nav.scan": "Scan",
    "nav.list": "List",
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
    "list.title": "Predictive Suggestions",
    "list.desc": "Consumption-based logic trained on your 6-month history.",
    "list.btn": "Generate List",
    "list.btn.active": "Analyzing history...",
    "list.empty": "No suggestions yet. Scan more receipts to train the AI.",
    "list.qty": "Qty:",
    "prompt.add": "Enter item name:",
    "toast.err.stock": "Error loading stock",
    "toast.err.update": "Failed to update quantity",
    "toast.err.scan": "AI analysis failed",
    "toast.err.list": "Could not generate list",
    "toast.scan.success": "Successfully scanned {0} items",
    "category.other": "Other",
  },
  Swedish: {
    "nav.stock": "Skafferi",
    "nav.scan": "Skanna",
    "nav.list": "Inköpslista",
    "pulse.total": "Totalt antal",
    "pulse.low": "Lågt saldo",
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
    "list.title": "Smarta förslag",
    "list.desc":
      "Förslag baserade på din historik från de senaste 6 månaderna.",
    "list.btn": "Skapa lista",
    "list.btn.active": "Analyserar historik...",
    "list.empty": "Inga förslag ännu. Skanna fler kvitton för att träna AI:n.",
    "list.qty": "Antal:",
    "prompt.add": "Ange varans namn:",
    "toast.err.stock": "Kunde inte hämta skafferiet",
    "toast.err.update": "Kunde inte uppdatera antal",
    "toast.err.scan": "AI-analysen misslyckades",
    "toast.err.list": "Kunde inte skapa förslag",
    "toast.scan.success": "Skannade {0} varor",
    "category.other": "Övrigt",
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

    // Shopping State
    suggestions: [],
    loadingSuggestions: false,

    // UI State
    toasts: [],

    async init() {
      await this.fetchSettings();
      this.refreshInventory();

      // Watch view changes to refresh data
      this.$watch("view", (value) => {
        if (value === "inventory") this.refreshInventory();
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
      // Optimistic UI update
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

        // Refresh to get correct category/timestamp from server if new
        if (!item) this.refreshInventory();
      } catch (err) {
        if (item) item.quantity = oldQty;
        this.addToast(this.t("toast.err.update"), "error");
      }
    },

    async manualAdd() {
      const name = prompt(this.t("prompt.add"));
      if (!name) return;
      await this.updateQty(name.trim(), 1);
    },

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
      this.loadingSuggestions = true;
      try {
        const res = await fetch("/api/insights/shopping-list");
        if (!res.ok) throw new Error("Failed to fetch list");
        this.suggestions = await res.json();
      } catch (err) {
        this.addToast(this.t("toast.err.list"), "error");
      } finally {
        this.loadingSuggestions = false;
      }
    },

    // Helpers
    formatQty(qty) {
      return Number.isInteger(qty) ? qty : qty.toFixed(1);
    },

    formatDate(dateStr) {
      if (!dateStr) return "Never";
      const date = new Date(dateStr);
      const locale = this.language === "Swedish" ? "sv-SE" : "en-US";
      return date.toLocaleDateString(locale, {
        month: "short",
        day: "numeric",
      });
    },

    formatCurrency(val) {
      const locale = this.language === "Swedish" ? "sv-SE" : "en-US";
      const currency = this.language === "Swedish" ? "SEK" : "USD"; // Assuming USD fallback
      return new Intl.NumberFormat(locale, {
        style: "currency",
        currency: currency,
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
  };
}
