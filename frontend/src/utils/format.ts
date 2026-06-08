export const MIN_DISPLAY_QUANTITY = 1e-10

export function formatDate(s: string): string {
  return new Date(s).toLocaleDateString(undefined, { dateStyle: 'short' })
}

export function formatQuantity(n: number): string {
  const x = Number(n)
  if (Number.isNaN(x)) return '—'
  if (Math.abs(x) < MIN_DISPLAY_QUANTITY) return '0'
  return x.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 8 })
}

export function formatMoney(n: number): string {
  const x = Number(n)
  if (Number.isNaN(x)) return '—'
  return x.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

export function formatOptionalMoney(n: number | null | undefined): string {
  if (n == null || Number.isNaN(Number(n))) return '—'
  return formatMoney(n)
}

export function formatPercent(n: number | null | undefined): string {
  if (n == null || Number.isNaN(Number(n))) return '—'
  return `${Number(n).toLocaleString(undefined, { maximumFractionDigits: 2 })}%`
}

export function pnlClass(n: number | null | undefined): string {
  if (n == null || Number.isNaN(Number(n))) return ''
  if (n > 0) return 'pnl-pos'
  if (n < 0) return 'pnl-neg'
  return ''
}

export function syncFeedbackClass(message: string): string {
  if (message.startsWith('Synced') && !message.startsWith('Synced 0')) return 'success'
  if (message.startsWith('No ')) return 'info'
  return 'error'
}
