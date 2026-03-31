let inventoryData = [];
let filteredData = [];

function showView(viewId) {
    document.querySelectorAll('.view').forEach(v => {
        v.classList.remove('active');
    });
    document.querySelectorAll('.nav-btn').forEach(b => {
        b.classList.remove('active');
    });

    const view = document.getElementById(viewId + '-view');
    const btn = document.querySelector(`.nav-btn[data-view="${viewId}"]`);

    requestAnimationFrame(() => {
        view.classList.add('active');
        btn.classList.add('active');
    });

    if (viewId === 'inventory') refreshInventory();
}

function showToast(message, type = 'info') {
    const container = document.getElementById('toast-container');
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.textContent = message;
    container.appendChild(toast);

    setTimeout(() => {
        toast.classList.add('removing');
        toast.addEventListener('animationend', () => toast.remove());
    }, 3000);
}

function setLoading(viewId, loading) {
    const loadingEl = document.getElementById(`${viewId}-loading`);
    const emptyEl = document.getElementById(`${viewId}-empty`);
    const listEl = document.getElementById(`${viewId}-list`);

    if (loadingEl) loadingEl.classList.toggle('visible', loading);
    if (emptyEl) emptyEl.classList.remove('visible');
    if (listEl) listEl.style.display = loading ? 'none' : '';
}

function setEmpty(viewId, empty) {
    const emptyEl = document.getElementById(`${viewId}-empty`);
    const listEl = document.getElementById(`${viewId}-list`);

    if (emptyEl) emptyEl.classList.toggle('visible', empty);
    if (listEl) listEl.style.display = empty ? 'none' : '';
}

async function refreshInventory() {
    setLoading('inventory', true);

    try {
        const response = await fetch('/api/inventory');
        if (!response.ok) throw new Error('Failed to fetch inventory');

        inventoryData = await response.json();
        filteredData = [...inventoryData];
        renderInventory();
    } catch (err) {
        showToast('Failed to load inventory', 'error');
    } finally {
        setLoading('inventory', false);
    }
}

function renderInventory() {
    const list = document.getElementById('inventory-list');
    const countEl = document.getElementById('item-count');
    const emptyEl = document.getElementById('inventory-empty');

    countEl.textContent = `${filteredData.length} item${filteredData.length !== 1 ? 's' : ''}`;

    if (filteredData.length === 0) {
        list.innerHTML = '';
        setEmpty('inventory', true);
        return;
    }

    setEmpty('inventory', false);

    list.innerHTML = filteredData.map(item => `
        <li class="inventory-item" data-name="${item.itemName}">
            <span class="item-name">${escapeHtml(item.itemName)}</span>
            <div class="qty-controls">
                <button class="qty-btn remove" onclick="updateQty('${escapeAttr(item.itemName)}', -1)" aria-label="Decrease quantity">−</button>
                <span class="qty-value">${formatQty(item.quantity)}</span>
                <button class="qty-btn" onclick="updateQty('${escapeAttr(item.itemName)}', 1)" aria-label="Increase quantity">+</button>
            </div>
        </li>
    `).join('');
}

function filterInventory() {
    const query = document.getElementById('search-input').value.toLowerCase().trim();
    filteredData = query
        ? inventoryData.filter(item => item.itemName.toLowerCase().includes(query))
        : [...inventoryData];
    renderInventory();
}

async function updateQty(itemName, change) {
    const itemEl = document.querySelector(`.inventory-item[data-name="${CSS.escape(itemName)}"]`);
    const qtyEl = itemEl?.querySelector('.qty-value');

    const oldQty = inventoryData.find(i => i.itemName === itemName)?.quantity ?? 0;
    const newQty = Math.max(0, oldQty + change);

    if (itemEl) {
        itemEl.classList.add(change > 0 ? 'adding' : 'removing-qty');
        if (qtyEl) qtyEl.textContent = formatQty(newQty);
        setTimeout(() => {
            itemEl?.classList.remove('adding', 'removing-qty');
        }, 500);
    }

    try {
        const response = await fetch('/api/inventory/update', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                itemName,
                quantityChange: change,
                price: null,
                currency: null
            })
        });

        if (!response.ok) throw new Error('Update failed');

        const idx = inventoryData.findIndex(i => i.itemName === itemName);
        if (idx !== -1) {
            inventoryData[idx].quantity = newQty;
        } else if (change > 0) {
            inventoryData.push({ itemName, quantity: newQty });
        }

        filteredData = document.getElementById('search-input').value.toLowerCase().trim()
            ? inventoryData.filter(i => i.itemName.toLowerCase().includes(document.getElementById('search-input').value.toLowerCase().trim()))
            : [...inventoryData];

        renderInventory();
        showToast(`${change > 0 ? 'Added' : 'Removed'} ${itemName}`, 'success');
    } catch (err) {
        if (qtyEl) qtyEl.textContent = formatQty(oldQty);
        showToast('Failed to update inventory', 'error');
    }
}

async function manualAdd() {
    const input = document.getElementById('manual-item');
    const itemName = input.value.trim();
    if (!itemName) return;

    await updateQty(itemName, 1);
    input.value = '';
}

const cameraInput = document.getElementById('receipt-camera');
const fileInput = document.getElementById('receipt-file');
const scanBtn = document.getElementById('scan-btn');
const scanStatus = document.getElementById('scan-status');
const filePreview = document.getElementById('file-preview');
const scanProgress = document.getElementById('scan-progress');

function handleFileSelection(e) {
    const file = e.target.files[0];
    if (!file) return;

    if (e.target === cameraInput) fileInput.value = '';
    else cameraInput.value = '';

    scanBtn.disabled = false;

    const isImage = file.type.startsWith('image/');
    filePreview.innerHTML = isImage
        ? `<img src="${URL.createObjectURL(file)}" alt="Preview"><span class="file-name">${escapeHtml(file.name)}</span>`
        : `<span class="file-name">${escapeHtml(file.name)}</span>`;

    filePreview.innerHTML += `<button type="button" class="file-remove" onclick="clearFileSelection()">&times;</button>`;
    filePreview.classList.remove('hidden');
    filePreview.classList.add('visible');
}

function clearFileSelection() {
    cameraInput.value = '';
    fileInput.value = '';
    scanBtn.disabled = true;
    filePreview.innerHTML = '';
    filePreview.classList.add('hidden');
    filePreview.classList.remove('visible');
    scanStatus.innerHTML = '';
}

cameraInput.onchange = handleFileSelection;
fileInput.onchange = handleFileSelection;

document.getElementById('receipt-form').onsubmit = async (e) => {
    e.preventDefault();
    const file = cameraInput.files[0] || fileInput.files[0];
    if (!file) return;

    scanBtn.disabled = true;
    scanProgress.classList.remove('hidden');
    scanStatus.innerHTML = '';

    const formData = new FormData();
    formData.append('receiptImage', file);

    try {
        const response = await fetch('/api/receipts/scan', {
            method: 'POST',
            body: formData
        });

        if (!response.ok) throw new Error('Scan failed');

        const items = await response.json();

        scanProgress.classList.add('hidden');
        scanStatus.innerHTML = `
            <div class="result-list">
                ${items.map(i => `
                    <div class="result-item">
                        <span class="item-name">${escapeHtml(i.itemName)} &times; ${i.quantity}</span>
                        <span class="item-price">${i.price ? `${i.price.toFixed(2)}` : '—'}</span>
                    </div>
                `).join('')}
            </div>
        `;

        showToast(`Scanned ${items.length} item${items.length !== 1 ? 's' : ''}`, 'success');
        clearFileSelection();
    } catch (err) {
        scanProgress.classList.add('hidden');
        scanStatus.innerHTML = '<p style="color: var(--terracotta);">Failed to process receipt. Try again with a clearer image.</p>';
        scanBtn.disabled = false;
        showToast('Receipt scan failed', 'error');
    }
};

async function refreshShoppingList() {
    const btn = document.getElementById('analyze-btn');
    btn.disabled = true;
    setLoading('shopping', true);
    setEmpty('shopping', false);

    try {
        const response = await fetch('/api/insights/shopping-list');
        if (!response.ok) throw new Error('Failed to generate suggestions');

        const items = await response.json();

        setLoading('shopping', false);

        if (!items || items.length === 0) {
            setEmpty('shopping', true);
            showToast('No suggestions found', 'info');
            return;
        }

        const list = document.getElementById('shopping-list');
        list.innerHTML = items.map(item => `
            <li class="shopping-item">
                <div class="shopping-item-header">
                    <span class="shopping-item-name">${escapeHtml(item.ItemName)}</span>
                    <span class="shopping-item-qty">Qty: ${item.Quantity}</span>
                </div>
                <p class="shopping-item-reason">${escapeHtml(item.Reason)}</p>
            </li>
        `).join('');

        showToast(`Found ${items.length} suggestion${items.length !== 1 ? 's' : ''}`, 'success');
    } catch (err) {
        setLoading('shopping', false);
        setEmpty('shopping', true);
        showToast('Failed to generate suggestions', 'error');
    } finally {
        btn.disabled = false;
    }
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function escapeAttr(text) {
    return text.replace(/'/g, "\\'").replace(/"/g, '&quot;');
}

function formatQty(qty) {
    return Number.isInteger(qty) ? qty.toString() : qty.toFixed(1);
}

showView('inventory');
