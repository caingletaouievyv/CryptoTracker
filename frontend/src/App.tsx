import { useState, useEffect, useMemo, type FormEvent } from 'react'
import {
  getTransactions,
  addTransaction,
  getPortfolio,
  getHoldings,
  setHoldings,
  syncOkxTransactions,
  syncBinanceTransactions,
  login,
  register,
  clearAuthSession,
  getAuthToken,
  getStoredUsername,
} from './services/api'
import type { Transaction, CreateTransactionRequest, PortfolioPosition, PortfolioSummary, HoldingItem } from './types'
import { AppShell, type AppTab } from './components/layout/AppShell'
import { PortfolioView } from './components/PortfolioView'
import { TransactionsView } from './components/TransactionsView'
import { HoldingsView } from './components/HoldingsView'
import { SignInView } from './components/SignInView'
import { filterVisiblePositions } from './utils/portfolio'

const THEME_KEY = 'theme'
const OKX_CREDS_KEY = 'cryptotracker_okx'
const BINANCE_CREDS_KEY = 'cryptotracker_binance'

function getStoredOkxCreds(): { apiKey: string; secretKey: string; passphrase: string } {
  try {
    const s = localStorage.getItem(OKX_CREDS_KEY)
    if (s) {
      const o = JSON.parse(s) as Record<string, string>
      return { apiKey: o.apiKey ?? '', secretKey: o.secretKey ?? '', passphrase: o.passphrase ?? '' }
    }
  } catch (_) {}
  return { apiKey: '', secretKey: '', passphrase: '' }
}

function getStoredBinanceCreds(): { apiKey: string; secretKey: string; symbols: string; historyLookbackDays: string } {
  try {
    const s = localStorage.getItem(BINANCE_CREDS_KEY)
    if (s) {
      const o = JSON.parse(s) as Record<string, string>
      return {
        apiKey: o.apiKey ?? '',
        secretKey: o.secretKey ?? '',
        symbols: o.symbols ?? '',
        historyLookbackDays: typeof o.historyLookbackDays === 'string' ? o.historyLookbackDays.trim() : '',
      }
    }
  } catch (_) {}
  return { apiKey: '', secretKey: '', symbols: '', historyLookbackDays: '' }
}

type Theme = 'light' | 'dark'

function getStoredTheme(): Theme {
  const s = localStorage.getItem(THEME_KEY)
  if (s === 'light' || s === 'dark') return s
  if (typeof window !== 'undefined' && window.matchMedia?.('(prefers-color-scheme: light)').matches) return 'light'
  return 'dark'
}

function emptyHoldingRow(): HoldingItem {
  return { symbol: '', currentQuantity: 0, source: 'OKX', sellTargetUsd: undefined, buyZoneUsd: undefined }
}

function holdingsToEditableRows(list: {
  symbol: string
  currentQuantity: number
  source: string
  sellTargetUsd?: number | null
  buyZoneUsd?: number | null
}[]): HoldingItem[] {
  if (list.length === 0) return [emptyHoldingRow()]
  return list.map(h => ({
    symbol: h.symbol,
    currentQuantity: h.currentQuantity,
    source: h.source || 'OKX',
    sellTargetUsd: h.sellTargetUsd ?? undefined,
    buyZoneUsd: h.buyZoneUsd ?? undefined,
  }))
}

const emptySummary = (): PortfolioSummary => ({
  totalValueUsd: 0,
  totalCostBasis: 0,
  totalUnrealizedPnl: 0,
  realizedSellProceedsUsd: 0,
})

function isNoiseTransaction(t: Transaction): boolean {
  const noiseTypes = ['Fee', 'Deposit', 'Withdraw']
  if (noiseTypes.includes(t.type)) return true
  if (t.type === 'Buy' && !(t.priceAtTransaction > 0)) return true
  return false
}

const PAGE_TITLES: Record<AppTab, string> = {
  portfolio: 'Portfolio',
  holdings: 'Holdings',
  transactions: 'Transactions',
}

export default function App() {
  const [theme, setTheme] = useState<Theme>(() => getStoredTheme())
  const [hasToken, setHasToken] = useState(() => !!getAuthToken())
  const [authUsername, setAuthUsername] = useState(() => getStoredUsername() ?? '')
  const [authUsernameInput, setAuthUsernameInput] = useState('')
  const [authPassword, setAuthPassword] = useState('')
  const [authBusy, setAuthBusy] = useState(false)
  const [authError, setAuthError] = useState<string | null>(null)
  const [list, setList] = useState<Transaction[]>([])
  const [portfolio, setPortfolio] = useState<PortfolioPosition[]>([])
  const [portfolioSummary, setPortfolioSummary] = useState<PortfolioSummary>(emptySummary)
  const [loading, setLoading] = useState(() => !!getAuthToken())
  const [error, setError] = useState<string | null>(null)
  const [form, setForm] = useState<CreateTransactionRequest>({
    symbol: '',
    type: 'Buy',
    quantity: 0,
    priceAtTransaction: 0,
    fee: 0,
    date: new Date().toISOString().slice(0, 10),
    baseCurrency: 'USD',
    notes: undefined,
  })
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [okxCreds, setOkxCreds] = useState(getStoredOkxCreds)
  const [okxSaved, setOkxSaved] = useState(false)
  const [syncStatus, setSyncStatus] = useState<string | null>(null)
  const [syncing, setSyncing] = useState(false)
  const [binanceCreds, setBinanceCreds] = useState(getStoredBinanceCreds)
  const [binanceSaved, setBinanceSaved] = useState(false)
  const [binanceSyncStatus, setBinanceSyncStatus] = useState<string | null>(null)
  const [binanceSyncing, setBinanceSyncing] = useState(false)
  const [holdingRows, setHoldingRows] = useState<HoldingItem[]>([emptyHoldingRow()])
  const [holdingError, setHoldingError] = useState<string | null>(null)
  const [holdingSaving, setHoldingSaving] = useState(false)
  const [showNoiseTransactions, setShowNoiseTransactions] = useState(false)
  const [activeTab, setActiveTab] = useState<AppTab>('portfolio')

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme)
    localStorage.setItem(THEME_KEY, theme)
  }, [theme])

  const load = async () => {
    setLoading(true)
    setError(null)
    try {
      const [txs, dashboard, holdings] = await Promise.all([getTransactions(), getPortfolio(), getHoldings()])
      setList(txs)
      setPortfolio(dashboard.positions ?? [])
      setPortfolioSummary(dashboard.summary ?? emptySummary())
      setHoldingRows(holdingsToEditableRows(holdings))
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (!hasToken) {
      setList([])
      setPortfolio([])
      setPortfolioSummary(emptySummary())
      setHoldingRows([emptyHoldingRow()])
      setLoading(false)
      return
    }
    void load()
  }, [hasToken])

  const visiblePortfolio = useMemo(() => filterVisiblePositions(portfolio), [portfolio])

  const visibleTransactions = useMemo(
    () => (showNoiseTransactions ? list : list.filter(t => !isNoiseTransaction(t))),
    [list, showNoiseTransactions],
  )

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    setSubmitError(null)
    try {
      const payload: CreateTransactionRequest = {
        ...form,
        quantity: Number(form.quantity),
        priceAtTransaction: Number(form.priceAtTransaction),
        fee: Number(form.fee),
        date: form.date.length === 10 ? `${form.date}T12:00:00Z` : form.date,
        notes: form.notes?.trim() || undefined,
      }
      await addTransaction(payload)
      setForm(f => ({ ...f, symbol: '', quantity: 0, priceAtTransaction: 0, fee: 0, notes: undefined }))
      await load()
    } catch (e) {
      setSubmitError(e instanceof Error ? e.message : 'Failed to add')
    } finally {
      setSubmitting(false)
    }
  }

  const doLogin = async () => {
    setAuthError(null)
    setAuthBusy(true)
    try {
      const r = await login(authUsernameInput.trim(), authPassword)
      setAuthUsername(r.username)
      setAuthPassword('')
      setHasToken(true)
    } catch (e) {
      setAuthError(e instanceof Error ? e.message : 'Login failed')
    } finally {
      setAuthBusy(false)
    }
  }

  const doRegister = async () => {
    setAuthError(null)
    setAuthBusy(true)
    try {
      const r = await register(authUsernameInput.trim(), authPassword)
      setAuthUsername(r.username)
      setAuthPassword('')
      setHasToken(true)
    } catch (e) {
      setAuthError(e instanceof Error ? e.message : 'Register failed')
    } finally {
      setAuthBusy(false)
    }
  }

  const doLogout = () => {
    clearAuthSession()
    setHasToken(false)
    setAuthUsername('')
    setAuthUsernameInput('')
    setAuthPassword('')
  }

  const saveHoldings = async () => {
    setHoldingError(null)
    const payload = holdingRows
      .filter(r => r.symbol.trim())
      .map(r => ({
        symbol: r.symbol.trim().toUpperCase(),
        currentQuantity: Number(r.currentQuantity),
        source: (r.source || 'OKX').trim() || 'OKX',
        sellTargetUsd: typeof r.sellTargetUsd === 'number' && !Number.isNaN(r.sellTargetUsd) ? r.sellTargetUsd : undefined,
        buyZoneUsd: typeof r.buyZoneUsd === 'number' && !Number.isNaN(r.buyZoneUsd) ? r.buyZoneUsd : undefined,
      }))
    if (payload.some(p => Number.isNaN(p.currentQuantity))) {
      setHoldingError('Each quantity must be a number.')
      return
    }
    setHoldingSaving(true)
    try {
      await setHoldings(payload)
      await load()
    } catch (e) {
      setHoldingError(e instanceof Error ? e.message : 'Save failed')
    } finally {
      setHoldingSaving(false)
    }
  }

  const saveOkxCreds = () => {
    localStorage.setItem(OKX_CREDS_KEY, JSON.stringify(okxCreds))
    setOkxSaved(true)
    setTimeout(() => setOkxSaved(false), 2000)
  }

  const runSyncTransactions = async () => {
    const stored = getStoredOkxCreds()
    if (!stored.apiKey?.trim() || !stored.secretKey?.trim() || !stored.passphrase?.trim()) {
      setSyncStatus('Enter OKX keys and click Save first.')
      return
    }
    setSyncing(true)
    setSyncStatus(null)
    try {
      const r = await syncOkxTransactions(stored)
      setSyncStatus(r.synced > 0
        ? `Synced ${r.synced} transactions from OKX; ${r.updated} prices filled.`
        : (r.message ?? `Synced ${r.synced} transactions.`))
      await load()
    } catch (e) {
      setSyncStatus(e instanceof Error ? e.message : 'Sync failed')
    } finally {
      setSyncing(false)
    }
  }

  const saveBinanceCreds = () => {
    localStorage.setItem(BINANCE_CREDS_KEY, JSON.stringify(binanceCreds))
    setBinanceSaved(true)
    setTimeout(() => setBinanceSaved(false), 2000)
  }

  const runSyncBinance = async () => {
    const apiKey = binanceCreds.apiKey?.trim()
    const secretKey = binanceCreds.secretKey?.trim()
    if (!apiKey || !secretKey) {
      setBinanceSyncStatus('Enter Binance API key and secret, then Save.')
      return
    }
    const symbols = binanceCreds.symbols.split(/[\s,]+/).map(s => s.trim().toUpperCase()).filter(Boolean)
    if (symbols.length === 0) {
      setBinanceSyncStatus('Add at least one Spot symbol (e.g. BTCUSDT).')
      return
    }
    const lookbackRaw = binanceCreds.historyLookbackDays?.trim() ?? ''
    let historyLookbackDays: number | undefined
    if (lookbackRaw === '' || lookbackRaw === '0') {
      historyLookbackDays = 0
    } else {
      const n = Number.parseInt(lookbackRaw, 10)
      if (!Number.isFinite(n) || n < 1 || n > 3650) {
        setBinanceSyncStatus('History (days): empty or 0 for full scan, or 1–3650.')
        return
      }
      historyLookbackDays = n
    }
    setBinanceSyncing(true)
    setBinanceSyncStatus(null)
    try {
      const r = await syncBinanceTransactions({ apiKey, secretKey, symbols, historyLookbackDays })
      setBinanceSyncStatus(r.synced > 0
        ? `Synced ${r.synced} trades from Binance; ${r.updated} prices filled.`
        : (r.message ?? `Synced ${r.synced} trades.`))
      await load()
    } catch (e) {
      setBinanceSyncStatus(e instanceof Error ? e.message : 'Binance sync failed')
    } finally {
      setBinanceSyncing(false)
    }
  }

  if (!hasToken) {
    return (
      <SignInView
        authError={authError}
        username={authUsernameInput}
        password={authPassword}
        busy={authBusy}
        onUsernameChange={setAuthUsernameInput}
        onPasswordChange={setAuthPassword}
        onLogin={doLogin}
        onRegister={doRegister}
        theme={theme}
        onToggleTheme={() => setTheme(t => t === 'dark' ? 'light' : 'dark')}
      />
    )
  }

  return (
    <AppShell
      activeTab={activeTab}
      onTabChange={setActiveTab}
      username={authUsername || 'User'}
      onLogout={doLogout}
      theme={theme}
      onToggleTheme={() => setTheme(t => t === 'dark' ? 'light' : 'dark')}
      pageTitle={PAGE_TITLES[activeTab]}
    >
      {activeTab === 'portfolio' && (
        <PortfolioView
          loading={loading}
          error={error}
          summary={portfolioSummary}
          positions={portfolio}
          visiblePositions={visiblePortfolio}
        />
      )}
      {activeTab === 'holdings' && (
        <HoldingsView
          rows={holdingRows}
          setRows={setHoldingRows}
          emptyRow={emptyHoldingRow}
          saving={holdingSaving}
          error={holdingError}
          onSave={saveHoldings}
        />
      )}
      {activeTab === 'transactions' && (
        <TransactionsView
          loading={loading}
          error={error}
          transactions={visibleTransactions}
          showNoise={showNoiseTransactions}
          onShowNoiseChange={setShowNoiseTransactions}
          form={form}
          setForm={setForm}
          submitting={submitting}
          submitError={submitError}
          onSubmit={handleSubmit}
          okxCreds={okxCreds}
          setOkxCreds={setOkxCreds}
          okxSaved={okxSaved}
          saveOkxCreds={saveOkxCreds}
          syncing={syncing}
          syncStatus={syncStatus}
          runSyncOkx={runSyncTransactions}
          binanceCreds={binanceCreds}
          setBinanceCreds={setBinanceCreds}
          binanceSaved={binanceSaved}
          saveBinanceCreds={saveBinanceCreds}
          binanceSyncing={binanceSyncing}
          binanceSyncStatus={binanceSyncStatus}
          runSyncBinance={runSyncBinance}
        />
      )}
    </AppShell>
  )
}
