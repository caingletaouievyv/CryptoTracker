import type { Dispatch, SetStateAction } from 'react'
import { syncFeedbackClass } from '../utils/format'

type OkxCreds = { apiKey: string; secretKey: string; passphrase: string }
type BinanceCreds = { apiKey: string; secretKey: string; symbols: string; historyLookbackDays: string }

type Props = {
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

export function ImportTradesPanel({
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
    <div className="panel-block">
      <h2 className="panel-heading">Import trades</h2>
      <p className="dash-hint">Adds SPOT trades to the ledger. Does not update holdings.</p>

      <details className="import-panel">
        <summary>OKX</summary>
        <div className="import-panel-body">
          <div className="inline-form-row">
            <label>
              <span className="field-label">API key</span>
              <input type="password" autoComplete="off" value={okxCreds.apiKey} onChange={e => setOkxCreds(c => ({ ...c, apiKey: e.target.value }))} />
            </label>
            <label>
              <span className="field-label">Secret</span>
              <input type="password" autoComplete="off" value={okxCreds.secretKey} onChange={e => setOkxCreds(c => ({ ...c, secretKey: e.target.value }))} />
            </label>
            <label>
              <span className="field-label">Passphrase</span>
              <input type="password" autoComplete="off" value={okxCreds.passphrase} onChange={e => setOkxCreds(c => ({ ...c, passphrase: e.target.value }))} />
            </label>
          </div>
          <div className="inline-form-row">
            <button type="button" onClick={saveOkxCreds}>{okxSaved ? 'Saved' : 'Save keys'}</button>
            <button type="button" onClick={runSyncOkx} disabled={syncing}>{syncing ? 'Syncing…' : 'Sync OKX'}</button>
          </div>
          {syncStatus && <p className={`dash-msg ${syncFeedbackClass(syncStatus)}`}>{syncStatus}</p>}
        </div>
      </details>

      <details className="import-panel">
        <summary>Binance</summary>
        <div className="import-panel-body">
          <div className="inline-form-row">
            <label>
              <span className="field-label">API key</span>
              <input type="password" autoComplete="off" value={binanceCreds.apiKey} onChange={e => setBinanceCreds(c => ({ ...c, apiKey: e.target.value }))} />
            </label>
            <label>
              <span className="field-label">Secret</span>
              <input type="password" autoComplete="off" value={binanceCreds.secretKey} onChange={e => setBinanceCreds(c => ({ ...c, secretKey: e.target.value }))} />
            </label>
            <label className="field-wide">
              <span className="field-label">Symbols</span>
              <input
                type="text"
                autoComplete="off"
                placeholder="BTCUSDT, ETHUSDT"
                value={binanceCreds.symbols}
                onChange={e => setBinanceCreds(c => ({ ...c, symbols: e.target.value }))}
              />
            </label>
            <label>
              <span className="field-label">Days</span>
              <input
                type="number"
                min={0}
                max={3650}
                placeholder="0"
                value={binanceCreds.historyLookbackDays}
                onChange={e => setBinanceCreds(c => ({ ...c, historyLookbackDays: e.target.value }))}
              />
            </label>
          </div>
          <div className="inline-form-row">
            <button type="button" onClick={saveBinanceCreds}>{binanceSaved ? 'Saved' : 'Save'}</button>
            <button type="button" onClick={runSyncBinance} disabled={binanceSyncing}>{binanceSyncing ? 'Syncing…' : 'Sync Binance'}</button>
          </div>
          {binanceSyncStatus && <p className={`dash-msg ${syncFeedbackClass(binanceSyncStatus)}`}>{binanceSyncStatus}</p>}
        </div>
      </details>
    </div>
  )
}
