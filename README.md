# CryptoTracker

Personal crypto portfolio tracker for investors who maintain a **holdings snapshot** and **transaction ledger**. Quantities match exchange balances; cost basis and P/L derive from buy history and live spot prices.

---

## Demo

### URLs

| Environment | URL |
|-------------|-----|
| Frontend (production) | — |
| Backend (production) | — |
| Frontend (local) | http://localhost:5173 |
| API / Swagger (local) | http://localhost:5260/swagger |

Requires backend and frontend running ([Quick start](#quick-start)).

### Walkthrough

| Feature | Demo | Steps |
|---------|------|-------|
| Sign in / Register | — | 1. Open the application.<br>2. Enter username and password.<br>3. Select **Log in** or **Register**.<br>4. Sidebar shell loads; **Portfolio** is the default view. |
| Portfolio dashboard | — | 1. Sidebar → **Portfolio**.<br>2. Review stat row: total value, cost basis, unrealized P/L, BTC dominance.<br>3. Review holdings table: quantity, price, value, avg cost, P/L, allocation %, strategy tag. |
| Holdings snapshot | — | 1. Sidebar → **Holdings**.<br>2. Edit symbol, quantity, source; optional sell target and buy zone.<br>3. **Add row** if needed, then **Save holdings**.<br>4. **Portfolio** reflects updated quantities; cost/avg remains from buy history. |
| Add transaction | — | 1. Sidebar → **Transactions**.<br>2. Complete **Add transaction** (symbol, type, quantity, price, fee, date).<br>3. Select **Add**; ledger refreshes.<br>4. **Portfolio** cost basis updates from buys (price > 0). |
| Import trades | — | 1. Sidebar → **Transactions** → **Import trades**.<br>2. Expand **OKX** or **Binance**; enter read-only credentials → **Save keys** (browser storage only).<br>3. **Sync OKX** or **Sync Binance**.<br>4. Ledger updates; holdings unchanged until the snapshot is saved separately. |
| Theme toggle | — | 1. Top bar → theme control.<br>2. Dark or light mode applies.<br>3. Preference persists across reload (`localStorage`). |

Transaction ledger supports an optional filter: fees, transfers, and zero-price buys.

---

## Why this exists

| | |
|---|---|
| **Problem** | Exchange UIs scatter balances, cost basis, and trade history; manual spreadsheets drift out of date. |
| **Approach** | Two inputs — current holdings and transactions — with portfolio math on the server. |
| **Outcome** | One dashboard: value, cost basis, unrealized P/L, allocation, and optional strategy targets. |

---

## Features

### Core

- Per-user accounts (register / login, JWT)
- Transaction ledger (manual entry, paginated history)
- Holdings snapshot editor (replace-all save)
- Portfolio dashboard (quantity from holdings; average cost from buys with price > 0)
- Spot USD pricing (CoinGecko primary, CryptoCompare fallback)
- Price backfill for rows imported with price 0

### Exchange integration

- OKX SPOT trade import (read-only API keys, browser-stored)
- Binance Spot `myTrades` import (symbol list, configurable lookback)
- Seed scripts for bulk transaction and holdings load

### Productivity

- Noise filter on transactions (fees, transfers, zero-price buys)
- Portfolio display filters (excluded symbols, whitelist, min notional) via configuration
- Dark / light theme (persisted)
- Terminal-style UI: sidebar navigation, dense tables, summary stat row

---

## Quick start

### Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/) (LTS recommended)
- npm

### Backend

```bash
cd backend
dotnet run
```

API: http://localhost:5260 · Swagger: http://localhost:5260/swagger

### Frontend

```bash
cd frontend
npm install
cp .env.example .env
npm run dev
```

Default API URL: `http://localhost:5260` (override via `VITE_API_URL`).

UI: http://localhost:5173

### Seed data

Requires a JWT from login:

```bash
export CRYPTOTRACKER_TOKEN=<jwt>

node scripts/parse-export.js
node scripts/seed-api.js

node scripts/seed-holdings-api.js
```

Edit `scripts/seed-holdings.json` before running the holdings seed.

---

## Tech stack

| Layer | Technology |
|-------|------------|
| Frontend | React 18, TypeScript, Vite 6, CSS variables |
| Backend | ASP.NET Core 8, EF Core 8 |
| Database | SQLite (`backend/CryptoTracker.db`) |
| Auth | JWT bearer (HS256), BCrypt |
| Hosting | — |

---

## Project structure

```text
backend/     ASP.NET Core API, EF migrations, services
frontend/    React SPA (portfolio, holdings, transactions)
docs/        Product spec, architecture, UI guide (see Documentation)
scripts/     Seed and export helpers (Node.js)
```

---

## Documentation

| Document | Contents |
|----------|----------|
| [docs/docs.md](docs/docs.md) | Documentation index |
| [docs/project.md](docs/project.md) | Product scope, [API reference](docs/project.md#api-reference), domain rules, flows, tests |
| [docs/architecture.md](docs/architecture.md) | Engineering standard — layers, API envelope, auth |
| [docs/ui.md](docs/ui.md) | Frontend layout, theme, components, UX |

---

## Deployment

| Component | Target |
|-----------|--------|
| Frontend | — |
| Backend | — |
| Database | SQLite file; PostgreSQL or SQL Server for multi-instance production |

Production checklist:

- Set `Jwt:SigningKey` (minimum 32 characters) via environment or secrets manager
- Restrict CORS to the frontend origin
- Exclude `.env`, API keys, and `*.db` from version control

---

## Configuration

### Backend

Configuration file: `backend/appsettings.json` (overridable via environment).

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:DefaultConnection` | SQLite path |
| `Jwt:SigningKey` | Required; minimum 32 characters |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes` | Token validation and lifetime |
| `Cors:AllowedOrigins` | Allowed UI origins |
| `Portfolio:ExcludedSymbols`, `IncludeSymbolsOnly`, `MinNotionalUsd` | Portfolio row filters |
| `Binance:*` | Base URL, rate limits, history lookback |

Migrations apply on API startup. Database reset: stop API, delete `backend/CryptoTracker.db` (and `-shm`, `-wal` if present), restart.

### Frontend

| Variable | Purpose |
|----------|---------|
| `VITE_API_URL` | API base URL |

Exchange sync credentials (OKX, Binance) are stored in browser `localStorage` only.

### Scripts

| Variable | Purpose |
|----------|---------|
| `CRYPTOTRACKER_TOKEN` | JWT for seed scripts |

API endpoints: [docs/project.md#api-reference](docs/project.md#api-reference) · Swagger at `/swagger` when the API is running.
