import type { PortfolioPosition, PortfolioSummary } from '../types'
import { formatMoney, formatOptionalMoney, formatQuantity, formatPercent, pnlClass } from '../utils/format'
import { formatStrategyLabel, strategyTagClass } from '../utils/strategy'
import { btcDominancePercent } from '../utils/portfolio'

type Props = {
  loading: boolean
  error: string | null
  summary: PortfolioSummary
  positions: PortfolioPosition[]
  visiblePositions: PortfolioPosition[]
}

export function PortfolioView({ loading, error, summary, positions, visiblePositions }: Props) {
  const btcDom = btcDominancePercent(positions)

  return (
    <div className="view-portfolio">
      <div className="stat-row" role="region" aria-label="Portfolio summary">
        <div className="stat-card">
          <span className="stat-label">Total value</span>
          <span className="stat-value num">{formatMoney(summary.totalValueUsd)}</span>
        </div>
        <div className="stat-card">
          <span className="stat-label">Cost basis</span>
          <span className="stat-value num">{formatMoney(summary.totalCostBasis)}</span>
        </div>
        <div className="stat-card">
          <span className="stat-label">Unrealized P/L</span>
          <span className={`stat-value num ${pnlClass(summary.totalUnrealizedPnl)}`}>
            {formatMoney(summary.totalUnrealizedPnl)}
          </span>
        </div>
        <div className="stat-card">
          <span className="stat-label">BTC dominance</span>
          <span className="stat-value num">{btcDom != null ? formatPercent(btcDom) : '—'}</span>
        </div>
      </div>

      {loading && <p className="dash-msg">Loading…</p>}
      {error && <p className="dash-msg error">{error}</p>}
      {!loading && !error && positions.length === 0 && (
        <p className="dash-msg">No positions. Add balances on the Holdings tab.</p>
      )}
      {!loading && !error && positions.length > 0 && visiblePositions.length === 0 && (
        <p className="dash-msg">No positions with meaningful quantity.</p>
      )}

      {!loading && !error && visiblePositions.length > 0 && (
        <div className="terminal-wrap">
          <table className="terminal-table">
            <thead>
              <tr>
                <th>Symbol</th>
                <th className="num">Quantity</th>
                <th className="num">Price</th>
                <th className="num">Value</th>
                <th className="num">Avg cost</th>
                <th className="num">Unreal. P/L</th>
                <th className="num">%</th>
                <th>Strategy</th>
              </tr>
            </thead>
            <tbody>
              {visiblePositions.map(p => (
                <tr key={p.symbol}>
                  <td className="cell-symbol">{p.symbol}</td>
                  <td className="num">{formatQuantity(p.quantity)}</td>
                  <td className="num mono">{formatOptionalMoney(p.currentPriceUsd)}</td>
                  <td className="num mono">{formatOptionalMoney(p.currentValueUsd)}</td>
                  <td className="num mono">{formatMoney(p.averagePricePerUnit)}</td>
                  <td className={`num mono ${pnlClass(p.unrealizedPnl)}`}>
                    {formatOptionalMoney(p.unrealizedPnl)}
                  </td>
                  <td className="num">{formatPercent(p.allocationPercent)}</td>
                  <td>
                    <span className={strategyTagClass(p.strategyStatus)}>
                      {formatStrategyLabel(p.strategyStatus)}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
