/**
 * HomeStoq Shopping Feature - Clean Separation
 * 
 * Three distinct views:
 * 1. Ny lista (Compose) - Build list with AI help
 * 2. Sparade (Saved) - Browse saved lists + Archive
 * 3. Shopping Mode - Read-only checking (tap saved list to enter)
 * 4. Edit Mode - Manual editing (no AI)
 */

function createShoppingFeature() {
    return {
        // === STATE ===
        shopSubtab: 'create', // 'create' or 'saved'
        savedLists: [],
        archivedLists: [],
        
        // Current list being composed (Ny lista)
        composeList: {
            id: null,
            items: [],
            messages: [],
            chatInput: "",
            isLoading: false,
            savedName: "",
        },
        
        // Shopping mode state (read-only view)
        shoppingMode: false,
        activeShoppingList: null, // The list being shopped
        
        // Edit mode state
        editMode: false,
        editingList: null,
        editingItems: [],
        
        // Expand-in-place saved list
        expandedSavedListId: null,
        
        // Categories
        categories: ['Produce', 'Dairy', 'Meat & Fish', 'Bakery', 'Pantry', 'Frozen', 'Household', 'Other'],
        categoryEmojis: {
            'Produce': '🥬', 'Dairy': '🥛', 'Meat & Fish': '🥩', 'Bakery': '🍞',
            'Pantry': '🥫', 'Frozen': '🧊', 'Household': '🧽', 'Other': '📦'
        },
        swedishCategories: {
            'Produce': 'Frukt & Grönt', 'Dairy': 'Mejeri', 'Meat & Fish': 'Kött & Fisk',
            'Bakery': 'Bageri', 'Pantry': 'Skafferi', 'Frozen': 'Fryst',
            'Household': 'Hushåll', 'Other': 'Övrigt'
        },
        
        // === LIFECYCLE ===
        
        async initShoppingFeature() {
            const draft = localStorage.getItem('homestoq_compose_draft');
            if (draft) {
                try {
                    const parsed = JSON.parse(draft);
                    this.composeList.id = parsed.id || null;
                    this.composeList.items = parsed.items || [];
                    this.composeList.messages = parsed.messages || [];
                    this.categorizeComposeItems();
                } catch (e) {
                    localStorage.removeItem('homestoq_compose_draft');
                }
            } else {
                // No draft - add initial greeting
                const greeting = this.language === "Swedish" 
                    ? "Hej! Säg vad du behöver köpa, så hjälper jag dig."
                    : "Hi! Tell me what you need to buy, and I'll help you.";
                
                this.composeList.messages.push({
                    role: "assistant",
                    content: greeting,
                    timestamp: new Date().toISOString()
                });
            }
            
            await this.loadSavedLists();
            await this.loadArchivedLists();
        },
        
        // === DRAFT MANAGEMENT ===
        
        saveComposeDraft() {
            const draft = {
                id: this.composeList.id,
                items: this.composeList.items,
                messages: this.composeList.messages.slice(-5),
            };
            localStorage.setItem('homestoq_compose_draft', JSON.stringify(draft));
        },
        
        clearComposeDraft() {
            localStorage.removeItem('homestoq_compose_draft');
        },
        
        // === NY LISTA (COMPOSE) ===
        
        resetComposeList() {
            this.composeList.id = null;
            this.composeList.items = [];
            this.composeList.messages = [];
            this.composeList.chatInput = "";
            this.composeList.savedName = "";
            this.clearComposeDraft();
        },
        
        clearAndGreet() {
            this.resetComposeList();
            
            const greeting = this.language === "Swedish" 
                ? "Hej! Säg vad du behöver köpa, så hjälper jag dig."
                : "Hi! Tell me what you need to buy, and I'll help you.";
            
            this.composeList.messages.push({
                role: "assistant",
                content: greeting,
                timestamp: new Date().toISOString()
            });
        },
        
        startNewList() {
            // If has unsaved items, warn
            if (this.composeList.items.length > 0) {
                if (!confirm(this.language === 'Swedish' 
                    ? 'Du har osparade varor. Rensa och börja om?'
                    : 'You have unsaved items. Clear and start over?')) {
                    return;
                }
            }
            
            this.resetComposeList();
            
            const greeting = this.language === "Swedish" 
                ? "Hej! Säg vad du behöver köpa, så hjälper jag dig."
                : "Hi! Tell me what you need to buy, and I'll help you.";
            
            this.composeList.messages.push({
                role: "assistant",
                content: greeting,
                timestamp: new Date().toISOString()
            });
        },
        
        async sendChatMessage(message = null) {
            const msg = message || this.composeList.chatInput;
            if (!msg || !msg.trim() || this.composeList.isLoading) return;
            
            if (!message) this.composeList.chatInput = "";
            
            this.composeList.messages.push({
                role: "user",
                content: msg,
                timestamp: new Date().toISOString()
            });
            
            this.composeList.isLoading = true;
            
            try {
                if (!this.composeList.id) {
                    const createRes = await fetch("/api/shopping-list/create", { method: "POST" });
                    if (!createRes.ok) throw new Error("Failed to create list");
                    const createData = await createRes.json();
                    this.composeList.id = createData.id;
                    this.composeList.items = [];
                }
                
                const res = await fetch(`/api/shopping-list/${this.composeList.id}/chat`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ message: msg, language: this.language }),
                });
                
                if (!res.ok) throw new Error("Chat failed");
                const data = await res.json();
                
                // Auto-accept actions immediately — items are written to DB via /confirm
                if (data.actions && data.actions.length > 0) {
                    await this.applyComposeActions(data.actions);
                    // composeList.items is now set by applyComposeActions() — don't overwrite below
                } else {
                    // No actions (info reply) — use the snapshot from /chat
                    this.composeList.items = data.currentItems || this.composeList.items;
                }
                
                this.composeList.messages.push({
                    role: "assistant",
                    content: data.reply,
                    timestamp: new Date().toISOString()
                });
                this.categorizeComposeItems();
                this.saveComposeDraft();
                
            } catch (e) {
                console.error("Chat failed", e);
                this.composeList.messages.push({
                    role: "assistant",
                    content: this.language === "Swedish" 
                        ? "Något gick fel. Försök igen."
                        : "Something went wrong. Please try again.",
                    timestamp: new Date().toISOString()
                });
            } finally {
                this.composeList.isLoading = false;
            }
            
            this.$nextTick(() => {
                const chatBox = this.$refs.chatThread;
                if (chatBox) chatBox.scrollTop = chatBox.scrollHeight;
            });
        },
        
        async applyComposeActions(actions) {
            try {
                const res = await fetch(`/api/shopping-list/${this.composeList.id}/confirm`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ accept: true, actions: actions }),
                });
                
                if (res.ok) {
                    const data = await res.json();
                    this.composeList.items = data.currentItems || this.composeList.items;
                }
            } catch (e) {
                console.error("Apply actions failed", e);
            }
        },
        
        async undoLastAdd() {
            // Remove last added items (simplified - just reload)
            await this.loadCurrentComposeList();
            this.showUndoToast = false;
        },
        
        async loadCurrentComposeList() {
            try {
                const res = await fetch("/api/shopping-list/current");
                if (!res.ok) throw new Error("Failed to load");
                const data = await res.json();
                
                if (data.hasList) {
                    this.composeList.id = data.id;
                    this.composeList.items = data.items || [];
                    this.categorizeComposeItems();
                }
            } catch (e) {
                console.error("Failed to load compose list", e);
            }
        },
        
        categorizeComposeItems() {
            const keywords = {
                'Produce': ['apple', 'banana', 'grape', 'tomato', 'potato', 'grönsak', 'frukt', 'äpple', 'banan', 'apelsin', 'tomat', 'potatis', 'lök', 'morot', 'sallad', 'avokado', 'citron'],
                'Dairy': ['milk', 'cheese', 'yogurt', 'butter', 'egg', 'mjölk', 'ost', 'smör', 'ägg', 'grädde'],
                'Meat & Fish': ['chicken', 'beef', 'fish', 'kyckling', 'nötkött', 'fläsk', 'fisk', 'lax', 'korv'],
                'Bakery': ['bread', 'bröd', 'fralla', 'baguette', 'bulle'],
                'Pantry': ['pasta', 'rice', 'pasta', 'ris', 'mjöl', 'socker', 'salt', 'krydda'],
                'Frozen': ['frozen', 'fryst', 'glass', 'pizza'],
                'Household': ['soap', 'paper', 'tvål', 'papper', 'rengöring']
            };
            
            this.composeList.items.forEach(item => {
                if (item.category) return;
                const name = item.itemName.toLowerCase();
                for (const [category, words] of Object.entries(keywords)) {
                    if (words.some(word => name.includes(word))) {
                        item.category = category;
                        break;
                    }
                }
                if (!item.category) item.category = 'Other';
            });
        },
        
        getComposeItemsByCategory(category) {
            return this.composeList.items.filter(i => !i.isDismissed && i.category === category);
        },
        
        getCategoryDisplayName(category) {
            return this.language === 'Swedish' 
                ? (this.swedishCategories[category] || category)
                : category;
        },
        
        async toggleComposeItemCheck(itemId) {
            const item = this.composeList.items.find(i => i.id === itemId);
            if (!item) return;
            item.isChecked = !item.isChecked;
            this.saveComposeDraft();
        },
        
        async changeComposeQuantity(itemId, delta) {
            const item = this.composeList.items.find(i => i.id === itemId);
            if (!item) return;
            item.quantity = Math.max(1, item.quantity + delta);
            this.saveComposeDraft();
        },
        
        async removeComposeItem(itemId) {
            const item = this.composeList.items.find(i => i.id === itemId);
            if (item) item.isDismissed = true;
            this.saveComposeDraft();
        },
        
        async saveComposeList() {
            if (!this.composeList.id) {
                if (this.addToast) {
                    this.addToast(
                        this.language === 'Swedish' ? 'Ingen lista att spara' : 'No list to save',
                        "error"
                    );
                }
                return;
            }

            try {
                const res = await fetch(`/api/shopping-list/${this.composeList.id}`);
                if (res.ok) {
                    const data = await res.json();
                    this.composeList.items = data.items || [];
                }
            } catch (e) {
                console.error("Failed to fetch fresh items before save", e);
            }

            const activeItems = this.composeList.items.filter(i => !i.isDismissed);
            if (activeItems.length === 0) {
                if (this.addToast) {
                    this.addToast(
                        this.language === 'Swedish' ? 'Listan är tom, inget att spara' : 'List is empty, nothing to save',
                        "error"
                    );
                }
                return;
            }
            
            const name = this.composeList.savedName?.trim() || null;
            
            try {
                const res = await fetch(`/api/shopping-list/${this.composeList.id}/save`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ autoName: !name, customName: name }),
                });
                
                if (!res.ok) {
                    const errorText = await res.text();
                    throw new Error(`Save failed: ${res.status} - ${errorText}`);
                }
                
                this.clearComposeDraft();
                this.resetComposeList();
                await this.loadSavedLists();
                
                if (this.addToast) {
                    this.addToast(
                        this.language === 'Swedish' ? '✓ Listan sparad' : '✓ List saved',
                        "success"
                    );
                }
                
                // Switch to saved view
                this.shopSubtab = 'saved';
                
            } catch (e) {
                console.error("Save failed", e);
                if (this.addToast) {
                    this.addToast(
                        this.language === 'Swedish' ? 'Kunde inte spara listan' : 'Failed to save list',
                        "error"
                    );
                }
            }
        },
        
        copyComposeList() {
            const items = this.composeList.items.filter(i => !i.isDismissed);
            const text = items.map(i => `- ${i.itemName} x${i.quantity}`).join("\n");
            navigator.clipboard.writeText(text);
            if (this.addToast) {
                this.addToast(this.language === 'Swedish' ? '📋 Kopierad' : '📋 Copied', "success");
            }
        },
        
        // === SPARADE (SAVED LISTS) ===
        
        async loadSavedLists() {
            try {
                const res = await fetch("/api/shopping-list/saved/all");
                if (!res.ok) throw new Error("Failed to load");
                const lists = await res.json();
                // Filter out completed/cancelled lists as safety measure (backend should already exclude them)
                this.savedLists = lists.filter(l => l.status !== 'completed' && l.status !== 'cancelled');
            } catch (e) {
                console.error("Failed to load saved lists", e);
            }
        },
        
        async loadArchivedLists() {
            // For now, archive is just saved lists marked as archived
            // In practice, this will be lists that were "completed"
            try {
                const res = await fetch("/api/shopping-list/history");
                if (!res.ok) throw new Error("Failed to load");
                const lists = await res.json();
                this.archivedLists = lists.filter(l => l.status === 'completed' || l.status === 'archived');
            } catch (e) {
                this.archivedLists = [];
            }
        },
        
        // === SHOPPING MODE (Read-only + Check) ===
        
        enterShoppingMode(list) {
            this.activeShoppingList = {
                ...list,
                items: list.items.map(i => ({
                    ...i,
                    isChecked: false // Reset checks for new shopping trip
                }))
            };
            this.shoppingMode = true;
        },
        
        exitShoppingMode() {
            this.shoppingMode = false;
            this.activeShoppingList = null;
        },
        
        toggleShoppingCheck(itemIndex) {
            if (!this.activeShoppingList) return;
            const item = this.activeShoppingList.items[itemIndex];
            item.isChecked = !item.isChecked;
        },
        
        async completeShoppingTrip() {
            if (!this.activeShoppingList) return;
            
            // Archive the list (mark as completed)
            try {
                await fetch(`/api/shopping-list/${this.activeShoppingList.id}/complete`, { method: "POST" });
            } catch (e) {
                console.error("Failed to complete list", e);
            }
            
            this.exitShoppingMode();
            await this.loadSavedLists();
            await this.loadArchivedLists();
            
            if (this.addToast) {
                this.addToast(
                    this.language === 'Swedish' ? '✓ Handlat klart!' : '✓ Shopping done!',
                    "success"
                );
            }
        },
        
        resetShoppingChecks() {
            if (!this.activeShoppingList) return;
            this.activeShoppingList.items.forEach(i => i.isChecked = false);
        },
        
        // === EDIT MODE (Manual, no AI) ===
        
        enterEditMode(list) {
            this.editingList = list;
            this.editingItems = list.items.map(i => ({
                name: i.itemName,
                quantity: i.quantity,
                id: i.id || Date.now() + Math.random()
            }));
            this.editMode = true;
            this.shoppingMode = false; // Exit shopping if in it
        },
        
        exitEditMode() {
            this.editMode = false;
            this.editingList = null;
            this.editingItems = [];
        },
        
        addEditItem() {
            this.editingItems.push({
                name: '',
                quantity: 1,
                id: Date.now() + Math.random()
            });
            
            // Focus the new input
            this.$nextTick(() => {
                const inputs = document.querySelectorAll('.edit-item-input');
                if (inputs.length > 0) {
                    inputs[inputs.length - 1].focus();
                }
            });
        },
        
        removeEditItem(index) {
            this.editingItems.splice(index, 1);
        },
        
        async saveEditChanges() {
            if (!this.editingList) return;
            
            // Filter out empty items
            const validItems = this.editingItems.filter(i => i.name.trim());
            
            if (validItems.length === 0) {
                if (this.addToast) {
                    this.addToast(
                        this.language === 'Swedish' ? 'Listan är tom' : 'List is empty',
                        "error"
                    );
                }
                return;
            }
            
            try {
                // Clear existing items and add new ones
                await fetch(`/api/shopping-list/${this.editingList.id}/items/clear`, { method: "POST" });
                
                for (const item of validItems) {
                    await fetch(`/api/shopping-list/${this.editingList.id}/items`, {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ itemName: item.name, quantity: item.quantity }),
                    });
                }
                
                this.exitEditMode();
                await this.loadSavedLists();
                
                if (this.addToast) {
                    this.addToast(
                        this.language === 'Swedish' ? '✓ Ändringar sparade' : '✓ Changes saved',
                        "success"
                    );
                }
                
            } catch (e) {
                console.error("Save edit failed", e);
            }
        },
        
        // === SHARED ===
        
        async deleteSavedList(listId, isArchived = false) {
            const list = isArchived 
                ? this.archivedLists.find(l => l.id === listId)
                : this.savedLists.find(l => l.id === listId);
            if (!list) return;
            
            if (!confirm(this.language === 'Swedish' 
                ? `Ta bort "${list.name}"?` 
                : `Delete "${list.name}"?`)) return;
            
            try {
                const res = await fetch(`/api/shopping-list/${listId}`, { method: "DELETE" });
                if (!res.ok) throw new Error("Delete failed");
                
                if (isArchived) {
                    this.archivedLists = this.archivedLists.filter(l => l.id !== listId);
                } else {
                    this.savedLists = this.savedLists.filter(l => l.id !== listId);
                }
                
                if (this.addToast) {
                    this.addToast(
                        this.language === 'Swedish' ? '✓ Borttagen' : '✓ Deleted',
                        "success"
                    );
                }
            } catch (e) {
                console.error("Delete failed", e);
            }
        },
        
        getShoppingItemsByCategory(category) {
            if (!this.activeShoppingList) return [];
            
            const keywords = {
                'Produce': ['apple', 'banana', 'grape', 'tomato', 'potato', 'grönsak', 'frukt', 'äpple', 'banan', 'apelsin', 'tomat', 'potatis', 'lök', 'morot', 'sallad', 'avokado', 'citron'],
                'Dairy': ['milk', 'cheese', 'yogurt', 'butter', 'egg', 'mjölk', 'ost', 'smör', 'ägg', 'grädde'],
                'Meat & Fish': ['chicken', 'beef', 'fish', 'kyckling', 'nötkött', 'fläsk', 'fisk', 'lax', 'korv'],
                'Bakery': ['bread', 'bröd', 'fralla', 'baguette', 'bulle'],
                'Pantry': ['pasta', 'rice', 'pasta', 'ris', 'mjöl', 'socker', 'salt', 'krydda'],
                'Frozen': ['frozen', 'fryst', 'glass', 'pizza'],
                'Household': ['soap', 'paper', 'tvål', 'papper', 'rengöring']
            };
            
            return this.activeShoppingList.items.map((item, originalIndex) => {
                let itemCategory = item.category;
                if (!itemCategory) {
                    const name = item.itemName.toLowerCase();
                    for (const [cat, words] of Object.entries(keywords)) {
                        if (words.some(word => name.includes(word))) {
                            itemCategory = cat;
                            break;
                        }
                    }
                    if (!itemCategory) itemCategory = 'Other';
                }
                return { ...item, originalIndex, category: itemCategory };
            }).filter(item => item.category === category);
        },
        
        switchShopSubtab(tab) {
            this.shopSubtab = tab;
            this.expandedSavedListId = null; // Collapse any expanded cards
            if (tab === 'saved') {
                this.loadSavedLists();
                this.loadArchivedLists();
            }
        }
    };
}

// Export
window.createShoppingFeature = createShoppingFeature;
