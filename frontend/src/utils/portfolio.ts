import type { PortfolioPosition } from '../types'

export function filterVisiblePositions(positions: PortfolioPosition[]): PortfolioPosition[] {
  return positions.filter(p => Math.abs(p.quantity) >= 1e-10)
}

export function btcDominancePercent(positions: PortfolioPosition[]): number | null {
  const total = positions.reduce((sum, p) => sum + (p.currentValueUsd ?? 0), 0)
  if (total <= 0) return null
  const btc = positions.find(p => p.symbol.toUpperCase() === 'BTC')
  if (!btc?.currentValueUsd) return null
  return (btc.currentValueUsd / total) * 100
}
