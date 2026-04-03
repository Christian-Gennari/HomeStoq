const fs = require('fs');
const path = require('path');

const stores = ['ICA Supermarket', 'Coop', 'Willys', 'City Gross', 'Hemköp', 'Lidl'];

const categories = {
    'Mejeri': [
        { name: 'Mjölk', exp: 'Mellanmjölk 1.5% 1L', price: 14.90 },
        { name: 'Smör', exp: 'Svenskt Smör Normalsaltat 500g', price: 62.90 },
        { name: 'Ost', exp: 'Herrgård Herrgårdost 28% 700g', price: 89.00 },
        { name: 'Yoghurt', exp: 'Naturell Yoghurt 3% 1L', price: 22.50 },
        { name: 'Ägg', exp: 'Ägg Frigående 12-pack', price: 34.90 },
        { name: 'Gräddfil', exp: 'Gräddfil 12% 3dl', price: 12.50 }
    ],
    'Frukt & Grönt': [
        { name: 'Äpple', exp: 'Svenska Äpplen Ingrid Marie', price: 29.90, isWeight: true },
        { name: 'Banan', exp: 'Ekologiska Bananer', price: 24.90, isWeight: true },
        { name: 'Tomat', exp: 'Kvisttomater', price: 39.90, isWeight: true },
        { name: 'Gurka', exp: 'Svensk Gurka', price: 15.00 },
        { name: 'Lök', exp: 'Gullök i nät 1kg', price: 14.90 },
        { name: 'Potatis', exp: 'Fast Potatis 2kg', price: 29.90 },
        { name: 'Morot', exp: 'Morötter i påse 1kg', price: 12.90 }
    ],
    'Skafferi': [
        { name: 'Kaffe', exp: 'Mellanrost Bryggkaffe 500g', price: 54.90 },
        { name: 'Pasta', exp: 'Spaghetti 1kg', price: 24.90 },
        { name: 'Ris', exp: 'Jasminris 1kg', price: 32.90 },
        { name: 'Havregryn', exp: 'Havregryn 750g', price: 16.90 },
        { name: 'Krossade tomater', exp: 'Krossade Tomater 400g', price: 11.90 },
        { name: 'Olivolja', exp: 'Extra Virgin Olivolja 500ml', price: 69.90 }
    ],
    'Kött & Fisk': [
        { name: 'Köttfärs', exp: 'Nötfärs 12% 500g', price: 59.90 },
        { name: 'Kycklingfilé', exp: 'Färsk Kycklingbröstfilé 1kg', price: 119.00 },
        { name: 'Lax', exp: 'Färsk Laxfilé 4-pack', price: 99.00 },
        { name: 'Bacon', exp: 'Alspånsrökt Bacon 140g', price: 14.90 },
        { name: 'Korv', exp: 'Falukorv 800g', price: 44.90 }
    ],
    'Bageri': [
        { name: 'Rågbröd', exp: 'Mörkt Rågbröd Skivat', price: 26.90 },
        { name: 'Formfranska', exp: 'Ljust Rostat Bröd', price: 22.90 },
        { name: 'Knäckebröd', exp: 'Husmans Knäckebröd', price: 18.90 }
    ],
    'Frysvaror': [
        { name: 'Ärtor', exp: 'Frysta Gröna Ärtor 500g', price: 19.90 },
        { name: 'Pommes frites', exp: 'Strips 1kg', price: 29.90 },
        { name: 'Fiskpinnar', exp: 'Fiskpinnar 15-pack', price: 34.90 }
    ],
    'Hushåll': [
        { name: 'Toalettpapper', exp: 'Toalettpapper 8-rullar', price: 49.90 },
        { name: 'Hushållspapper', exp: 'Hushållspapper 4-rullar', price: 39.90 },
        { name: 'Tvättmedel', exp: 'Kulörtvättmedel 1kg', price: 45.00 }
    ]
};

const allItemsList = [];
for (const [cat, items] of Object.entries(categories)) {
    for (const item of items) {
        allItemsList.push({ ...item, category: cat });
    }
}

// Helpers
function randomInt(min, max) {
    return Math.floor(Math.random() * (max - min + 1)) + min;
}

function randomDate(start, end) {
    return new Date(start.getTime() + Math.random() * (end.getTime() - start.getTime()));
}

function randomItem() {
    return allItemsList[randomInt(0, allItemsList.length - 1)];
}

const receipts = [];
const history = [];
const inventory = {};

// 1 Year ago
const startDate = new Date();
startDate.setFullYear(startDate.getFullYear() - 1);
const endDate = new Date();

let currentDate = new Date(startDate);
let receiptId = 1;

while (currentDate <= endDate) {
    // 1-2 receipts per week
    const daysToNextShopping = randomInt(3, 7);
    currentDate.setDate(currentDate.getDate() + daysToNextShopping);

    if (currentDate > endDate) break;

    const numItems = randomInt(5, 15);
    const storeName = stores[randomInt(0, stores.length - 1)];
    let receiptTotal = 0;
    
    const receiptItems = [];

    for (let i = 0; i < numItems; i++) {
        const item = randomItem();
        let qty = 1;
        if (item.isWeight) {
            qty = randomInt(5, 20) / 10; // 0.5 to 2.0 kg
        } else if (item.price < 30) {
            qty = randomInt(1, 3);
        }
        
        const price = item.price;
        const totalLinePrice = price * qty;
        receiptTotal += totalLinePrice;

        receiptItems.push({
            timestamp: new Date(currentDate.getTime() + i * 1000).toISOString(),
            itemName: item.name,
            expandedName: item.exp,
            action: 'Add',
            quantity: qty,
            price: price,
            totalPrice: totalLinePrice,
            currency: 'SEK',
            source: 'Receipt',
            receiptId: receiptId,
            category: item.category
        });

        // Update inventory
        if (!inventory[item.name]) {
            inventory[item.name] = { 
                name: item.name, 
                quantity: 0, 
                category: item.category, 
                lastPrice: price, 
                updatedAt: '' 
            };
        }
        inventory[item.name].quantity += qty;
        inventory[item.name].lastPrice = price;
        inventory[item.name].updatedAt = new Date(currentDate.getTime() + i * 1000).toISOString();
    }

    receipts.push({
        id: receiptId,
        timestamp: currentDate.toISOString(),
        storeName: storeName,
        totalAmountPaid: receiptTotal.toFixed(2)
    });

    history.push(...receiptItems);

    // Simulate consumption between this and next shopping trip
    const consumeDate = new Date(currentDate);
    for (let i = 0; i < daysToNextShopping; i++) {
        consumeDate.setDate(consumeDate.getDate() + 1);
        if (consumeDate > endDate) break;

        // Consume 1-4 items
        const consumeCount = randomInt(1, 4);
        for (let j = 0; j < consumeCount; j++) {
            const invItems = Object.values(inventory).filter(x => x.quantity > 0.1);
            if (invItems.length === 0) break;

            const itemToConsume = invItems[randomInt(0, invItems.length - 1)];
            let consumeQty = 1;
            
            const originalDef = allItemsList.find(x => x.name === itemToConsume.name);

            if (originalDef && originalDef.isWeight) {
                consumeQty = randomInt(2, 5) / 10;
            }

            if (consumeQty > itemToConsume.quantity) {
                consumeQty = itemToConsume.quantity;
            }

            itemToConsume.quantity -= consumeQty;
            itemToConsume.updatedAt = consumeDate.toISOString();

            history.push({
                timestamp: consumeDate.toISOString(),
                itemName: itemToConsume.name,
                expandedName: originalDef ? originalDef.exp : itemToConsume.name,
                action: 'Remove',
                quantity: consumeQty,
                price: null,
                totalPrice: null,
                currency: 'SEK',
                source: 'Manual',
                receiptId: null,
                category: itemToConsume.category
            });
        }
    }

    receiptId++;
}

// Build SQL
let sql = '';

// Receipts
sql += `INSERT INTO Receipts (Id, Timestamp, StoreName, TotalAmountPaid) VALUES\n`;
const receiptValues = receipts.map(r => `(${r.id}, '${r.timestamp}', '${r.storeName.replace(/'/g, "''")}', ${r.totalAmountPaid})`);
sql += receiptValues.join(',\n') + ';\n\n';

// History (chunked to avoid huge insert statements)
function chunkArray(arr, size) {
    const chunked = [];
    for (let i = 0; i < arr.length; i += size) {
        chunked.push(arr.slice(i, i + size));
    }
    return chunked;
}

const historyChunks = chunkArray(history, 100);
for (const chunk of historyChunks) {
    sql += `INSERT INTO History (Timestamp, ItemName, ExpandedName, Action, Quantity, Price, TotalPrice, Currency, Source, ReceiptId) VALUES\n`;
    const hValues = chunk.map(h => {
        const price = h.price ? h.price.toFixed(2) : 'NULL';
        const totalPrice = h.totalPrice ? h.totalPrice.toFixed(2) : 'NULL';
        const rId = h.receiptId ? h.receiptId : 'NULL';
        return `('${h.timestamp}', '${h.itemName.replace(/'/g, "''")}', '${h.expandedName.replace(/'/g, "''")}', '${h.action}', ${h.quantity.toFixed(2)}, ${price}, ${totalPrice}, '${h.currency}', '${h.source}', ${rId})`;
    });
    sql += hValues.join(',\n') + ';\n\n';
}

// Inventory
const validInventory = Object.values(inventory).filter(i => i.quantity > 0.05);
if (validInventory.length > 0) {
    sql += `INSERT INTO Inventory (ItemName, Quantity, Category, LastPrice, Currency, UpdatedAt) VALUES\n`;
    const iValues = validInventory.map(i => {
        return `('${i.name.replace(/'/g, "''")}', ${i.quantity.toFixed(2)}, '${i.category}', ${i.lastPrice.toFixed(2)}, 'SEK', '${i.updatedAt}')`;
    });
    sql += iValues.join(',\n') + ';\n\n';
}

fs.writeFileSync(path.join(__dirname, '..', 'data', 'seed.sql'), sql, 'utf8');
console.log('Successfully generated seed.sql with ~1 year of Swedish data.');
