/**
 * HomeStoq Chat Feature
 * Pantry chat slide-over logic - Alpine.js factory function
 */

function createChatFeature() {
    return {
        // State
        showChat: false,
        chatInput: "",
        chatHistory: [],
        chatLoading: false,
        
        // Toggle chat panel
        toggleChat() {
            this.showChat = !this.showChat;
            if (this.showChat && this.chatHistory.length === 0) {
                const initialMsg = this.language === "Swedish" 
                    ? "Hej! Jag är din skafferi-assistent. Hur kan jag hjälpa dig idag?"
                    : "Hello! I'm your pantry assistant. How can I help you today?";
                this.chatHistory.push({ id: Date.now(), role: "assistant", content: initialMsg });
            }
        },
        
        // Send message to pantry chat
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
                        if (this.refreshInventory) {
                            this.refreshInventory();
                        }
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
        }
    };
}

// Export for main app
window.createChatFeature = createChatFeature;
