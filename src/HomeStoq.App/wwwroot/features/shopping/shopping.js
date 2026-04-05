/**
 * HomeStoq Shopping Feature
 * Inköpslistor/Shopping Lists - Alpine.js factory function
 */

function createShoppingFeature() {
    return {
        // State
        shopSubtab: 'create',
        savedLists: [],
        buyList: {
            id: null,
            status: null,
            savedName: "",
            messages: [],
            items: [],
            pendingActions: [],
            suggestedReplies: [],
            isLoading: false,
            sessionMode: 'brainstorming',
            chatInput: "",
            customName: "",
        },
        
        // Switch subtabs
        switchShopSubtab(tab) {
            this.shopSubtab = tab;
            if (tab === 'view') {
                this.loadSavedLists();
            }
        },
        
        // Reset buy list state
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
        
        // Start a new list with greeting
        async startNewList() {
            this.resetBuyList();
            
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
        
        // Load current buy list from API
        async loadCurrentBuyList() {
            try {
                const res = await fetch("/api/shopping-list/current");
                if (!res.ok) throw new Error("Failed to load buy list");
                const data = await res.json();
                
                if (data.hasList) {
                    this.buyList.id = data.id;
                    this.buyList.status = data.status;
                    this.buyList.items = data.items || [];
                    if (data.messages) {
                        this.buyList.messages = data.messages;
                    }
                } else {
                    this.resetBuyList();
                }
            } catch (e) {
                console.error("Failed to load buy list", e);
            }
        },
        
        // Send chat message to AI
        async sendChatMessage(message = null) {
            const msg = message || this.buyList.chatInput;
            if (!msg || !msg.trim() || this.buyList.isLoading) return;
            
            if (!message) {
                this.buyList.chatInput = "";
            }
            
            this.buyList.messages.push({
                role: "user",
                content: msg,
                timestamp: new Date().toISOString()
            });
            
            this.buyList.isLoading = true;
            this.buyList.suggestedReplies = [];
            
            try {
                if (!this.buyList.id) {
                    const createRes = await fetch("/api/shopping-list/generate", { method: "POST" });
                    if (!createRes.ok) throw new Error("Failed to create list");
                    const createData = await createRes.json();
                    this.buyList.id = createData.id;
                    this.buyList.items = createData.items || [];
                }
                
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
                
                this.buyList.messages.push({
                    role: "assistant",
                    content: data.reply,
                    timestamp: new Date().toISOString(),
                    actions: data.actions || [],
                    requiresConfirmation: data.requiresConfirmation
                });
                
                this.buyList.pendingActions = data.actions || [];
                this.buyList.suggestedReplies = data.suggestedReplies || [];
                this.buyList.items = data.currentItems || this.buyList.items;
                
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
            
            this.$nextTick(() => {
                const chatBox = this.$refs.chatThread;
                if (chatBox) chatBox.scrollTop = chatBox.scrollHeight;
            });
        },
        
        // Confirm or reject pending actions
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
        
        // Silently apply actions without confirmation
        async applyActions(actions) {
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
        
        // Save the current list
        async saveList(autoName) {
            if (!this.buyList.id || !this.buyList.items.length) return;
            
            const name = autoName ? null : (this.buyList.customName || null);
            
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
                
                if (this.addToast) {
                    this.addToast(
                        this.language === "Swedish" 
                            ? `✓ Listan sparad som "${data.name}"`
                            : `✓ List saved as "${data.name}"`,
                        "success"
                    );
                }
            } catch (e) {
                console.error("Save failed", e);
                if (this.addToast) {
                    this.addToast("Failed to save list", "error");
                }
            }
        },
        
        // Remove item from list
        async removeItem(itemId) {
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
        
        // Copy list to clipboard
        copyList() {
            const items = this.buyList.items.filter(i => !i.isDismissed);
            const text = items.map(i => `- ${i.itemName} x${i.quantity}`).join("\n");
            navigator.clipboard.writeText(text);
            if (this.addToast) {
                this.addToast(this.t("shop.copied"), "success");
            }
        },
        
        // Load all saved lists
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
        
        // Use a saved list (copy to clipboard)
        async useSavedList(listId) {
            const list = this.savedLists.find(l => l.id === listId);
            if (!list) return;

            const text = list.items.map(i => `- ${i.itemName} x${i.quantity}`).join("\n");
            navigator.clipboard.writeText(text);
            
            if (this.addToast) {
                this.addToast(
                    this.language === 'Swedish' 
                        ? `✓ "${list.name}" kopierad till urklipp`
                        : `✓ "${list.name}" copied to clipboard`,
                    "success"
                );
            }
        },
        
        // Edit a saved list (load into create mode)
        async editSavedList(listId) {
            const list = this.savedLists.find(l => l.id === listId);
            if (!list) return;

            this.shopSubtab = 'create';
            
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
            
            this.buyList.messages = [{
                role: 'assistant',
                content: this.language === 'Swedish'
                    ? `Jag har laddat "${list.name}". Du kan nu redigera den eller lägga till fler varor.`
                    : `I've loaded "${list.name}". You can now edit it or add more items.`,
                timestamp: new Date().toISOString()
            }];
        },
        
        // Delete a saved list
        async deleteSavedList(listId) {
            const list = this.savedLists.find(l => l.id === listId);
            if (!list) return;

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
                
                this.savedLists = this.savedLists.filter(l => l.id !== listId);
                
                if (this.addToast) {
                    this.addToast(
                        this.language === 'Swedish'
                            ? `✓ "${list.name}" borttagen`
                            : `✓ "${list.name}" deleted`,
                        "success"
                    );
                }
            } catch (e) {
                console.error("Delete failed", e);
                if (this.addToast) {
                    this.addToast(
                        this.language === 'Swedish'
                            ? "Kunde inte ta bort listan"
                            : "Could not delete list",
                        "error"
                    );
                }
            }
        }
    };
}

// Export for main app
window.createShoppingFeature = createShoppingFeature;
