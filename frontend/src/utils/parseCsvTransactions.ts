import type { CreateTransactionRequest } from '../types'

const VALID_TYPES = new Set(['Buy', 'Sell', 'Swap', 'Fee', 'Deposit', 'Withdraw'])
const STABLES = new Set(['USDT', 'USDC', 'DAI', 'BUSD', 'TUSD', 'USDP'])
const OKX_SKIP_TRADE_TYPES = new Set(['transfer', 'swap', 'margin', 'futures', 'funding'])
const MAX_ROWS = 5000

export interface CsvParseResult {
  rows: CreateTransactionRequest[]
  skipped: number
  format: 'template' | 'okx'
}

interface HeaderScan {
  lineIndex: number
  delimiter: string
  headers: string[]
  format: 'template' | 'okx'
}

export function parseCsvTransactions(text: string): CsvParseResult {
  const raw = text.replace(/^\uFEFF/, '').trim()
  if (!raw) throw new Error('File is empty.')

  const lines = raw.split(/\r?\n/).filter(l => l.trim())
  if (lines.length < 2) throw new Error('CSV needs a header row and at least one data row.')

  const scan = findHeaderScan(lines)
  const { lineIndex: headerLineIndex, delimiter, headers, format } = scan

  const rows: CreateTransactionRequest[] = []
  let skipped = 0
  let dataLines = 0

  for (let i = headerLineIndex + 1; i < lines.length; i++) {
    const cells = parseCsvLine(lines[i], delimiter)
    if (cells.every(c => !c.trim())) continue
    if (isMetadataRow(cells)) continue

    dataLines++
    const row = format === 'okx'
      ? mapOkxRow(headers, cells)
      : mapTemplateRow(headers, cells)

    if (!row) {
      skipped++
      continue
    }
    rows.push(row)
    if (rows.length > MAX_ROWS) {
      throw new Error(`CSV exceeds ${MAX_ROWS} importable rows. Split the file or use seed scripts.`)
    }
  }

  if (rows.length === 0) {
    throw new Error(buildEmptyError(dataLines, skipped, format))
  }

  return { rows, skipped, format }
}

function findHeaderScan(lines: string[]): HeaderScan {
  for (let i = 0; i < Math.min(lines.length, 15); i++) {
    if (isMetadataLine(lines[i])) continue

    const delimiters = uniqueDelimiters(lines[i])
    for (const delimiter of delimiters) {
      const headers = parseCsvLine(lines[i], delimiter).map(normalizeHeader)
      if (isMetadataRow(headers)) continue
      try {
        const format = detectFormat(headers)
        return { lineIndex: i, delimiter, headers, format }
      } catch {
        continue
      }
    }
  }
  throw new Error(
    'Unrecognized CSV format. Use template columns (date, symbol, type, quantity, price, fee) or an OKX transaction export (Time, Trade Type, Symbol, Action, Amount, Filled Price).',
  )
}

function uniqueDelimiters(line: string): string[] {
  const primary = detectDelimiter(line)
  return [...new Set([primary, '\t', ',', ';'])]
}

function isMetadataLine(line: string): boolean {
  const trimmed = stripBom(line.trim()).toLowerCase()
  return trimmed.startsWith('uid:') || trimmed.startsWith('account type:') || trimmed.startsWith('time zone:')
}

function isMetadataRow(cells: string[]): boolean {
  const first = stripBom((cells[0] ?? '').trim()).toLowerCase()
  if (first.startsWith('uid:')) return true
  if (first.startsWith('account type:')) return true
  if (first.startsWith('time zone:')) return true
  if (cells.length <= 3 && cells.some(c => /^uid:/i.test(stripBom(c.trim())))) return true
  return false
}

function buildEmptyError(dataLines: number, skipped: number, format: string): string {
  if (dataLines === 0) {
    return 'No data rows found below the header. Export transactions from OKX with at least one trade row, then save as CSV (UTF-8).'
  }
  if (format === 'okx' && skipped === dataLines) {
    return `Found ${dataLines} row(s) but none were importable. OKX Trading History imports Spot rows only (Convert/trades via Balance Change + Balance Unit). Transfer, Swap, and Funding rows are skipped.`
  }
  return `No importable rows (${dataLines} data line(s), ${skipped} skipped). Check Trade Type, Action, Amount, and Time columns.`
}

function detectDelimiter(line: string): string {
  const tabs = (line.match(/\t/g) ?? []).length
  const semis = (line.match(/;/g) ?? []).length
  const commas = (line.match(/,/g) ?? []).length
  if (tabs >= commas && tabs >= semis && tabs > 0) return '\t'
  if (semis > commas) return ';'
  return ','
}

function detectFormat(headers: string[]): 'template' | 'okx' {
  if (isOkxHeaders(headers)) return 'okx'
  if (headers.includes('date') && headers.includes('symbol')) return 'template'
  if (headers.includes('time') && headers.includes('symbol') && headers.includes('type')) return 'template'
  throw new Error('Unrecognized headers')
}

function isOkxHeaders(headers: string[]): boolean {
  const hasTime = headers.some(h => h === 'time' || h.startsWith('time'))
  const hasAmount = headers.some(h => h === 'amount' || h.startsWith('amount'))
  const hasAction = headers.some(h => h === 'action' || h.startsWith('action'))
  const hasTradeType = headers.some(h => h.startsWith('trade typ') || h === 'type')
  const hasSymbol = headers.some(h => h === 'symbol' || h.startsWith('symbol'))
  const hasUnit = headers.some(h => h.startsWith('trading'))
  const hasLegacyCrypto = headers.includes('crypto')
  return hasTime && hasAmount && (hasAction || hasTradeType) && (hasSymbol || hasUnit || hasLegacyCrypto)
}

function stripBom(s: string): string {
  return s.replace(/^\uFEFF/, '')
}

function cleanCell(raw: string): string {
  let s = stripBom(raw.trim())
  if (s.startsWith('"') && s.endsWith('"') && s.length >= 2) {
    s = s.slice(1, -1).replace(/""/g, '"')
  }
  return s.trim()
}

function normalizeHeader(h: string): string {
  return cleanCell(h).toLowerCase().replace(/\s+/g, ' ')
}

function headerIndex(headers: string[], ...names: string[]): number {
  for (const name of names) {
    const exact = headers.indexOf(name)
    if (exact >= 0) return exact
    const fuzzy = headers.findIndex(h => h.startsWith(name) || name.startsWith(h))
    if (fuzzy >= 0) return fuzzy
  }
  return -1
}

function cell(cells: string[], headers: string[], ...names: string[]): string {
  const i = headerIndex(headers, ...names)
  return i >= 0 ? cleanCell(cells[i] ?? '') : ''
}

function mapTemplateRow(headers: string[], cells: string[]): CreateTransactionRequest | null {
  const symbol = cell(cells, headers, 'symbol').toUpperCase()
  const typeRaw = cell(cells, headers, 'type')
  const type = normalizeType(typeRaw)
  const quantity = parseNumber(cell(cells, headers, 'quantity', 'amount'))
  const price = parseNumber(cell(cells, headers, 'price', 'priceattransaction', 'fill price', 'filled price'))
  const fee = parseNumber(cell(cells, headers, 'fee')) || 0
  const dateRaw = cell(cells, headers, 'date', 'time')
  const baseCurrency = cell(cells, headers, 'basecurrency', 'base currency') || defaultBase(symbol)
  const notes = cell(cells, headers, 'notes', 'note') || undefined

  if (!symbol || !type || quantity === null || quantity === 0) return null
  if (!VALID_TYPES.has(type)) return null

  const date = parseDate(dateRaw)
  if (!date) return null

  return {
    symbol,
    type,
    quantity: Math.abs(quantity),
    priceAtTransaction: price ?? stablePrice(symbol),
    fee,
    date,
    baseCurrency,
    notes: notes || undefined,
  }
}

function mapOkxRow(headers: string[], cells: string[]): CreateTransactionRequest | null {
  const tradeType = cell(cells, headers, 'trade type', 'trade typ', 'type').toLowerCase()
  const action = cell(cells, headers, 'action', 'side').toLowerCase()

  if (isOkxSkippedRow(tradeType, action)) return null

  const fromBalance = mapOkxBalanceChangeRow(headers, cells, tradeType)
  if (fromBalance) return fromBalance

  return mapOkxActionRow(headers, cells, tradeType, action)
}

function isOkxSkippedRow(tradeType: string, action: string): boolean {
  if (tradeType === 'transfer' || action === 'transfer' || action.startsWith('transfer ')) return true
  if (OKX_SKIP_TRADE_TYPES.has(tradeType) || tradeType.includes('swap')) return true
  if (action.includes('funding') || action.includes('open long') || action.includes('close long')) return true
  return false
}

/** OKX Trading History export: Spot rows use signed Balance Change + Balance Unit (Action is often empty). */
function mapOkxBalanceChangeRow(
  headers: string[],
  cells: string[],
  tradeType: string,
): CreateTransactionRequest | null {
  if (headerIndex(headers, 'balance change') < 0 || headerIndex(headers, 'balance unit') < 0) return null
  if (tradeType && tradeType !== 'spot' && tradeType !== 'trade' && tradeType !== 'convert') return null

  const balanceChange = parseNumber(cell(cells, headers, 'balance change'))
  const symbol = cell(cells, headers, 'balance unit').toUpperCase()
  if (balanceChange === null || balanceChange === 0 || !symbol) return null

  const type = balanceChange > 0 ? 'Buy' : 'Sell'
  const quantity = Math.abs(balanceChange)
  const priceRaw = parseNumber(cell(cells, headers, 'filled price', 'filled pric', 'fill price', 'price'))
  const feeRaw = parseNumber(cell(cells, headers, 'fee'))
  const dateRaw = cell(cells, headers, 'time')
  const orderId = cell(cells, headers, 'order id', 'order')
  const notes = orderId ? `OKX order ${orderId}` : undefined

  const date = parseDate(dateRaw)
  if (!date) return null

  const price = STABLES.has(symbol) ? 1 : (priceRaw ?? 0)

  return {
    symbol,
    type,
    quantity,
    priceAtTransaction: price,
    fee: feeRaw ?? 0,
    date,
    baseCurrency: defaultBase(symbol),
    notes,
  }
}

/** Legacy OKX bill export with Action (Convert in/out, Buy, Sell). */
function mapOkxActionRow(
  headers: string[],
  cells: string[],
  tradeType: string,
  action: string,
): CreateTransactionRequest | null {
  const tradingUnit = cell(cells, headers, 'trading unit', 'trading u', 'crypto')
  const symbolCol = cell(cells, headers, 'symbol')
  const symbol = resolveOkxSymbol(tradingUnit, symbolCol)
  const quantityRaw = parseNumber(cell(cells, headers, 'amount'))
  if (!symbol || quantityRaw === null || quantityRaw === 0) return null

  const type = mapOkxAction(tradeType, action)
  if (!type || !VALID_TYPES.has(type)) return null

  const quantity = Math.abs(quantityRaw)
  const priceRaw = parseNumber(cell(cells, headers, 'filled price', 'filled pric', 'fill price', 'price'))
  const feeRaw = parseNumber(cell(cells, headers, 'fee'))
  const dateRaw = cell(cells, headers, 'time')
  const orderId = cell(cells, headers, 'order id', 'order')
  const notes = orderId ? `OKX order ${orderId}` : undefined

  const date = parseDate(dateRaw)
  if (!date) return null

  return {
    symbol,
    type,
    quantity,
    priceAtTransaction: priceRaw ?? stablePrice(symbol),
    fee: feeRaw ?? 0,
    date,
    baseCurrency: defaultBase(symbol),
    notes,
  }
}

function resolveOkxSymbol(tradingUnit: string, symbolCol: string): string {
  const unit = tradingUnit.trim().toUpperCase()
  if (unit && unit !== '--' && unit !== '-') return unit.split(/[/\-]/)[0]

  const sym = symbolCol.trim().toUpperCase()
  if (!sym || sym === '--' || sym === '-') return ''
  if (sym.includes('/')) return sym.split('/')[0]
  if (sym.includes('-')) return sym.split('-')[0]
  return sym
}

function mapOkxAction(billType: string, action: string): string | null {
  const a = (action || billType).toLowerCase().trim()
  const t = billType.toLowerCase().trim()

  if (a.includes('convert in') || a === 'buy' || a.endsWith(' buy')) return 'Buy'
  if (a.includes('convert out') || a === 'sell' || a.endsWith(' sell')) return 'Sell'
  if (t === 'convert' || a.includes('convert')) {
    if (a.includes(' in')) return 'Buy'
    if (a.includes(' out')) return 'Sell'
  }
  if (t.includes('trade') || t === 'spot' || t === 'spot and futures') {
    if (a.includes('buy')) return 'Buy'
    if (a.includes('sell')) return 'Sell'
  }
  if (a.includes('buy')) return 'Buy'
  if (a.includes('sell')) return 'Sell'
  if (a.includes('swap')) return 'Swap'
  return null
}

function normalizeType(raw: string): string {
  const t = raw.trim()
  if (!t) return ''
  const cap = t.charAt(0).toUpperCase() + t.slice(1).toLowerCase()
  if (cap === 'Transfer') return ''
  return VALID_TYPES.has(cap) ? cap : t
}

function parseNumber(raw: string): number | null {
  if (!raw?.trim() || raw.trim() === '--' || raw.trim() === '-') return null
  const cleaned = raw.replace(/,/g, '').trim()
  const n = Number(cleaned)
  if (Number.isFinite(n)) return n
  const stripped = cleaned.replace(/[^\d.eE+-]/g, '')
  if (!stripped) return null
  const n2 = Number(stripped)
  return Number.isFinite(n2) ? n2 : null
}

function stablePrice(symbol: string): number {
  return STABLES.has(symbol.toUpperCase()) ? 1 : 0
}

function defaultBase(symbol: string): string {
  return STABLES.has(symbol.toUpperCase()) ? symbol.toUpperCase() : 'USD'
}

function parseDate(raw: string): string | null {
  if (!raw?.trim() || raw.trim() === '--' || raw.trim() === '-') return null
  const s = raw.trim()

  const iso = Date.parse(s)
  if (!Number.isNaN(iso)) return new Date(iso).toISOString()

  const ymdTime = s.match(/^(\d{4})[/-](\d{1,2})[/-](\d{1,2})(?:[ T](\d{1,2}):(\d{2})(?::(\d{2}))?)?/)
  if (ymdTime) {
    const [, yyyy, mm, dd, hh = '0', min = '0', sec = '0'] = ymdTime
    const d = new Date(Date.UTC(Number(yyyy), Number(mm) - 1, Number(dd), Number(hh), Number(min), Number(sec)))
    if (!Number.isNaN(d.getTime())) return d.toISOString()
  }

  const mdy = s.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})(?:[, T]+(\d{1,2}):(\d{2})(?::(\d{2}))?)?/)
  if (mdy) {
    const [, mm, dd, yyyy, hh = '0', min = '0', sec = '0'] = mdy
    const d = new Date(Date.UTC(Number(yyyy), Number(mm) - 1, Number(dd), Number(hh), Number(min), Number(sec)))
    if (!Number.isNaN(d.getTime())) return d.toISOString()
  }

  if (/^\d{4}-\d{2}-\d{2}$/.test(s)) return `${s}T12:00:00.000Z`
  return null
}

function parseCsvLine(line: string, delimiter: string): string[] {
  const out: string[] = []
  let cur = ''
  let inQuotes = false

  for (let i = 0; i < line.length; i++) {
    const c = line[i]
    if (c === '"') {
      if (inQuotes && line[i + 1] === '"') {
        cur += '"'
        i++
      } else {
        inQuotes = !inQuotes
      }
      continue
    }
    if (c === delimiter && !inQuotes) {
      out.push(cleanCell(cur))
      cur = ''
      continue
    }
    cur += c
  }
  out.push(cleanCell(cur))
  return out
}
