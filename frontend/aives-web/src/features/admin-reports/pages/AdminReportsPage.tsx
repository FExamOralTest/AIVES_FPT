import { CalendarDays, CircleAlert, LayoutDashboard, ListChecks, RotateCw, SlidersHorizontal } from 'lucide-react'
import { Popover } from 'radix-ui'
import { useMemo, useState } from 'react'
import { PageBar, TableSkeleton, TOOLBAR_BUTTON } from '@/components/common/FlatPage'
import { Button } from '@/components/ui/button'
import { ROLE_HOME } from '@/features/auth'
import { parseApiDate } from '@/lib/format/date'
import { useApi } from '@/lib/http/useApi'
import { cn } from '@/lib/utils'
import { adminReportsApi } from '../api/adminReportsApi'
import { SessionResultsDialog } from '../components/SessionResultsDialog'
import { SessionTable } from '@/features/exam-sessions/components/SessionTable'
import type { ExamSessionSummary, SessionStatus } from '../types'

const dateInputClass =
  'mt-1 block h-9 w-full rounded-lg border border-input bg-background px-3 text-sm text-foreground outline-none focus-visible:border-brand focus-visible:ring-4 focus-visible:ring-brand/15'

export default function AdminReportsPage() {
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [status, setStatus] = useState<SessionStatus | null>(null)
  const [viewing, setViewing] = useState<ExamSessionSummary | null>(null)
  // Loaded once, unfiltered: the status tabs need every session to show their counts.
  const sessions = useApi(adminReportsApi.listSessions, '', 'Could not load exam sessions.')

  const datedSessions = useMemo(() => {
    if (sessions.state.status !== 'ready') return []
    const from = fromDate ? new Date(`${fromDate}T00:00:00`).getTime() : -Infinity
    const to = toDate ? new Date(`${toDate}T23:59:59.999`).getTime() : Infinity
    return sessions.state.data.filter((s) => {
      const start = parseApiDate(s.startTime).getTime()
      return start >= from && start <= to
    })
  }, [sessions.state, fromDate, toDate])

  const activeDates = Number(Boolean(fromDate)) + Number(Boolean(toDate))

  // The date range lives behind a toolbar "Filter" button so the toolbar stays on one row.
  const dateFilter = (
    <Popover.Root>
      <Popover.Trigger asChild>
        <Button variant="outline" className={cn(TOOLBAR_BUTTON, activeDates && 'border-brand/40 text-brand')}>
          <SlidersHorizontal aria-hidden="true" />
          Filter
          {activeDates > 0 && (
            <span className="rounded-md bg-brand-soft px-1.5 text-xs tabular-nums">{activeDates}</span>
          )}
        </Button>
      </Popover.Trigger>
      <Popover.Portal>
        <Popover.Content
          align="end"
          sideOffset={6}
          aria-label="Filter by date"
          className="z-50 w-72 rounded-xl border bg-background p-4 text-sm shadow-[0_12px_32px_-12px_rgba(17,17,20,0.25)]"
        >
          <p className="flex items-center gap-2 font-medium">
            <CalendarDays className="size-4 text-muted-foreground" aria-hidden="true" />
            Session start date
          </p>
          <div className="mt-3 grid gap-3">
            <label className="block text-muted-foreground">
              From
              <input type="date" className={dateInputClass} value={fromDate} max={toDate || undefined} onChange={(e) => setFromDate(e.target.value)} />
            </label>
            <label className="block text-muted-foreground">
              To
              <input type="date" className={dateInputClass} value={toDate} min={fromDate || undefined} onChange={(e) => setToDate(e.target.value)} />
            </label>
          </div>
          <Button
            variant="ghost"
            className="mt-3 h-8 w-full rounded-lg text-sm font-medium"
            disabled={!activeDates}
            onClick={() => {
              setFromDate('')
              setToDate('')
            }}
          >
            Clear dates
          </Button>
        </Popover.Content>
      </Popover.Portal>
    </Popover.Root>
  )

  return (
    <>
      <PageBar
        parent={{ label: 'Administration', to: ROLE_HOME.ADMIN, icon: LayoutDashboard }}
        title="Exams & results"
        icon={ListChecks}
        actions={[{ label: 'Reload list', icon: RotateCw, onSelect: sessions.reload }]}
      />

      <section aria-label="Exam sessions" aria-busy={sessions.state.status === 'loading'}>
        {sessions.state.status === 'loading' && <TableSkeleton />}
        {sessions.state.status === 'error' && (
          <div className="flex flex-col items-center gap-3 px-6 py-14 text-center">
            <CircleAlert className="size-7 text-destructive" aria-hidden="true" />
            <p className="max-w-sm text-muted-foreground">{sessions.state.message}</p>
            <Button variant="secondary" size="sm" onClick={sessions.reload}>
              <RotateCw aria-hidden="true" />
              Try again
            </Button>
          </div>
        )}
        {sessions.state.status === 'ready' && (
          <SessionTable
            sessions={datedSessions}
            status={status}
            onStatusChange={setStatus}
            onOpen={setViewing}
            controls={dateFilter}
          />
        )}
      </section>

      <SessionResultsDialog session={viewing} onClose={() => setViewing(null)} />
    </>
  )
}
