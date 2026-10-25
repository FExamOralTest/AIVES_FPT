import { ArrowDown, ArrowUp, ArrowUpDown, CalendarClock, ChevronLeft, ChevronRight, Search } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Button } from '@/components/ui/button'
import { SESSION_STATUS_LABEL, SESSION_STATUS_TONE, type SessionStatus } from '@/lib/domain/sessionStatus'
import { parseApiDate } from '@/lib/format/date'
import { cn } from '@/lib/utils'
import type { ExamSessionSummary } from '../types'

const PAGE_SIZE = 20
const TABS: (SessionStatus | null)[] = [null, 'IN_PROGRESS', 'NOT_STARTED', 'COMPLETED', 'CANCELLED']

type SortKey = 'name' | 'time' | 'students'
type Sort = { key: SortKey; desc: boolean }

const shortDay = new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' })
const shortTime = new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit' })

interface SessionTableProps {
  /** Sessions already narrowed by the page's date filter; the tabs and search narrow them further. */
  sessions: ExamSessionSummary[]
  status: SessionStatus | null
  onStatusChange: (status: SessionStatus | null) => void
  onOpen: (session: ExamSessionSummary) => void
}

/** Status tabs with counts, a search box, sortable columns and 20 sessions per page. */
export function SessionTable({ sessions, status, onStatusChange, onOpen }: SessionTableProps) {
  const [query, setQuery] = useState('')
  const [sort, setSort] = useState<Sort>({ key: 'time', desc: true })
  const [page, setPage] = useState(0)

  const counts = useMemo(() => {
    const byStatus: Record<string, number> = { all: sessions.length }
    for (const s of sessions) byStatus[s.status] = (byStatus[s.status] ?? 0) + 1
    return byStatus
  }, [sessions])

  const rows = useMemo(() => {
    const q = query.trim().toLowerCase()
    const matched = sessions.filter(
      (s) =>
        (status === null || s.status === status) &&
        (!q || [s.sessionName, s.examTitle, s.examCode, s.room ?? ''].some((field) => field.toLowerCase().includes(q))),
    )
    const value = (s: ExamSessionSummary) =>
      sort.key === 'name' ? s.sessionName.toLowerCase() : sort.key === 'students' ? s.enrolledStudents : parseApiDate(s.startTime).getTime()
    return matched.sort((a, b) => {
      const [x, y] = [value(a), value(b)]
      const order = x < y ? -1 : x > y ? 1 : 0
      return sort.desc ? -order : order
    })
  }, [sessions, status, query, sort])

  const pageCount = Math.max(1, Math.ceil(rows.length / PAGE_SIZE))
  const current = Math.min(page, pageCount - 1)
  const visible = rows.slice(current * PAGE_SIZE, (current + 1) * PAGE_SIZE)

  function toggleSort(key: SortKey) {
    // Names start A→Z; time and students start with the newest / largest first.
    setSort((s) => (s.key === key ? { key, desc: !s.desc } : { key, desc: key !== 'name' }))
    setPage(0)
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div role="tablist" aria-label="Session status" className="-mx-1 flex gap-1 overflow-x-auto px-1 pb-1">
          {TABS.map((tab) => {
            const selected = tab === status
            return (
              <button
                key={tab ?? 'all'}
                type="button"
                role="tab"
                aria-selected={selected}
                onClick={() => {
                  onStatusChange(tab)
                  setPage(0)
                }}
                className={cn(
                  'flex shrink-0 items-center gap-2 rounded-full border px-3.5 py-1.5 text-sm transition-colors',
                  selected ? 'border-foreground bg-foreground text-background' : 'hover:bg-muted',
                )}
              >
                {tab ? SESSION_STATUS_LABEL[tab] : 'All'}
                <span className={cn('rounded-full px-1.5 text-xs tabular-nums', selected ? 'bg-background/20' : 'bg-muted')}>
                  {counts[tab ?? 'all'] ?? 0}
                </span>
              </button>
            )
          })}
        </div>
        <label className="relative block lg:w-72">
          <span className="sr-only">Search sessions</span>
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
          <input
            type="search"
            value={query}
            onChange={(e) => {
              setQuery(e.target.value)
              setPage(0)
            }}
            placeholder="Session, exam, code or room"
            className="block w-full rounded-xl border bg-background py-2 pr-3 pl-9 text-sm"
          />
        </label>
      </div>

      {visible.length === 0 ? (
        <div className="flex flex-col items-center gap-2 rounded-2xl border-2 border-dashed px-6 py-12 text-center">
          <CalendarClock className="size-8 text-muted-foreground" aria-hidden="true" />
          <p className="text-muted-foreground">{sessions.length === 0 ? 'No exam sessions yet.' : 'No sessions match these filters.'}</p>
        </div>
      ) : (
        <div className="overflow-hidden rounded-2xl border">
          <table className="w-full text-sm">
            <thead className="bg-muted/50 text-left text-xs text-muted-foreground">
              <tr>
                <SortHeader label="Session" sortKey="name" sort={sort} onSort={toggleSort} />
                <th scope="col" className="hidden px-3 py-2.5 sm:px-4 font-medium md:table-cell">Exam</th>
                <SortHeader label="Time" sortKey="time" sort={sort} onSort={toggleSort} />
                <th scope="col" className="hidden px-3 py-2.5 sm:px-4 font-medium sm:table-cell">Room</th>
                <SortHeader label="Students" sortKey="students" sort={sort} onSort={toggleSort} className="hidden text-right sm:table-cell" />
                <th scope="col" className="w-8"><span className="sr-only">Open</span></th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {visible.map((s) => {
                const start = parseApiDate(s.startTime)
                const end = parseApiDate(s.endTime)
                return (
                  <tr key={s.sessionId} onClick={() => onOpen(s)} className="cursor-pointer transition-colors hover:bg-brand-soft/30">
                    <td className="px-3 py-3 sm:px-4">
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation()
                          onOpen(s)
                        }}
                        className="text-left font-medium hover:underline"
                      >
                        {s.sessionName}
                      </button>
                      <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1">
                        <span className={cn('rounded-full px-2 py-0.5 text-[11px] font-medium', SESSION_STATUS_TONE[s.status])}>
                          {SESSION_STATUS_LABEL[s.status] ?? s.status}
                        </span>
                        <span className="text-xs text-muted-foreground md:hidden">{s.examTitle}</span>
                        <span className="text-xs text-muted-foreground sm:hidden">
                          · {s.enrolledStudents} student{s.enrolledStudents === 1 ? '' : 's'}
                        </span>
                      </div>
                    </td>
                    <td className="hidden max-w-56 px-3 py-3 sm:px-4 md:table-cell">
                      <span className="block truncate">{s.examTitle}</span>
                      <code className="font-mono text-xs text-muted-foreground">{s.examCode}</code>
                    </td>
                    <td className="px-3 py-3 sm:px-4 whitespace-nowrap">
                      <span className="block">{shortDay.format(start)}</span>
                      <span className="text-xs text-muted-foreground">
                        {shortTime.format(start)} – {shortTime.format(end)}
                      </span>
                    </td>
                    <td className="hidden px-3 py-3 sm:px-4 text-muted-foreground sm:table-cell">{s.room ?? '—'}</td>
                    <td className="hidden px-3 py-3 text-right tabular-nums sm:table-cell sm:px-4">{s.enrolledStudents}</td>
                    <td className="pr-3 text-muted-foreground">
                      <ChevronRight className="size-4" aria-hidden="true" />
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      {rows.length > PAGE_SIZE && (
        <nav aria-label="Pages" className="flex items-center justify-between gap-3 text-sm">
          <span className="text-muted-foreground">
            {current * PAGE_SIZE + 1}–{Math.min(rows.length, (current + 1) * PAGE_SIZE)} of {rows.length}
          </span>
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" disabled={current === 0} onClick={() => setPage(current - 1)}>
              <ChevronLeft aria-hidden="true" />
              Previous
            </Button>
            <span className="tabular-nums">
              {current + 1} / {pageCount}
            </span>
            <Button variant="outline" size="sm" disabled={current >= pageCount - 1} onClick={() => setPage(current + 1)}>
              Next
              <ChevronRight aria-hidden="true" />
            </Button>
          </div>
        </nav>
      )}
    </div>
  )
}

function SortHeader({
  label,
  sortKey,
  sort,
  onSort,
  className,
}: {
  label: string
  sortKey: SortKey
  sort: Sort
  onSort: (key: SortKey) => void
  className?: string
}) {
  const active = sort.key === sortKey
  const Icon = !active ? ArrowUpDown : sort.desc ? ArrowDown : ArrowUp
  return (
    <th scope="col" aria-sort={active ? (sort.desc ? 'descending' : 'ascending') : 'none'} className={cn('px-3 py-2.5 sm:px-4 font-medium', className)}>
      <button type="button" onClick={() => onSort(sortKey)} className="inline-flex items-center gap-1 hover:text-foreground">
        {label}
        <Icon className={cn('size-3.5', !active && 'opacity-50')} aria-hidden="true" />
      </button>
    </th>
  )
}
