/**
 * HomeStoq Main App
 * Orchestrator that combines all feature modules
 */

function pantryApp() {
    return {
        // Global State
        view: "inventory",
        language: "English",
        toasts: [],
        
        // Initialize feature modules
        ...createInventoryFeature(),
        ...createScanFeature(),
        ...createReceiptsFeature(),
        ...createShoppingFeature(),
        ...createChatFeature(),
        
        // Computed: Group items by category (must be in main object for Alpine reactivity)
        get categorizedItems() {
            const items = this.items || [];
            const search = this.search || "";
            
            const filtered = items.filter((i) =>
                i.itemName.toLowerCase().includes(search.toLowerCase()),
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
        
        // Lifecycle
        async init() {
            console.log("HomeStoq app initializing...");
            await this.fetchSettings();
            
            // Initialize inventory
            if (this.refreshInventory) {
                console.log("Refreshing inventory...");
                await this.refreshInventory();
                console.log("Inventory refreshed, items count:", this.items?.length);
            }

            // Watch view changes to refresh data
            this.$watch("view", (value) => {
                console.log("View changed to:", value);
                if (value === "inventory" && this.refreshInventory) this.refreshInventory();
                if (value === "receipts_history" && this.loadReceipts) this.loadReceipts();
                if (value === "shopping" && this.loadCurrentBuyList) this.loadCurrentBuyList();
            });
            
            console.log("HomeStoq app initialized");
        },
        
        // Settings
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
        
        // i18n helper
        t(key, ...args) {
            const dict = i18n[this.language] || i18n["English"];
            let text = dict[key] || key;
            args.forEach((arg, i) => {
                text = text.replace(`{${i}}`, arg);
            });
            return text;
        },
        
        // Locale helpers
        getLocale() {
            return this.language === "Swedish" ? "sv-SE" : "en-US";
        },
        
        getCurrency() {
            return this.language === "Swedish" ? "SEK" : "USD";
        },
        
        // Formatters
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
        
        // Toast system
        addToast(message, type = "info") {
            const id = Date.now();
            this.toasts.push({ id, message, type });
            setTimeout(() => this.removeToast(id), 4000);
        },
        
        removeToast(id) {
            this.toasts = this.toasts.filter((t) => t.id !== id);
        }
    };
}

// Export
window.pantryApp = pantryApp;
