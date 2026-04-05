/**
 * HomeStoq Scan Feature
 * Receipt scanning view logic - Alpine.js factory function
 */

function createScanFeature() {
    return {
        // State
        selectedFile: null,
        scanning: false,
        scanResults: [],
        
        // Handle file selection
        handleFile(e) {
            this.selectedFile = e.target.files[0];
            this.scanResults = [];
        },
        
        // Upload and scan receipt
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
                if (this.addToast) {
                    this.addToast(this.t("toast.scan.success", this.scanResults.length), "success");
                }
                this.selectedFile = null;
                if (this.refreshInventory) {
                    this.refreshInventory();
                }
            } catch (err) {
                if (this.addToast) {
                    this.addToast(this.t("toast.err.scan"), "error");
                }
            } finally {
                this.scanning = false;
            }
        }
    };
}

// Export for main app
window.createScanFeature = createScanFeature;
