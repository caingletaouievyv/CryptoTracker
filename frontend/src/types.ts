/** Mirrors API TransactionResponse (camelCase). */
export interface Transaction {
  id: string
  symbol: string
  type: string
  quantity: number
  priceAtTransaction: number
  fee: number
  netValue: number
  date: string
  baseCurrency: string
  notes?: string | null
}

/** Mirrors API CreateTransactionRequest (camelCase). */
export interface CreateTransactionRequest {
  symbol: string
  type: string
  quantity: number
  priceAtTransaction: number
  fee: number
  date: string
  baseCurrency: string
  notes?: string | null
}

/** Mirrors API PortfolioPositionResponse (camelCase). */
export interface PortfolioPosition {
  symbol: string
  quantity: number
  source?: string | null
  totalCostBasis: number
  averagePricePerUnit: number
  currentPriceUsd?: number | null
  currentValueUsd?: number | null
  unrealizedPnl?: number | null
  allocationPercent?: number | null
  strategyStatus?: string | null
}

/** GET /api/portfolio (camelCase). */
export interface PortfolioSummary {
  totalValueUsd: number
  totalCostBasis: number
  totalUnrealizedPnl: number
  realizedSellProceedsUsd: number
}

export interface PortfolioDashboard {
  positions: PortfolioPosition[]
  summary: PortfolioSummary
}

/** GET /api/holdings (camelCase). */
export interface Holding {
  symbol: string
  currentQuantity: number
  source: string
  lastUpdated: string
  sellTargetUsd?: number | null
  buyZoneUsd?: number | null
}

/** POST /api/holdings body item (camelCase). */
export interface HoldingItem {
  symbol: string
  currentQuantity: number
  source: string
  sellTargetUsd?: number | null
  buyZoneUsd?: number | null
}
