// The backend stores UTC but may serialize DateTime without an offset ("2026-10-15T07:30:00").
// JS would read that as local time (7 hours off in Vietnam), so treat offset-less values as UTC.
const HAS_OFFSET = /(Z|[+-]\d{2}:?\d{2})$/i

export function parseApiDate(value: string): Date {
  return new Date(HAS_OFFSET.test(value) ? value : `${value}Z`)
}

const dayFormat = new Intl.DateTimeFormat('vi-VN', {
  weekday: 'long',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})
const timeFormat = new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit' })

/** "Thứ Năm, 15/10/2026 · 14:30 – 16:30" (end on another day keeps its own date). */
export function formatTimeRange(start: Date, end: Date): string {
  const sameDay = start.toDateString() === end.toDateString()
  const day = capitalize(dayFormat.format(start))
  const endText = sameDay ? timeFormat.format(end) : `${timeFormat.format(end)}, ${dayFormat.format(end)}`
  return `${day} · ${timeFormat.format(start)} – ${endText}`
}

// numeric: 'always' keeps "sau 2 ngày" instead of words like "Ngày kia" that read oddly mid-sentence.
const relative = new Intl.RelativeTimeFormat('vi-VN', { numeric: 'always' })

/** "sau 3 ngày", "sau 2 giờ", "15 phút trước". */
export function formatRelative(target: Date, now = new Date()): string {
  const minutes = Math.round((target.getTime() - now.getTime()) / 60_000)
  if (Math.abs(minutes) < 60) return relative.format(minutes, 'minute')
  const hours = Math.round(minutes / 60)
  if (Math.abs(hours) < 24) return relative.format(hours, 'hour')
  return relative.format(Math.round(hours / 24), 'day')
}

function capitalize(text: string) {
  return text.charAt(0).toUpperCase() + text.slice(1)
}
