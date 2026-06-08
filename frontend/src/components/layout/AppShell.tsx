import type { ReactNode } from 'react'
import { LayoutDashboard, Wallet, ArrowLeftRight, LogOut, Moon, Sun } from 'lucide-react'

export type AppTab = 'portfolio' | 'transactions' | 'holdings'

type Props = {
  activeTab: AppTab
  onTabChange: (tab: AppTab) => void
  username: string
  onLogout: () => void
  theme: 'light' | 'dark'
  onToggleTheme: () => void
  pageTitle: string
  children: ReactNode
}

const NAV: { id: AppTab; label: string; icon: typeof LayoutDashboard }[] = [
  { id: 'portfolio', label: 'Portfolio', icon: LayoutDashboard },
  { id: 'holdings', label: 'Holdings', icon: Wallet },
  { id: 'transactions', label: 'Transactions', icon: ArrowLeftRight },
]

export function AppShell({
  activeTab,
  onTabChange,
  username,
  onLogout,
  theme,
  onToggleTheme,
  pageTitle,
  children,
}: Props) {
  return (
    <div className="dash-shell">
      <aside className="dash-sidebar" aria-label="Main navigation">
        <div className="dash-brand">CryptoTracker</div>
        <nav className="dash-nav">
          {NAV.map(({ id, label, icon: Icon }) => (
            <button
              key={id}
              type="button"
              className={activeTab === id ? 'dash-nav-item dash-nav-item--active' : 'dash-nav-item'}
              aria-current={activeTab === id ? 'page' : undefined}
              onClick={() => onTabChange(id)}
            >
              <Icon size={16} strokeWidth={1.75} aria-hidden />
              <span>{label}</span>
            </button>
          ))}
        </nav>
        <div className="dash-sidebar-foot">
          <span className="dash-user">{username}</span>
          <button type="button" className="dash-icon-btn" onClick={onLogout} title="Sign out">
            <LogOut size={16} strokeWidth={1.75} />
          </button>
        </div>
      </aside>

      <div className="dash-main">
        <header className="dash-topbar">
          <h1 className="dash-page-title">{pageTitle}</h1>
          <div className="dash-topbar-actions">
            <button
              type="button"
              className="dash-icon-btn"
              onClick={onToggleTheme}
              aria-label="Toggle theme"
              title={theme === 'dark' ? 'Light mode' : 'Dark mode'}
            >
              {theme === 'dark' ? <Sun size={16} strokeWidth={1.75} /> : <Moon size={16} strokeWidth={1.75} />}
            </button>
          </div>
        </header>
        <div className="dash-content">{children}</div>
      </div>
    </div>
  )
}
