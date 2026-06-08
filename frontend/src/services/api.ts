import type { Transaction, CreateTransactionRequest, PortfolioDashboard, Holding, HoldingItem } from '../types'

const base = import.meta.env.VITE_API_URL ?? 'http://localhost:5260'

const AUTH_TOKEN_KEY = 'cryptotracker_jwt'
const AUTH_USERNAME_KEY = 'cryptotracker_username'

export function getAuthToken(): string | null {
  return localStorage.getItem(AUTH_TOKEN_KEY)
}

export function clearAuthSession(): void {
  localStorage.removeItem(AUTH_TOKEN_KEY)
  localStorage.removeItem(AUTH_USERNAME_KEY)
  localStorage.removeItem('cryptotracker_user_email') // legacy key from email-based auth
}

function setAuthSession(token: string, username: string): void {
  localStorage.setItem(AUTH_TOKEN_KEY, token)
  localStorage.setItem(AUTH_USERNAME_KEY, username)
}

export function getStoredUsername(): string | null {
  return localStorage.getItem(AUTH_USERNAME_KEY)
}

function withAuth(init: RequestInit = {}): RequestInit {
  const headers = new Headers(init.headers)
  const t = getAuthToken()
  if (t) headers.set('Authorization', `Bearer ${t}`)
  return { ...init, headers }
}

interface ApiEnvelope<T> {
  success: boolean
  message: string
  data?: T
}

interface PagedList<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

async function parseJson<T>(res: Response): Promise<ApiEnvelope<T>> {
  try {
    return (await res.json()) as ApiEnvelope<T>
  } catch {
    throw new Error('Invalid JSON response')
  }
}

async function expectOkData<T>(res: Response): Promise<T> {
  const json = await parseJson<T>(res)
  if (!res.ok || !json.success) {
    throw new Error(json.message || `HTTP ${res.status}`)
  }
  return json.data as T
}

async function expectOkVoid(res: Response): Promise<void> {
  const json = await parseJson<unknown>(res)
  if (!res.ok || !json.success) {
    throw new Error(json.message || `HTTP ${res.status}`)
  }
}

export interface AuthResult {
  accessToken: string
  expiresAtUtc: string
  username: string
}

export async function register(username: string, password: string): Promise<AuthResult> {
  const res = await fetch(`${base}/api/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password }),
  })
  const data = await expectOkData<AuthResult>(res)
  setAuthSession(data.accessToken, data.username)
  return data
}

export async function login(username: string, password: string): Promise<AuthResult> {
  const res = await fetch(`${base}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password }),
  })
  const data = await expectOkData<AuthResult>(res)
  setAuthSession(data.accessToken, data.username)
  return data
}

export async function getTransactions(): Promise<Transaction[]> {
  const all: Transaction[] = []
  let page = 1
  const pageSize = 100
  for (;;) {
    const res = await fetch(`${base}/api/transaction?page=${page}&pageSize=${pageSize}`, withAuth())
    const paged = await expectOkData<PagedList<Transaction>>(res)
    all.push(...paged.items)
    if (page >= paged.totalPages || paged.items.length === 0) break
    page++
  }
  return all
}

export async function addTransaction(t: CreateTransactionRequest): Promise<Transaction> {
  const res = await fetch(`${base}/api/transaction`, withAuth({
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(t),
  }))
  return expectOkData<Transaction>(res)
}

export async function getPortfolio(): Promise<PortfolioDashboard> {
  const res = await fetch(`${base}/api/portfolio`, withAuth())
  return expectOkData<PortfolioDashboard>(res)
}

export async function getHoldings(): Promise<Holding[]> {
  const res = await fetch(`${base}/api/holdings`, withAuth())
  return expectOkData<Holding[]>(res)
}

export async function setHoldings(holdings: HoldingItem[]): Promise<void> {
  const res = await fetch(`${base}/api/holdings`, withAuth({
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ holdings }),
  }))
  await expectOkVoid(res)
}

export interface OkxSyncBody {
  apiKey?: string
  secretKey?: string
  passphrase?: string
  after?: string
  limit?: number
}

export interface OkxSyncResult {
  synced: number
  updated: number
  message?: string
}

export async function syncOkxTransactions(body?: OkxSyncBody): Promise<OkxSyncResult> {
  const res = await fetch(`${base}/api/sync/okx/transactions`, withAuth({
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body ?? {}),
  }))
  return expectOkData<OkxSyncResult>(res)
}

export interface BinanceSyncBody {
  apiKey?: string
  secretKey?: string
  symbols: string[]
  historyLookbackDays?: number
}

export async function syncBinanceTransactions(body: BinanceSyncBody): Promise<OkxSyncResult> {
  const payload: Record<string, unknown> = {
    apiKey: body.apiKey,
    secretKey: body.secretKey,
    symbols: body.symbols,
  }
  if (body.historyLookbackDays != null)
    payload.historyLookbackDays = body.historyLookbackDays
  const res = await fetch(`${base}/api/sync/binance/transactions`, withAuth({
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  }))
  return expectOkData<OkxSyncResult>(res)
}
