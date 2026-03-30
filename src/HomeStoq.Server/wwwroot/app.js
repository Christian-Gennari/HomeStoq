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
const cameraInput = document.getElementById('receipt-camera');
const fileInput = document.getElementById('receipt-file');
const scanBtn = document.getElementById('scan-btn');
const scanStatus = document.getElementById('scan-status');

function handleFileSelection(e) {
    const file = e.target.files[0];
    if (!file) return;

    // Clear the other input
    if (e.target === cameraInput) fileInput.value = '';
    else cameraInput.value = '';

    scanBtn.disabled = false;
    scanStatus.innerHTML = `<div class="selected-file-info">Selected: ${file.name}</div>`;
}

cameraInput.onchange = handleFileSelection;
fileInput.onchange = handleFileSelection;

document.getElementById('receipt-form').onsubmit = async (e) => {
    e.preventDefault();
    const file = cameraInput.files[0] || fileInput.files[0];
    if (!file) return;

    scanBtn.disabled = true;
    scanStatus.innerText = 'Analyzing receipt with Gemini...';

    const formData = new FormData();
    formData.append('receiptImage', file);

    try {
        const response = await fetch('/api/receipts/scan', {
            method: 'POST',
            body: formData
        });
        const items = await response.json();
        scanStatus.innerHTML = `<h3>Scanned Items:</h3><ul>${items.map(i => `<li>${i.itemName} (${i.quantity}) - ${i.price || '?'}</li>`).join('')}</ul>`;
        
        // Reset state
        cameraInput.value = '';
        fileInput.value = '';
        scanBtn.disabled = true;
    } catch (err) {
        scanStatus.innerText = 'Error processing receipt.';
        scanBtn.disabled = false;
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
