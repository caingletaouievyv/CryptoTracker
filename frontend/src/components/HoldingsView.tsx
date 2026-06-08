import type { Dispatch, SetStateAction } from 'react'
import type { HoldingItem } from '../types'

type Props = {
  rows: HoldingItem[]
  setRows: Dispatch<SetStateAction<HoldingItem[]>>
  emptyRow: () => HoldingItem
  saving: boolean
  error: string | null
  onSave: () => void
}

export function HoldingsView({ rows, setRows, emptyRow, saving, error, onSave }: Props) {
  return (
    <div className="view-holdings">
      <p className="dash-hint">Source-of-truth balances. Save replaces all rows for your account.</p>

      <div className="terminal-wrap terminal-wrap--tall">
        <table className="terminal-table terminal-table--edit">
          <thead>
            <tr>
              <th>Symbol</th>
              <th className="num">Quantity</th>
              <th>Source</th>
              <th className="num">Sell $</th>
              <th className="num">Buy zone $</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((row, i) => (
              <tr key={i}>
                <td>
                  <input
                    className="cell-input"
                    aria-label={`Symbol row ${i + 1}`}
                    value={row.symbol}
                    onChange={e => setRows(r => r.map((x, j) => j === i ? { ...x, symbol: e.target.value } : x))}
                    placeholder="BTC"
                  />
                </td>
                <td>
                  <input
                    className="cell-input num"
                    type="number"
                    step="any"
                    aria-label={`Quantity row ${i + 1}`}
                    value={row.currentQuantity === 0 && !row.symbol.trim() ? '' : row.currentQuantity}
                    onChange={e => setRows(r => r.map((x, j) => j === i ? {
                      ...x,
                      currentQuantity: e.target.value === '' ? 0 : e.target.valueAsNumber,
                    } : x))}
                  />
                </td>
                <td>
                  <input
                    className="cell-input"
                    aria-label={`Source row ${i + 1}`}
                    value={row.source}
                    onChange={e => setRows(r => r.map((x, j) => j === i ? { ...x, source: e.target.value } : x))}
                    placeholder="OKX"
                  />
                </td>
                <td>
                  <input
                    className="cell-input num"
                    type="number"
                    step="any"
                    min={0}
                    aria-label={`Sell target row ${i + 1}`}
                    value={row.sellTargetUsd == null ? '' : row.sellTargetUsd}
                    onChange={e => setRows(r => r.map((x, j) => j === i ? {
                      ...x,
                      sellTargetUsd: e.target.value === '' ? undefined : (Number.isNaN(e.target.valueAsNumber) ? undefined : e.target.valueAsNumber),
                    } : x))}
                  />
                </td>
                <td>
                  <input
                    className="cell-input num"
                    type="number"
                    step="any"
                    min={0}
                    aria-label={`Buy zone row ${i + 1}`}
                    value={row.buyZoneUsd == null ? '' : row.buyZoneUsd}
                    onChange={e => setRows(r => r.map((x, j) => j === i ? {
                      ...x,
                      buyZoneUsd: e.target.value === '' ? undefined : (Number.isNaN(e.target.valueAsNumber) ? undefined : e.target.valueAsNumber),
                    } : x))}
                  />
                </td>
                <td>
                  <button
                    type="button"
                    className="btn-ghost"
                    onClick={() => setRows(r => r.filter((_, j) => j !== i))}
                    disabled={rows.length <= 1}
                  >
                    Remove
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="toolbar toolbar--end">
        <button type="button" className="btn-ghost" onClick={() => setRows(r => [...r, emptyRow()])}>Add row</button>
        <button type="button" onClick={onSave} disabled={saving}>{saving ? 'Saving…' : 'Save holdings'}</button>
      </div>
      {error && <p className="dash-msg error">{error}</p>}
    </div>
  )
}
