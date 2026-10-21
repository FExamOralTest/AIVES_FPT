// The backend stores UTC but may serialize DateTime without an offset ("2026-10-15T07:30:00").
// JS would read that as local time (7 hours off in Vietnam), so treat offset-less values as UTC.
const HAS_OFFSET = /(Z|[+-]\d{2}:?\d{2})$/i

export function parseApiDate(value: string): Date {
  return new Date(HAS_OFFSET.test(value) ? value : `${value}Z`)
}

// en-GB: English words with day/month order and a 24-hour clock, as users in Vietnam expect.
const LOCALE = 'en-GB'

const dayFormat = new Intl.DateTimeFormat(LOCALE, {
  weekday: 'long',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})
const timeFormat = new Intl.DateTimeFormat(LOCALE, { hour: '2-digit', minute: '2-digit' })

/** "Thursday, 15/10/2026 · 14:30 – 16:30" (end on another day keeps its own date). */
export function formatTimeRange(start: Date, end: Date): string {
  const sameDay = start.toDateString() === end.toDateString()
  const endText = sameDay ? timeFormat.format(end) : `${timeFormat.format(end)}, ${dayFormat.format(end)}`
  return `${dayFormat.format(start)} · ${timeFormat.format(start)} – ${endText}`
}

const dateTimeFormat = new Intl.DateTimeFormat(LOCALE, {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

/** "15/10/2026, 14:30". */
export function formatDateTime(date: Date): string {
  return dateTimeFormat.format(date)
}

// numeric: 'always' keeps "in 2 days" instead of words like "tomorrow" that read oddly mid-sentence.
const relative = new Intl.RelativeTimeFormat(LOCALE, { numeric: 'always' })

/** "in 3 days", "in 2 hours", "15 minutes ago". */
export function formatRelative(target: Date, now = new Date()): string {
  const minutes = Math.round((target.getTime() - now.getTime()) / 60_000)
  if (Math.abs(minutes) < 60) return relative.format(minutes, 'minute')
  const hours = Math.round(minutes / 60)
  if (Math.abs(hours) < 24) return relative.format(hours, 'hour')
  return relative.format(Math.round(hours / 24), 'day')
}
