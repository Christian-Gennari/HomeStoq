/**
 * HomeStoq Receipts Feature
 * Receipt history view logic - Alpine.js factory function
 */

function createReceiptsFeature() {
    return {
        // State
        receipts: [],
        receiptItems: [],
        expandedReceiptId: null,
        
        // Load all receipts
        async loadReceipts() {
            try {
                const res = await fetch("/api/receipts");
                if (!res.ok) throw new Error("Failed to load receipts");
                this.receipts = await res.json();
            } catch (e) {
                console.error("Failed to load receipts", e);
                if (this.addToast) {
                    this.addToast("Failed to load receipts", "error");
                }
            }
        },
        
        // Toggle receipt expansion
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
        }
    };
}

// Export for main app
window.createReceiptsFeature = createReceiptsFeature;
