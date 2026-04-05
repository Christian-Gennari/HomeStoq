/**
 * HomeStoq Inventory Feature
 * Stock/Skafferi view logic - Alpine.js factory function
 */

function createInventoryFeature() {
    return {
        // State
        items: [],
        search: "",
        loading: false,
        
        // Modal state (for adding items)
        showAddModal: false,
        newItemName: "",
        
        // Check stock level for styling
        isLowStock(item) {
            if (item.quantity == 0) return "low-stock";
            if (item.quantity == 1) return "low-stock-warning";
            return "";
        },
        
        // Fetch inventory from API
        async refreshInventory() {
            this.loading = true;
            try {
                const res = await fetch("/api/inventory");
                if (!res.ok) throw new Error("Failed to load inventory");
                this.items = await res.json();
                console.log("Inventory loaded:", this.items.length, "items");
            } catch (err) {
                console.error("refreshInventory error:", err);
                if (this.addToast) {
                    this.addToast(this.t("toast.err.stock"), "error");
                }
            } finally {
                this.loading = false;
            }
        },
        
        // Update item quantity (+1 or -1)
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
                if (this.addToast) {
                    this.addToast(this.t("toast.err.update"), "error");
                }
            }
        },
        
        // Open add item modal
        async manualAdd() {
            this.newItemName = "";
            this.showAddModal = true;
        },
        
        // Submit new item
        async submitAddItem() {
            if (!this.newItemName.trim()) return;
            await this.updateQty(this.newItemName.trim(), 1);
            this.showAddModal = false;
            this.newItemName = "";
        }
    };
}

// Export for main app
window.createInventoryFeature = createInventoryFeature;
