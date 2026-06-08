/** Map API strategy status to compact terminal labels. */
export function formatStrategyLabel(status: string | null | undefined): string {
  if (!status || status === 'WAITING') return 'WAITING'
  if (status === 'READY TO SELL') return 'TAKE PROFIT'
  if (status === 'ACCUMULATION ZONE') return 'ACCUMULATE'
  return status
}

export function strategyTagClass(status: string | null | undefined): string {
  if (!status) return 'tag tag-wait'
  if (status === 'READY TO SELL') return 'tag tag-profit'
  if (status === 'ACCUMULATION ZONE') return 'tag tag-accum'
  return 'tag tag-wait'
}
