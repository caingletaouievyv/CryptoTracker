import type { Dispatch, FormEvent, SetStateAction } from 'react'
import type { Transaction, CreateTransactionRequest } from '../types'
import { formatDate, formatMoney, formatQuantity } from '../utils/format'
import { ImportTradesPanel } from './ImportTradesPanel'

type OkxCreds = { apiKey: string; secretKey: string; passphrase: string }
type BinanceCreds = { apiKey: string; secretKey: string; symbols: string; historyLookbackDays: string }

type Props = {
  loading: boolean
  error: string | null
  transactions: Transaction[]
  showNoise: boolean
  onShowNoiseChange: (v: boolean) => void
  form: CreateTransactionRequest
  setForm: Dispatch<SetStateAction<CreateTransactionRequest>>
  submitting: boolean
  submitError: string | null
  onSubmit: (e: FormEvent) => void
  okxCreds: OkxCreds
  setOkxCreds: Dispatch<SetStateAction<OkxCreds>>
  okxSaved: boolean
  saveOkxCreds: () => void
  syncing: boolean
  syncStatus: string | null
  runSyncOkx: () => void
  binanceCreds: BinanceCreds
  setBinanceCreds: Dispatch<SetStateAction<BinanceCreds>>
  binanceSaved: boolean
  saveBinanceCreds: () => void
  binanceSyncing: boolean
  binanceSyncStatus: string | null
  runSyncBinance: () => void
}

function txValue(t: Transaction): number {
  return Math.abs(t.quantity) * t.priceAtTransaction
}

export function TransactionsView({
  loading,
  error,
  transactions,
  showNoise,
  onShowNoiseChange,
  form,
  setForm,
  submitting,
  submitError,
  onSubmit,
  okxCreds,
  setOkxCreds,
  okxSaved,
  saveOkxCreds,
  syncing,
  syncStatus,
  runSyncOkx,
  binanceCreds,
  setBinanceCreds,
  binanceSaved,
  saveBinanceCreds,
  binanceSyncing,
  binanceSyncStatus,
  runSyncBinance,
}: Props) {
  return (
    <div className="view-transactions">
      <div className="toolbar">
        <label className="toolbar-check">
          <input type="checkbox" checked={showNoise} onChange={e => onShowNoiseChange(e.target.checked)} />
          Show fees, transfers, zero-price buys
        </label>
      </div>

      {loading && <p className="dash-msg">Loading…</p>}
      {error && <p className="dash-msg error">{error}</p>}
      {!loading && !error && transactions.length === 0 && (
        <p className="dash-msg">No transactions. Add below or import from an exchange.</p>
      )}

      {!loading && !error && transactions.length > 0 && (
        <div className="terminal-wrap terminal-wrap--tall">
          <table className="terminal-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Type</th>
                <th>Symbol</th>
                <th className="num">Qty</th>
                <th className="num">Price</th>
                <th className="num">Value</th>
              </tr>
            </thead>
            <tbody>
              {transactions.map(t => (
                <tr key={t.id}>
                  <td>{formatDate(t.date)}</td>
                  <td><span className="tx-type">{t.type}</span></td>
                  <td className="cell-symbol">{t.symbol}</td>
                  <td className="num">{formatQuantity(t.quantity)}</td>
                  <td className="num mono">{formatMoney(t.priceAtTransaction)}</td>
                  <td className="num mono">{formatMoney(txValue(t))}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div className="panel-block">
        <h2 className="panel-heading">Add transaction</h2>
        <form className="inline-form" onSubmit={onSubmit}>
          <div className="inline-form-row">
            <label>
              <span className="field-label">Symbol</span>
              <input value={form.symbol} onChange={e => setForm(f => ({ ...f, symbol: e.target.value }))} required />
            </label>
            <label>
              <span className="field-label">Type</span>
              <select value={form.type} onChange={e => setForm(f => ({ ...f, type: e.target.value }))}>
                <option value="Buy">Buy</option>
                <option value="Sell">Sell</option>
                <option value="Swap">Swap</option>
                <option value="Fee">Fee</option>
                <option value="Deposit">Deposit</option>
                <option value="Withdraw">Withdraw</option>
              </select>
            </label>
            <label>
              <span className="field-label">Qty</span>
              <input
                type="number"
                step="any"
                value={form.quantity ?? ''}
                onChange={e => setForm(f => ({ ...f, quantity: e.target.valueAsNumber ?? 0 }))}
                required
              />
            </label>
            <label>
              <span className="field-label">Price</span>
              <input
                type="number"
                step="any"
                min="0"
                value={form.priceAtTransaction || ''}
                onChange={e => setForm(f => ({ ...f, priceAtTransaction: e.target.valueAsNumber || 0 }))}
                required
              />
            </label>
            <label>
              <span className="field-label">Fee</span>
              <input
                type="number"
                step="any"
                value={form.fee ?? ''}
                onChange={e => setForm(f => ({ ...f, fee: e.target.valueAsNumber ?? 0 }))}
              />
            </label>
            <label>
              <span className="field-label">Date</span>
              <input type="date" value={form.date} onChange={e => setForm(f => ({ ...f, date: e.target.value }))} required />
            </label>
            <label className="inline-form-action">
              <span className="field-label">&nbsp;</span>
              <button type="submit" disabled={submitting}>{submitting ? 'Adding…' : 'Add'}</button>
            </label>
          </div>
          {submitError && <p className="dash-msg error">{submitError}</p>}
        </form>
      </div>

      <ImportTradesPanel
        okxCreds={okxCreds}
        setOkxCreds={setOkxCreds}
        okxSaved={okxSaved}
        saveOkxCreds={saveOkxCreds}
        syncing={syncing}
        syncStatus={syncStatus}
        runSyncOkx={runSyncOkx}
        binanceCreds={binanceCreds}
        setBinanceCreds={setBinanceCreds}
        binanceSaved={binanceSaved}
        saveBinanceCreds={saveBinanceCreds}
        binanceSyncing={binanceSyncing}
        binanceSyncStatus={binanceSyncStatus}
        runSyncBinance={runSyncBinance}
      />
    </div>
  )
}
