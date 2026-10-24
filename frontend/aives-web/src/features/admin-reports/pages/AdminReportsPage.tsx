import { CircleAlert, RotateCw } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Button } from '@/components/ui/button'
import { parseApiDate } from '@/lib/format/date'
import { useApi } from '@/lib/http/useApi'
import { adminReportsApi } from '../api/adminReportsApi'
import { SessionResultsDialog } from '../components/SessionResultsDialog'
import { SessionTable } from '../components/SessionTable'
import type { ExamSessionSummary, SessionStatus } from '../types'

const inputClass = 'mt-1 block w-full rounded-xl border bg-background px-3 py-2 text-sm'

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

  return (
    <>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-3xl font-medium tracking-tight">Exams &amp; results</h1>
          <p className="mt-1.5 max-w-2xl text-muted-foreground">
            Every exam session in the system. Open a session to see each student's result.
          </p>
        </div>
        <Button variant="ghost" size="sm" onClick={sessions.reload} disabled={sessions.state.status === 'loading'}>
          <RotateCw aria-hidden="true" />
          Refresh
        </Button>
      </header>

      <section aria-label="Date range" className="mt-6 grid gap-3 sm:grid-cols-[1fr_1fr_auto] sm:items-end lg:max-w-xl">
        <label className="block text-sm">
          From
          <input type="date" className={inputClass} value={fromDate} max={toDate || undefined} onChange={(e) => setFromDate(e.target.value)} />
        </label>
        <label className="block text-sm">
          To
          <input type="date" className={inputClass} value={toDate} min={fromDate || undefined} onChange={(e) => setToDate(e.target.value)} />
        </label>
        <Button
          variant="outline"
          disabled={!fromDate && !toDate}
          onClick={() => {
            setFromDate('')
            setToDate('')
          }}
        >
          Clear dates
        </Button>
      </section>

      <section aria-label="Exam sessions" aria-busy={sessions.state.status === 'loading'} className="mt-6">
        {sessions.state.status === 'loading' && <div className="h-80 animate-pulse rounded-2xl bg-muted motion-reduce:animate-none" />}
        {sessions.state.status === 'error' && (
          <div className="flex flex-col items-center gap-3 rounded-2xl border px-6 py-10 text-center">
            <CircleAlert className="size-7 text-destructive" aria-hidden="true" />
            <p className="max-w-sm text-muted-foreground">{sessions.state.message}</p>
            <Button variant="secondary" size="sm" onClick={sessions.reload}>
              <RotateCw aria-hidden="true" />
              Try again
            </Button>
          </div>
        )}
        {sessions.state.status === 'ready' && (
          <SessionTable sessions={datedSessions} status={status} onStatusChange={setStatus} onOpen={setViewing} />
        )}
      </section>

      <SessionResultsDialog session={viewing} onClose={() => setViewing(null)} />
    </>
  )
}
