/**
 * POST seed-holdings.json to /api/holdings (replaces current holdings).
 * From repo root: node scripts/seed-holdings-api.js. API must be running.
 * Alternative: use the app section **Holdings & workflow** (copy amounts from exchange Assets, including Earn).
 */
const fs = require('fs');
const path = require('path');

const API = process.env.API_URL || 'http://localhost:5260';
const TOKEN = process.env.CRYPTOTRACKER_TOKEN || '';
const jsonPath = path.join(__dirname, 'seed-holdings.json');
let data;
try {
  data = JSON.parse(fs.readFileSync(jsonPath, 'utf8'));
} catch (e) {
  console.error('Failed to read seed-holdings.json:', e.message);
  process.exit(1);
}

async function seed() {
  if (!TOKEN) {
    console.error('Set CRYPTOTRACKER_TOKEN to a JWT from POST /api/auth/login.');
    process.exit(1);
  }
  console.log('Seeding holdings to', API, '...');
  try {
    const res = await fetch(`${API}/api/holdings`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${TOKEN}` },
      body: JSON.stringify({ holdings: data }),
    });
    if (res.ok) console.log('Done. Seeded', data.length, 'holdings.');
    else console.error('Error', res.status, await res.text());
  } catch (e) {
    console.error('Request failed:', e.message);
  }
}
seed();
