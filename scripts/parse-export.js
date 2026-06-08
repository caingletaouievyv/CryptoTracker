/**
 * One-off: parse export table (Time, Note, Amount, Before, After, Symbol, Type)
 * into seed-transactions.json format. Run: node scripts/parse-export.js
 * Reads scripts/export-paste.txt, writes scripts/seed-transactions.json
 */
const fs = require('fs');
const path = require('path');

const inputPath = path.join(__dirname, 'export-paste.txt');
const outputPath = path.join(__dirname, 'seed-transactions.json');

if (!fs.existsSync(inputPath)) {
  console.error('Create export-paste.txt with tab-separated: Time, Note, Amount, Before, After, Symbol, Type');
  process.exit(1);
}

const text = fs.readFileSync(inputPath, 'utf8');
const lines = text.split(/\r?\n/).filter(l => l.trim());
const isHeader = (l) => l.toLowerCase().includes('time') && l.toLowerCase().includes('note');
const dataLines = lines.filter(l => !isHeader(l));

function parseTime(s) {
  const [datePart, timePart] = (s || '').trim().split(/\s+/);
  if (!datePart) return new Date().toISOString();
  const [m, d, y] = datePart.split('/');
  const [h, min] = (timePart || '0:0').split(':');
  const iso = `${y.padStart(4, '20')}-${m.padStart(2, '0')}-${d.padStart(2, '0')}T${(h || '0').padStart(2, '0')}:${(min || '0').padStart(2, '0')}:00Z`;
  return iso;
}

function inferType(note, amount, symbol) {
  const n = (note || '').toLowerCase();
  const amt = parseFloat(String(amount).replace(/,/g, ''));
  const stables = ['usdt', 'usdc', 'pi', 'oksol', 'night'];
  const isStable = stables.includes((symbol || '').toLowerCase());

  if (amt < 0) {
    if (n.includes('subscription') || n.includes('stake') || n.includes('staking')) return 'Fee';
    if (n.includes('to unified') || n.includes('withdrawal') || n.includes('withdraw')) return 'Withdraw';
    if (n.includes('place an order') || n.includes('fulfill')) return 'Sell';
    return 'Fee';
  }
  if (amt > 0) {
    if (n.includes('from unified') || n.includes('redemption') || n.includes('fulfill an order') || n.includes('deposit') || n.includes('collection') || n.includes('received') || n.includes('profit') || n.includes('yield') || n.includes('swapping')) {
      return isStable ? 'Deposit' : 'Buy';
    }
    if (n.includes('swap')) return 'Swap';
    return isStable ? 'Deposit' : 'Buy';
  }
  return 'Deposit';
}

const out = [];
for (const line of dataLines) {
  const parts = line.split('\t');
  if (parts.length < 6) continue;
  const [time, note, amount, before, after, symbol, typeCol] = parts;
  const amt = parseFloat(String(amount).replace(/,/g, ''));
  if (isNaN(amt) || amt === 0) continue;

  const baseCurrency = (symbol || '').toUpperCase() === 'USDT' ? 'USDT' : 'USD';
  const price = ['USDT', 'USDC', 'PI', 'OKSOL', 'NIGHT'].includes((symbol || '').toUpperCase()) ? 1 : 0;

  out.push({
    symbol: (symbol || '').trim(),
    type: inferType(note, amount, symbol),
    quantity: amt,
    priceAtTransaction: price,
    fee: 0,
    date: parseTime(time),
    baseCurrency,
    notes: (note || '').trim() || undefined,
  });
}

fs.writeFileSync(outputPath, JSON.stringify(out, null, 2), 'utf8');
console.log(`Wrote ${out.length} transactions to seed-transactions.json`);
