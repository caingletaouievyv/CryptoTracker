import type { ChangeEvent, Dispatch, SetStateAction } from 'react'
import { syncFeedbackClass } from '../utils/format'
import type { CreateTransactionRequest } from '../types'

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
  csvPreview: CreateTransactionRequest[] | null
  csvParseError: string | null
  csvSkipped: number
  csvFormat: string | null
  onCsvFileChange: (e: ChangeEvent<HTMLInputElement>) => void
  csvImporting: boolean
  csvStatus: string | null
  runCsvImport: () => void
  onCsvClear: () => void
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
  csvPreview,
  csvParseError,
  csvSkipped,
  csvFormat,
  onCsvFileChange,
  csvImporting,
  csvStatus,
  runCsvImport,
  onCsvClear,
}: Props) {
  return (
    <div className="panel-block">
      <h2 className="panel-heading">Import trades</h2>
      <p className="dash-hint">Adds rows to the transaction ledger. Does not update holdings.</p>

      <details className="import-panel" open>
        <summary>CSV file</summary>
        <div className="import-panel-body">
          <p className="dash-hint">
            Upload a <strong>.csv</strong> (Excel: Save As → CSV). OKX <strong>Trading History</strong> export: metadata row (UID) is skipped;
            Spot rows import from <code>Balance Change</code> + <code>Balance Unit</code> (Convert/trades; Action may be empty). Transfer, Swap, and Funding rows are skipped. Or use template:{' '}
            <code>date, symbol, type, quantity, price, fee</code>.
          </p>
          <div className="inline-form-row">
            <label className="field-wide">
              <span className="field-label">File</span>
              <input type="file" accept=".csv,text/csv" onChange={onCsvFileChange} />
            </label>
          </div>
          {csvParseError && <p className="dash-msg error">{csvParseError}</p>}
          {csvPreview && (
            <p className="dash-hint">
              {csvPreview.length} row(s) ready
              {csvFormat ? ` (${csvFormat} format)` : ''}
              {csvSkipped > 0 ? ` · ${csvSkipped} skipped` : ''}.
            </p>
          )}
          <div className="inline-form-row">
            <button type="button" onClick={runCsvImport} disabled={csvImporting || !csvPreview?.length}>
              {csvImporting ? 'Importing…' : 'Import CSV'}
            </button>
            {csvPreview && (
              <button type="button" onClick={onCsvClear} disabled={csvImporting}>
                Clear
              </button>
            )}
          </div>
          {csvStatus && <p className={`dash-msg ${syncFeedbackClass(csvStatus)}`}>{csvStatus}</p>}
        </div>
      </details>

      <details className="import-panel">
        <summary>OKX</summary>
        <div className="import-panel-body">
          <p className="dash-hint">
            Recent SPOT trades, Convert, and Simple trade (~3 months). Not Transfer/Funding. For older history use{' '}
            <strong>CSV file</strong> above. Keys:{' '}
            <a href="https://www.okx.com/account/my-api" target="_blank" rel="noreferrer">
              OKX API management
            </a>
            .
          </p>
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
          <p className="dash-hint">
            Spot <code>myTrades</code> per symbol. Set <strong>Days</strong> to <strong>0</strong> for max lookback (~10 years). Keys:{' '}
            <a href="https://www.binance.com/en/my/settings/api-management" target="_blank" rel="noreferrer">
              Binance API management
            </a>
            .
          </p>
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
