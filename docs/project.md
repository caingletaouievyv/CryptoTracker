# Product specification

CryptoTracker is a personal crypto portfolio tracker built on two inputs: a **holdings snapshot** (current balances) and a **transaction ledger** (trade history). Portfolio quantity comes from the snapshot; cost basis and average price come from buy transactions. Live spot prices drive value and unrealized P/L.

**Related documentation:** [Architecture](architecture.md) · [UI guide](ui.md) · [Documentation index](docs.md) · [README](../README.md)

---

## Product scope

The application needs two maintained inputs:

1. **Transaction history** — manual entry, seed scripts, or exchange import (OKX, Binance).
2. **Current holdings** — snapshot edited in the UI or loaded via API/seed scripts.

The portfolio dashboard combines them: **quantity** from holdings (matches exchange balances); **cost basis and average price** from **Buy** rows with `priceAtTransaction > 0`.

### User flow

1. **Transactions:** Add or import history (date, type, symbol, quantity, price, fee). The ledger supports pagination and optional filtering of fee/transfer/noise rows.
2. **Holdings:** Enter current balances from the exchange (including Earn/Funding totals where applicable). Save replaces the full snapshot.
3. **Portfolio:** Review total value, cost basis, unrealized P/L, allocation, and optional strategy targets per holding.

Frontend patterns and screen layout: [docs/ui.md](ui.md). Engineering conventions: [docs/architecture.md](architecture.md).

---

## Capabilities

| Area | Status | Summary |
|------|--------|---------|
| Transaction ledger | Available | Manual add, paginated list, price backfill for rows with price 0 |
| Holdings snapshot | Available | Replace-all save; optional sell target and buy zone per symbol |
| Portfolio dashboard | Available | Spot USD pricing, unrealized P/L, allocation %, strategy status, summary rollups |
| Authentication | Available | Register/login, JWT bearer, per-user data isolation |
| Exchange import | Available | OKX SPOT bills; Binance Spot `myTrades` (read-only keys, browser-stored) |
| Theme | Available | Dark/light mode, persisted in browser storage |
| Automated tests | — | Manual checklist only (see [Testing](#testing)) |
| Refresh tokens / password reset | — | Not implemented |
| Matched-lot realized P/L | — | Realized metric is sell proceeds only |

---

## Repository layout

```text
CryptoTracker/
├── README.md                         Overview, quick start, configuration, deployment
├── docs/
│   ├── docs.md                       Documentation index
│   ├── architecture.md               Engineering standard: layers, API envelope, auth
│   ├── project.md                    This file — product scope, API, domain, flows
│   └── ui.md                         Frontend layout, theme, components, UX
├── backend/                          ASP.NET Core 8 API
│   ├── Controllers/
│   │   ├── AuthController.cs         POST /api/auth/register, /login
│   │   ├── TransactionController.cs  POST/GET /api/transaction, backfill-prices
│   │   ├── PortfolioController.cs    GET /api/portfolio
│   │   ├── HoldingsController.cs     GET/POST /api/holdings
│   │   └── SyncController.cs         POST /api/sync/okx|binance/transactions
│   ├── Services/
│   │   ├── TransactionService.cs     Add, paged list, backfill, bulk add (per user)
│   │   ├── AuthService.cs            Register, login; BCrypt password hash
│   │   ├── JwtTokenService.cs        JWT access token (HS256)
│   │   ├── CurrentUser.cs            ICurrentUser from HttpContext claims
│   │   ├── PriceService.cs           CoinGecko (primary), CryptoCompare (fallback)
│   │   ├── HoldingService.cs         Portfolio math; holdings snapshot (per user)
│   │   ├── OkxService.cs               OKX v5 bills (SPOT trade)
│   │   ├── OkxSyncService.cs         OKX → transaction requests
│   │   ├── BinanceService.cs         Binance Spot myTrades + exchangeInfo
│   │   └── BinanceSyncService.cs     Binance → transaction requests
│   ├── Interfaces/                   Service contracts
│   ├── Data/
│   │   └── AppDbContext.cs           DbSet<User>, DbSet<Transaction>, DbSet<Holding>
│   ├── Models/
│   │   ├── User.cs                   Id, Username, PasswordHash, CreatedUtc
│   │   ├── Transaction.cs            UserId FK + trade fields
│   │   └── Holding.cs                UserId + Symbol (composite PK), quantity, targets
│   ├── DTOs/                         Request/response types; ApiResponse envelope
│   ├── Middleware/
│   │   └── ExceptionHandlingMiddleware.cs   Global exception → JSON envelope
│   ├── Exceptions/                   HTTP status mapping for upstream failures
│   ├── Migrations/                   EF Core schema migrations
│   ├── Properties/
│   │   └── launchSettings.json       Dev ports and launch profile
│   ├── Program.cs                    DI, CORS, JWT, pipeline
│   ├── appsettings.json              Connection string, JWT, CORS, portfolio filters
│   ├── appsettings.Development.json  Development overrides
│   └── CryptoTracker.csproj
├── frontend/                         React 18 SPA (Vite, port 5173)
│   ├── src/
│   │   ├── App.tsx                   Auth gate, state, handlers; composes shell + views
│   │   ├── main.tsx                  React mount
│   │   ├── components/               AppShell, PortfolioView, HoldingsView, TransactionsView, …
│   │   ├── utils/                    format, strategy, portfolio helpers
│   │   ├── services/
│   │   │   └── api.ts                HTTP client; unwraps API envelope
│   │   ├── types.ts                  DTO-aligned TypeScript types
│   │   └── style.css                 Theme tokens, dash shell, terminal tables
│   ├── public/                       Static assets
│   ├── index.html                    HTML shell
│   ├── package.json
│   ├── tsconfig.json
│   ├── vite.config.ts
│   └── .env.example                  VITE_API_URL
└── scripts/
    ├── seed-api.js                   POST seed-transactions.json (requires JWT)
    ├── seed-transactions.json        Generated transaction seed data
    ├── parse-export.js               Build seed-transactions.json from export paste
    ├── export-paste.txt              Source paste for parse-export.js
    ├── seed-holdings.json            Holdings snapshot for seed script
    └── seed-holdings-api.js          POST holdings to /api/holdings
```

Build output directories (`bin/`, `obj/`, `node_modules/`, `frontend/dist/`) are generated locally and excluded from version control.

---

## Domain model

### Transaction

| Property | Type | Notes |
| -------- | ---- | ----- |
| UserId | Guid | FK → User (tenant scope) |
| Id | Guid | Primary key |
| Symbol | string | e.g. BTC |
| Type | string | Buy, Swap, Sell, Fee, Deposit, Withdraw |
| Quantity | decimal | Precision 28,18; may be negative (Fee, Withdraw) |
| PriceAtTransaction | decimal | Precision 28,18 |
| Fee | decimal | Positive = fee-out; negative = fee-in |
| Date | DateTime | Transaction timestamp |
| BaseCurrency | string | e.g. USD |
| Notes | string? | Optional; max 200 characters |

### User

| Property | Type | Notes |
| -------- | ---- | ----- |
| Id | Guid | Primary key |
| Username | string | Unique, normalized lowercase; 3–32 characters |
| PasswordHash | string | BCrypt |
| CreatedUtc | DateTime | Registration time |

### Holding

| Property | Type | Notes |
| -------- | ---- | ----- |
| UserId | Guid | FK → User; composite PK with Symbol |
| Symbol | string | Max 50 characters |
| CurrentQuantity | decimal | Snapshot quantity |
| Source | string | e.g. Earn, Spot; max 50 characters |
| LastUpdated | DateTime | Set on replace |
| SellTargetUsd | decimal? | Spot ≥ target → API status **READY TO SELL** |
| BuyZoneUsd | decimal? | Spot ≤ zone → API status **ACCUMULATION ZONE** |

---

## Services

### TransactionService

| Method | Returns | Responsibility |
| ------ | ------- | -------------- |
| AddTransactionAsync | TransactionResponse | Validate, persist; fetch USD price when price is 0 and type is Buy/Sell/Swap |
| GetTransactionsPageAsync | PagedListDto | Filter by user; order by date descending |
| BackfillPricesAsync | int | Update rows with price 0 (Buy/Sell/Swap); return count updated |
| AddTransactionsAsync | int | Bulk add without per-row price fetch (exchange sync) |

### HoldingService

| Method | Returns | Responsibility |
| ------ | ------- | -------------- |
| GetHoldingsAsync | IReadOnlyList | List snapshot for current user |
| GetPortfolioAsync | PortfolioDashboardResponse | Quantity from holdings; cost/avg from buys; spot, P/L, allocation, strategy; summary rollups |
| SetHoldingsAsync | Task | Replace all holdings for current user |

### Controllers

| Controller | Endpoint | Action |
| ---------- | -------- | ------ |
| AuthController | POST /api/auth/register, /login | Create user or validate credentials; return JWT |
| TransactionController | POST/GET /api/transaction | Add, list (Bearer; per-user) |
| TransactionController | POST /api/transaction/backfill-prices | Backfill zero prices (Bearer) |
| PortfolioController | GET /api/portfolio | Dashboard (Bearer) |
| HoldingsController | GET/POST /api/holdings | Read or replace snapshot (Bearer) |
| SyncController | POST /api/sync/okx/transactions | OKX SPOT bills (Bearer) |
| SyncController | POST /api/sync/binance/transactions | Binance Spot myTrades (Bearer) |

---

## API reference

All JSON responses use the envelope defined in [docs/architecture.md](architecture.md): `{ "success", "message", "data" }`.

**Authentication:** Register or log in, then send `Authorization: Bearer <accessToken>` on all routes below except `POST /api/auth/*`. In Swagger, use **Authorize** with value `Bearer <accessToken>`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Body `{ "username", "password" }` (password ≥ 8). `data` = `{ accessToken, expiresAtUtc, username }`. |
| POST | `/api/auth/login` | Body `{ "username", "password" }`. Same `data` shape as register. |
| POST | `/api/transaction` | **Bearer required.** Add transaction. `data` = transaction. **201** on success. |
| GET | `/api/transaction` | **Bearer required.** Query `page` (default 1), `pageSize` (default 10, max 500). `data` = paginated list. |
| POST | `/api/transaction/backfill-prices` | **Bearer required.** Backfill zero prices. `data` = `{ updated }`. |
| GET | `/api/portfolio` | **Bearer required.** `data` = `{ positions, summary }`. |
| GET | `/api/holdings` | **Bearer required.** `data` = holdings array. |
| POST | `/api/holdings` | **Bearer required.** Replace snapshot. Body `{ "holdings": [ … ] }`. **200** on success. |
| POST | `/api/sync/okx/transactions` | **Bearer required.** Sync SPOT Buy/Sell from OKX. Optional body credentials or appsettings. `data` = `{ synced, updated, message? }`. **502** if OKX unreachable. |
| POST | `/api/sync/binance/transactions` | **Bearer required.** Sync Spot myTrades. Body or appsettings for keys and symbols. **502** if Binance unreachable. |

**POST /api/transaction body:** `symbol`, `type` (Buy, Sell, Swap, Fee, Deposit, Withdraw), `quantity`, `priceAtTransaction`, `fee`, `date`, `baseCurrency`, optional `notes`.

**POST /api/holdings body:** `{ "holdings": [ { "symbol", "currentQuantity", "source", optional "sellTargetUsd", optional "buyZoneUsd" } ] }`.

Interactive reference: Swagger at `/swagger` when the API is running locally.

---

## Data and computation rules

### Per-transaction

- **Net value** = `quantity × priceAtTransaction + fee` (included in transaction responses).
- **Price when 0:** On add or backfill, if `priceAtTransaction == 0` and type is Buy/Sell/Swap, the backend fetches USD price from CoinGecko (primary) or CryptoCompare (fallback). Stablecoins resolve to 1.

### Portfolio (two inputs)

- **Quantity:** From the **holdings snapshot** only. Not derived by summing the ledger. Update via the Holdings view, `POST /api/holdings`, or seed scripts.
- **Cost basis / average price:** From **Buy** rows with `priceAtTransaction > 0` only. Average = total buy cost ÷ total buy quantity. Cost basis = average × holding quantity. Sells and swaps do not adjust average cost.
- **Spot price (today UTC):** CoinGecko batch pricing with CryptoCompare fallback. Empty price cells mean the provider could not resolve the ticker.
- **Unrealized P/L:** `(spotUsd − avgPrice) × quantity` when average and spot are available.
- **Realized (summary):** Sum of sell proceeds (`quantity × price + fee` for Sell rows with price > 0). Not matched-lot P/L.
- **Strategy status (API):** `READY TO SELL` when spot ≥ sell target; `ACCUMULATION ZONE` when spot ≤ buy zone; otherwise `WAITING`. The UI maps these to compact labels (see [docs/ui.md](ui.md)).

### Display filters

- **Backend:** `Portfolio:ExcludedSymbols`, `IncludeSymbolsOnly`, `MinNotionalUsd` in appsettings.
- **Frontend:** Hides fee/deposit/withdraw and zero-price buys by default; optional checkbox restores them. Dust quantities below `1e-10` are hidden client-side.

### Exchange workflows

**Holdings (manual snapshot):**

1. Copy balances from the exchange Assets view (including Earn where applicable).
2. Enter in the Holdings view or POST to `/api/holdings`. No API pull for Funding/Earn totals.

**Transactions (optional import):**

1. **OKX:** Transactions → Import trades → OKX. Read-only API key with Trading read permission. SPOT trade bills only (~3 months). Keys stored in browser `localStorage` only.
2. **Binance:** Import trades → Binance. Read-only key, secret, and Spot symbol list. Scans up to `Binance:HistoryLookbackMaxDays` (default 3650). Commission in **Fee** when paid in quote.

**Important:** Sync updates the transaction ledger only. Portfolio quantity changes only when holdings are saved separately.

---

## Export and portfolio caveats

1. **Fee rows** in exports often represent internal transfers (e.g. Simple Earn). They remain in the ledger; portfolio quantity still comes from the holdings snapshot.
2. **PriceAtTransaction = 0** on existing rows: run `POST /api/transaction/backfill-prices` after import.
3. **Deposit / Withdraw** pairs are account transfers; excluded from portfolio math.
4. **Quantity from snapshot** — sells in the ledger do not auto-reduce displayed quantity; keep the snapshot aligned with exchange balances.

---

## Request flows

### Backend

| Action | Path |
| ------ | ---- |
| Register / login | POST → AuthController → AuthService + JwtTokenService → Users + JWT |
| Add transaction | POST + Bearer → TransactionController → TransactionService → AppDbContext |
| List transactions | GET + Bearer → TransactionController → TransactionService (user filter, paginated) |
| Portfolio | GET + Bearer → PortfolioController → HoldingService → price providers |
| Save holdings | POST + Bearer → HoldingsController → HoldingService.SetHoldingsAsync |
| OKX / Binance sync | POST + Bearer → SyncController → sync service → TransactionService bulk add |

### Frontend

| Action | Path |
| ------ | ---- |
| Page load (signed in) | App.tsx → api.getTransactions + getPortfolio + getHoldings |
| Add transaction | TransactionsView → api.addTransaction → reload |
| Save holdings | HoldingsView → api.setHoldings → reload |
| Import trades | TransactionsView → ImportTradesPanel → api sync endpoints → reload |
| Theme toggle | App state + localStorage + `data-theme` on document (no backend) |

**Trace summary:** Form or view → `services/api.ts` → controller → service → AppDbContext → SQLite.

---

## Testing

Manual verification checklist. An automated test suite is not included in the current release.

### API

1. Run backend: `cd backend` then `dotnet run`.
2. Open Swagger: `http://localhost:5260/swagger`.
3. **POST /api/transaction** — valid body returns **201** with `success: true` and transaction in `data`.
4. **GET /api/transaction** — **200** with paginated `data.items`.
5. Invalid body (e.g. quantity 0) returns **400** with `success: false`.

### UI

1. Run backend and frontend (`npm run dev` in `frontend/`). UI: `http://localhost:5173`.
2. Register or log in; add a transaction; confirm ledger and portfolio update.
3. Refresh the page; data persists via API.
4. Stop the backend and submit a form; confirm an error message appears.

### Portfolio and theme

1. Set holdings and add buy transactions (price > 0). Portfolio shows quantity, average, and cost basis.
2. Toggle light/dark theme; refresh; preference persists.
3. With holdings and buys in place, summary cards show value, cost basis, unrealized P/L, and realized sell proceeds when applicable.

---

## Current state

| Component | State |
|-----------|-------|
| Backend API | Transaction CRUD, holdings, portfolio dashboard, auth, OKX/Binance sync |
| Frontend | Sidebar shell, portfolio/holdings/transactions views, theme, exchange import UI |
| Database | SQLite; EF migrations apply on startup |
| Auth | JWT per user; register/login; all data endpoints require Bearer token |
| Pricing | CoinGecko + CryptoCompare; backfill for zero-price rows |
| Seeds | Documented in [README](../README.md#seed-data) |
| Deployment targets | — (see [README](../README.md#deployment)) |

**Known limitations:** No transaction update/delete endpoints. No refresh tokens or password reset. No live ticker stream; spot uses daily price APIs. Realized P/L is sell proceeds, not matched-lot accounting.

**Database reset:** Stop the API, delete `backend/CryptoTracker.db` (and `-shm`, `-wal` if present), restart. Configuration details: [README](../README.md#configuration).
