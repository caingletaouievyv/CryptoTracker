# Frontend UI guide

Layout, theme, components, and UX patterns for the CryptoTracker React application (`frontend/`).

**Related documentation:** [Architecture](architecture.md) · [Product specification](project.md) · [Documentation index](docs.md) · [README](../README.md)

---

## Overview

| Item | Value |
|------|--------|
| **Purpose** | Portfolio dashboard from holdings snapshot + transaction history |
| **Stack** | React 18, TypeScript, Vite 6; ES modules; sidebar navigation (no URL router); no global state library |
| **Design** | Terminal-style dense tables; dark/light theme; neutral chrome; green/red for P/L only; exchange keys in browser storage only |

---

## Folder structure

```text
frontend/
├── index.html              Root shell; inline theme bootstrap from localStorage
├── package.json            Scripts: dev, build, preview
├── vite.config.ts          Dev server :5173; API via VITE_API_URL
└── src/
    ├── main.tsx            StrictMode; mounts App; imports style.css
    ├── App.tsx             Auth gate, data load, handlers; composes shell + views
    ├── types.ts            Transaction, portfolio, holdings (camelCase, API-aligned)
    ├── style.css           Theme tokens, dash shell, stat cards, terminal tables
    ├── vite-env.d.ts       import.meta.env (VITE_API_URL)
    ├── components/
    │   ├── layout/AppShell.tsx   Fixed sidebar + top bar
    │   ├── PortfolioView.tsx     Summary stats + holdings table
    │   ├── HoldingsView.tsx      Editable holdings table
    │   ├── TransactionsView.tsx  Ledger, add form, import panel
    │   ├── ImportTradesPanel.tsx OKX / Binance credential panels
    │   └── SignInView.tsx        Full-screen auth (no sidebar)
    ├── utils/
    │   ├── format.ts       formatMoney, formatQuantity, pnlClass
    │   ├── strategy.ts     API status → display label and CSS class
    │   └── portfolio.ts    filterVisiblePositions, btcDominancePercent
    └── services/
        └── api.ts          fetch helpers; unwraps API envelope
```

| Path | Role |
|------|------|
| `src/services/api.ts` | Single API surface; base URL from `VITE_API_URL` or `http://localhost:5260` |
| `src/style.css` | CSS variables; `.dash-shell`, `.terminal-table`, `.stat-row`, strategy tags |
| `src/App.tsx` | Tab state (`portfolio` \| `holdings` \| `transactions`), auth gate, mutation handlers |

---

## Theme and styling

### CSS variables

| Token | Role |
|-------|------|
| `--bg` | Page background |
| `--text` | Primary text |
| `--muted` | Hints, table headers, secondary copy |
| `--border` | Borders |
| `--input-bg` | Inputs and metric tiles |
| `--card-bg` | Stat cards, table surfaces |
| `--sidebar-bg` | Fixed left navigation |
| `--hover` | Row and nav hover |
| `--error` | Errors, negative P/L |
| `--success` | Success copy, positive P/L |
| `--focus-ring` | Focus outline |

**Modes:** `:root` and `[data-theme="dark"]` use dark tokens; `[data-theme="light"]` overrides. `index.html` sets `data-theme` from `localStorage` or `prefers-color-scheme` before paint. The theme toggle in `App` syncs `document.documentElement` and the `theme` localStorage key.

### Typography and layout

- Body: `system-ui`, 13px base.
- Page title: `.dash-page-title` in the top bar.
- Numbers: `.num` (tabular, right-aligned); `.mono` for prices.
- Shell: fixed sidebar (`--sidebar-w`) + `.dash-main` content area.
- Portfolio: `.stat-row` (four stat cards) + `.terminal-table` (sticky header, zebra rows).
- Tables scroll inside `.terminal-wrap` with viewport-based max height.

### Responsive behavior

| Breakpoint | Behavior |
|------------|----------|
| `< 640px` | Sidebar collapses to icon-only |
| All widths | Tables scroll horizontally/vertically inside `.terminal-wrap` |

---

## Views and navigation

Signed-in users see sidebar navigation with tabs: **Portfolio** (default), **Holdings**, **Transactions**. Tab state is React state only (no client-side router).

| View | API | Content |
|------|-----|---------|
| **Portfolio** | GET `/api/portfolio` | Four stat cards (value, cost, unrealized P/L, BTC dominance); table with symbol, qty, price, value, avg cost, P/L, allocation %, strategy tag |
| **Holdings** | GET/POST `/api/holdings` | Inline-editable snapshot; save replaces all rows; sell target and buy zone columns |
| **Transactions** | GET/POST `/api/transaction` | Paginated ledger, add form, import trades panel (OKX / Binance) |

**Signed out:** `SignInView` full screen (register or log in). JWT and username stored in `localStorage`; `api.ts` sends `Authorization: Bearer` on protected calls.

**Shell:** Sidebar brand, nav, user label, sign out; top bar with page title and theme toggle.

### Strategy labels

The API returns `WAITING`, `ACCUMULATION ZONE`, or `READY TO SELL`. The UI displays compact labels:

| API status | UI label |
|------------|----------|
| `WAITING` | WAITING |
| `ACCUMULATION ZONE` | ACCUMULATE |
| `READY TO SELL` | TAKE PROFIT |

Mapping: `src/utils/strategy.ts`.

---

## UX patterns

| Pattern | Implementation |
|---------|----------------|
| **Loading** | `.dash-msg` on Portfolio and Transactions; buttons show Saving…, Syncing…, Adding… while in flight |
| **Errors** | `.dash-msg.error` on load, submit, holdings save, auth, and sync |
| **Empty states** | `.dash-msg` when no data, filtered ledger empty, or dust-only portfolio |
| **Forms** | Labels and inputs; HTML `required` where applicable; client numeric checks before API calls |
| **Focus** | `focus-visible` on nav, buttons, inputs |
| **Theme** | Toggle persists; pre-hydration script prevents flash |

---

## State and data handling

| Concern | Approach |
|---------|----------|
| **Application state** | `App` holds transactions, portfolio, holdings editor, theme, exchange credentials, active tab |
| **Hooks** | `useState`, `useEffect`, `useMemo` only |
| **API layer** | `api.ts` unwraps `{ success, message, data }`; throws `Error(message)` for UI catch blocks |
| **Refresh** | `load()` fetches in parallel after mutations |
| **localStorage keys** | `theme`, `cryptotracker_jwt`, `cryptotracker_username`, `cryptotracker_okx`, `cryptotracker_binance` |

### Client-side behavior

| Feature | Location |
|---------|----------|
| Transaction noise filter | `isNoiseTransaction` in `App.tsx` + checkbox on Transactions |
| Pagination | Server-side; client requests `pageSize` 100 in `getTransactions()` |
| Portfolio dust filter | Hides rows with `\|quantity\| < 1e-10` |
| Exchange import | Collapsible `<details>` panels; credentials never sent except to sync endpoints |

---

## Accessibility

- `aria-label` on sidebar navigation.
- `aria-current="page"` on active nav item.
- Portfolio summary region uses `role="region"`.
