/**
 * One-off: POST seed-transactions.json to the API.
 * Run after DB reset. Backend must be running. From repo root: node scripts/seed-api.js
 */
const fs = require('fs');
const path = require('path');

const API = process.env.API_URL || 'http://localhost:5260';
const TOKEN = process.env.CRYPTOTRACKER_TOKEN || '';
const jsonPath = path.join(__dirname, 'seed-transactions.json');
const data = JSON.parse(fs.readFileSync(jsonPath, 'utf8'));

function headersJson() {
  const h = { 'Content-Type': 'application/json' };
  if (TOKEN) h.Authorization = `Bearer ${TOKEN}`;
  return h;
}

async function seed() {
  if (!TOKEN) {
    console.error('Set CRYPTOTRACKER_TOKEN to a JWT from POST /api/auth/login (same user whose data you are seeding).');
    process.exit(1);
  }
  let ok = 0, err = 0;
  for (const tx of data) {
    try {
      const res = await fetch(`${API}/api/transaction`, {
        method: 'POST',
        headers: headersJson(),
        body: JSON.stringify(tx),
      });
      if (res.ok) ok++; else { err++; console.warn(res.status, await res.text()); }
    } catch (e) {
      err++;
      console.warn(e.message);
    }
  }
  console.log(`Seed done: ${ok} ok, ${err} failed.`);
}

seed();
