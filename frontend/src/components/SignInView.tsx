type Props = {
  authError: string | null
  username: string
  password: string
  busy: boolean
  onUsernameChange: (v: string) => void
  onPasswordChange: (v: string) => void
  onLogin: () => void
  onRegister: () => void
  theme: 'light' | 'dark'
  onToggleTheme: () => void
}

export function SignInView({
  authError,
  username,
  password,
  busy,
  onUsernameChange,
  onPasswordChange,
  onLogin,
  onRegister,
  theme,
  onToggleTheme,
}: Props) {
  return (
    <div className="auth-screen">
      <header className="auth-top">
        <span className="dash-brand">CryptoTracker</span>
        <button type="button" className="dash-icon-btn" onClick={onToggleTheme} aria-label="Toggle theme">
          {theme === 'dark' ? 'Light' : 'Dark'}
        </button>
      </header>
      <div className="auth-card">
        <h1>Sign in</h1>
        <p className="dash-hint">Each account has its own holdings and transactions.</p>
        {authError && <p className="dash-msg error">{authError}</p>}
        <label>
          <span className="field-label">Username</span>
          <input type="text" autoComplete="username" value={username} onChange={e => onUsernameChange(e.target.value)} />
        </label>
        <label>
          <span className="field-label">Password</span>
          <input type="password" autoComplete="current-password" value={password} onChange={e => onPasswordChange(e.target.value)} />
        </label>
        <div className="auth-actions">
          <button type="button" onClick={onLogin} disabled={busy}>{busy ? '…' : 'Log in'}</button>
          <button type="button" className="btn-ghost" onClick={onRegister} disabled={busy}>Register</button>
        </div>
      </div>
    </div>
  )
}
