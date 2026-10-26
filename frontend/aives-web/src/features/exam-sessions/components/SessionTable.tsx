import {
  CalendarClock,
  ChevronLeft,
  ChevronRight,
  CircleCheckBig,
  CircleDot,
  CircleSlash,
  Clock,
  Eye,
  LayoutGrid,
  type LucideIcon,
} from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { ActionMenu, FilterTabs, SortHeader, SortMenu, Toolbar, ToolbarSearch } from '@/components/common/FlatPage'
import { SESSION_STATUS_LABEL, SESSION_STATUS_TONE, type SessionStatus } from '@/lib/domain/sessionStatus'
import { formatLongDate, formatTime, parseApiDate } from '@/lib/format/date'
import { cn } from '@/lib/utils'
import type { ExamSessionSummary } from '../types'

const PAGE_SIZE = 20

type Tab = 'ALL' | SessionStatus

const TABS: { value: Tab; label: string; icon: LucideIcon }[] = [
  { value: 'ALL', label: 'All sessions', icon: LayoutGrid },
  { value: 'IN_PROGRESS', label: SESSION_STATUS_LABEL.IN_PROGRESS, icon: CircleDot },
  { value: 'NOT_STARTED', label: SESSION_STATUS_LABEL.NOT_STARTED, icon: Clock },
  { value: 'COMPLETED', label: SESSION_STATUS_LABEL.COMPLETED, icon: CircleCheckBig },
  { value: 'CANCELLED', label: SESSION_STATUS_LABEL.CANCELLED, icon: CircleSlash },
]

type SortKey = 'name' | 'time' | 'students'
type Sort = { key: SortKey; dir: 'asc' | 'desc' }

const SORT_LABEL: Record<SortKey, string> = { name: 'Session name', time: 'Start time', students: 'Students' }

interface SessionTableProps {
  /** Sessions already narrowed by the page's date filter; the tabs and search narrow them further. */
  sessions: ExamSessionSummary[]
  status: SessionStatus | null
  onStatusChange: (status: SessionStatus | null) => void
  /** Row click; also the first item of the row menu, named by `openLabel`. */
  onOpen: (session: ExamSessionSummary) => void
  openLabel?: string
  /** Extra toolbar controls, placed before the sort button (e.g. a date range filter). */
  controls?: ReactNode
  /** The page's primary button, placed last in the toolbar (e.g. "New session"). */
  primaryAction?: ReactNode
}

/** Status tabs with counts, search, sortable columns and 20 sessions per page. */
export function SessionTable({
  sessions,
  status,
  onStatusChange,
  onOpen,
  openLabel = 'View results',
  controls,
  primaryAction,
}: SessionTableProps) {
  const [query, setQuery] = useState('')
  const [searchOpen, setSearchOpen] = useState(false)
  const [sort, setSort] = useState<Sort>({ key: 'time', dir: 'desc' })
  const [page, setPage] = useState(0)

  const counts = useMemo(() => {
    const byStatus: Record<Tab, number> = { ALL: sessions.length, NOT_STARTED: 0, IN_PROGRESS: 0, COMPLETED: 0, CANCELLED: 0 }
    for (const s of sessions) byStatus[s.status]++
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
      return sort.dir === 'desc' ? -order : order
    })
  }, [sessions, status, query, sort])

  const pageCount = Math.max(1, Math.ceil(rows.length / PAGE_SIZE))
  const current = Math.min(page, pageCount - 1)
  const visible = rows.slice(current * PAGE_SIZE, (current + 1) * PAGE_SIZE)

  function changeSort(next: Sort) {
    setSort(next)
    setPage(0)
  }

  function sortBy(key: SortKey) {
    // Names start A→Z; time and students start with the newest / largest first.
    changeSort(sort.key === key ? { key, dir: sort.dir === 'asc' ? 'desc' : 'asc' } : { key, dir: key === 'name' ? 'asc' : 'desc' })
  }

  const header = (key: SortKey, label: string, count?: number) => (
    <SortHeader label={label} count={count} active={sort.key === key} dir={sort.dir} onSort={() => sortBy(key)} />
  )

  return (
    <>
      <Toolbar>
        <FilterTabs
          label="Session status"
          tabs={TABS.map((t) => ({ ...t, count: counts[t.value] }))}
          value={status ?? 'ALL'}
          onChange={(tab) => {
            onStatusChange(tab === 'ALL' ? null : tab)
            setPage(0)
          }}
        />
        <div className="flex flex-wrap items-center gap-2">
          {controls}
          <SortMenu labels={SORT_LABEL} sort={sort} onSort={changeSort} />
          <ToolbarSearch
            open={searchOpen}
            onOpenChange={setSearchOpen}
            value={query}
            onChange={(value) => {
              setQuery(value)
              setPage(0)
            }}
            label="Search sessions"
            placeholder="Session, exam, code or room"
          />
          {primaryAction}
        </div>
      </Toolbar>

      {visible.length === 0 ? (
        <div className="flex flex-col items-center gap-2 px-6 py-14 text-center">
          <CalendarClock className="size-7 text-muted-foreground" aria-hidden="true" />
          <p className="text-muted-foreground">{sessions.length === 0 ? 'No exam sessions yet.' : 'No sessions match these filters.'}</p>
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[1080px] table-fixed text-left text-[15px]">
            <colgroup>
              <col />
              <col />
              <col className="w-44" />
              <col className="w-44" />
              <col className="w-32" />
              <col className="w-20" />
              <col className="w-24" />
              <col className="w-32" />
            </colgroup>
            <thead>
              <tr className="border-b [&>th:first-child]:pl-4 sm:[&>th:first-child]:pl-6">
                {header('name', 'Session', rows.length)}
                <th scope="col" className="px-3 py-3 font-medium">Exam</th>
                <th scope="col" className="px-3 py-3 font-medium">Code</th>
                {header('time', 'Date')}
                <th scope="col" className="px-3 py-3 font-medium">Time</th>
                <th scope="col" className="px-3 py-3 font-medium">Room</th>
                {header('students', 'Students')}
                <th scope="col" className="px-3 py-3 font-medium">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {visible.map((s) => {
                const start = parseApiDate(s.startTime)
                const end = parseApiDate(s.endTime)
                return (
                  <tr key={s.sessionId} onClick={() => onOpen(s)} className="cursor-pointer transition-colors hover:bg-muted/50">
                    <td className="max-w-0 py-3.5 pr-3 pl-4 sm:pl-6">
                      <div className="flex min-w-0 items-center gap-2">
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation()
                            onOpen(s)
                          }}
                          title={s.sessionName}
                          className="truncate text-left hover:underline"
                        >
                          {s.sessionName}
                        </button>
                        {/* The menu sits inside the clickable row, so keep its clicks from opening the row. */}
                        <span onClick={(e) => e.stopPropagation()} className="contents">
                          <ActionMenu
                            label={`Actions for ${s.sessionName}`}
                            actions={[{ label: openLabel, icon: Eye, onSelect: () => onOpen(s) }]}
                          />
                        </span>
                      </div>
                    </td>
                    <td title={s.examTitle} className="max-w-0 truncate px-3 py-3.5">{s.examTitle}</td>
                    <td title={s.examCode} className="max-w-0 truncate px-3 py-3.5 font-mono text-sm text-brand">{s.examCode}</td>
                    <td className="max-w-0 truncate px-3 py-3.5">{formatLongDate(start)}</td>
                    <td className="px-3 py-3.5 whitespace-nowrap text-muted-foreground tabular-nums">
                      {formatTime(start)} – {formatTime(end)}
                    </td>
                    <td className="max-w-0 truncate px-3 py-3.5 text-muted-foreground">{s.room ?? '—'}</td>
                    <td className="px-3 py-3.5 tabular-nums">{s.enrolledStudents}</td>
                    <td className="px-3 py-3.5">
                      <span className={cn('inline-flex rounded-md px-2 py-0.5 text-[13px] font-medium whitespace-nowrap', SESSION_STATUS_TONE[s.status])}>
                        {SESSION_STATUS_LABEL[s.status] ?? s.status}
                      </span>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      {rows.length > PAGE_SIZE && (
        <nav aria-label="Pages" className="flex items-center justify-between gap-3 border-t px-4 py-2.5 text-sm sm:px-6">
          <span className="text-muted-foreground">
            {current * PAGE_SIZE + 1}–{Math.min(rows.length, (current + 1) * PAGE_SIZE)} of {rows.length}
          </span>
          <div className="flex items-center gap-2">
            <Button variant="outline" className="h-8 rounded-lg px-2.5 text-sm font-medium" disabled={current === 0} onClick={() => setPage(current - 1)}>
              <ChevronLeft aria-hidden="true" />
              Previous
            </Button>
            <span className="tabular-nums">
              {current + 1} / {pageCount}
            </span>
            <Button variant="outline" className="h-8 rounded-lg px-2.5 text-sm font-medium" disabled={current >= pageCount - 1} onClick={() => setPage(current + 1)}>
              Next
              <ChevronRight aria-hidden="true" />
            </Button>
          </div>
        </nav>
      )}
    </>
  )
}
