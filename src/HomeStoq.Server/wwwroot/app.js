// Navigation
function showView(viewId) {
    document.querySelectorAll('.view').forEach(v => v.classList.add('hidden'));
    document.getElementById(viewId + '-view').classList.remove('hidden');
    if (viewId === 'inventory') refreshInventory();
}

// Inventory Logic
async function refreshInventory() {
    const response = await fetch('/api/inventory');
    const items = await response.json();
    const list = document.getElementById('inventory-list');
    list.innerHTML = items.map(item => `
        <li>
            <span>${item.itemName}</span>
            <div class="qty-controls">
                <button class="qty-btn remove" onclick="updateQty('${item.itemName}', -1)">-</button>
                <strong>${item.quantity}</strong>
                <button class="qty-btn" onclick="updateQty('${item.itemName}', 1)">+</button>
            </div>
        </li>
    `).join('');
}

async function updateQty(itemName, change) {
    await fetch('/api/inventory/update', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ itemName, quantityChange: change })
    });
    refreshInventory();
}

async function manualAdd() {
    const input = document.getElementById('manual-item');
    const itemName = input.value.trim();
    if (!itemName) return;
    await updateQty(itemName, 1);
    input.value = '';
}

// Receipt Scanning Logic
document.getElementById('receipt-form').onsubmit = async (e) => {
    e.preventDefault();
    const fileInput = document.getElementById('receipt-image');
    if (!fileInput.files[0]) return;

    const status = document.getElementById('scan-status');
    status.innerText = 'Analyzing receipt with Gemini...';

    const formData = new FormData();
    formData.append('receiptImage', fileInput.files[0]);

    try {
        const response = await fetch('/api/receipts/scan', {
            method: 'POST',
            body: formData
        });
        const items = await response.json();
        status.innerHTML = `<h3>Scanned Items:</h3><ul>${items.map(i => `<li>${i.itemName} (${i.quantity}) - ${i.price || '?'}</li>`).join('')}</ul>`;
        fileInput.value = '';
    } catch (err) {
        status.innerText = 'Error processing receipt.';
    }
};

// Smart Shopping List Logic
async function refreshShoppingList() {
    const list = document.getElementById('shopping-list');
    list.innerHTML = '<li>Analyzing your patterns...</li>';

    try {
        const response = await fetch('/api/insights/shopping-list');
        const items = await response.json();
        list.innerHTML = items.map(item => `
            <li>
                <div>
                    <strong>${item.ItemName}</strong>
                    <span class="shopping-reason">${item.Reason}</span>
                </div>
                <span>Qty: ${item.Quantity}</span>
            </li>
        `).join('');
    } catch (err) {
        list.innerHTML = '<li>Failed to generate suggestions.</li>';
    }
}

// Initial Load
showView('inventory');
