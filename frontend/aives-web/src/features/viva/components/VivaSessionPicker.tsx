import { CalendarClock, Mic, RotateCw } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { studentSessionsApi } from '@/features/student-sessions/api/studentSessionsApi'
import { getStudentSessionState, type StudentSessionState } from '@/features/student-sessions/lib/sessionState'
import type { StudentSessionEnrollment } from '@/features/student-sessions/types'
import { formatRelative, formatTimeRange, parseApiDate } from '@/lib/format/date'
import { useApi } from '@/lib/http/useApi'

interface VivaSessionPickerProps {
  /** The session being entered, if any; its button shows a spinner and the others are disabled. */
  enteringId: number | null
  onEnter: (sessionId: number) => void
}

type Row = { session: StudentSessionEnrollment; state: StudentSessionState }

/** Lists the student's sessions that can be entered now, plus upcoming ones for context. */
export function VivaSessionPicker({ enteringId, onEnter }: VivaSessionPickerProps) {
  const { state, reload } = useApi(studentSessionsApi.listMine, '', 'Could not load your sessions.')
  const [now, setNow] = useState(() => Date.now())

  const { open, upcoming } = useMemo(() => {
    const rows: Row[] = state.status === 'ready' ? state.data.map((s) => ({ session: s, state: getStudentSessionState(s, now) })) : []
    const byStart = (a: Row, b: Row) => parseApiDate(a.session.startTime).getTime() - parseApiDate(b.session.startTime).getTime()
    return {
      open: rows.filter((r) => r.state === 'ready' || r.state === 'inProgress').sort(byStart),
      upcoming: rows.filter((r) => r.state === 'waiting').sort(byStart),
    }
  }, [state, now])

  function refresh() {
    setNow(Date.now())
    reload()
  }

  if (state.status === 'loading') {
    return (
      <div aria-busy="true" className="space-y-3">
        {[0, 1].map((i) => <div key={i} className="h-20 animate-pulse rounded-2xl bg-muted motion-reduce:animate-none" />)}
      </div>
    )
  }

  if (state.status === 'error') {
    return (
      <p role="alert" className="text-sm text-destructive">
        {state.message}{' '}
        <Button variant="link" className="h-auto p-0" onClick={refresh}>Try again</Button>
      </p>
    )
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-3">
        <h2 className="font-heading font-medium">Sessions open now</h2>
        <Button variant="ghost" size="sm" onClick={refresh}>
          <RotateCw aria-hidden="true" />
          Refresh
        </Button>
      </div>

      {open.length === 0 ? (
        <p className="rounded-2xl border border-dashed p-6 text-center text-sm text-muted-foreground">
          No session is open right now. Sessions appear here once the staff starts them.
        </p>
      ) : (
        <ul className="space-y-3">
          {open.map(({ session, state: st }) => (
            <li key={session.sessionEnrollmentId} className="flex flex-col gap-3 rounded-2xl border p-5 sm:flex-row sm:items-center sm:justify-between">
              <SessionSummary session={session} />
              <Button
                className="shrink-0"
                disabled={enteringId !== null}
                onClick={() => onEnter(session.sessionId)}
              >
                <Mic aria-hidden="true" />
                {enteringId === session.sessionId ? 'Entering…' : st === 'inProgress' ? 'Continue exam' : 'Start exam'}
              </Button>
            </li>
          ))}
        </ul>
      )}

      {upcoming.length > 0 && (
        <section aria-labelledby="upcoming-heading">
          <h2 id="upcoming-heading" className="font-heading font-medium">Upcoming</h2>
          <ul className="mt-3 space-y-3">
            {upcoming.map(({ session }) => {
              const start = parseApiDate(session.startTime)
              return (
                <li key={session.sessionEnrollmentId} className="flex flex-col gap-2 rounded-2xl border border-dashed p-5 sm:flex-row sm:items-center sm:justify-between">
                  <SessionSummary session={session} />
                  <span className="shrink-0 text-sm text-muted-foreground">
                    {start.getTime() > now ? `Starts ${formatRelative(start, new Date(now))}` : 'Waiting for the session to open'}
                  </span>
                </li>
              )
            })}
          </ul>
        </section>
      )}

      <p className="border-t pt-5 text-sm text-muted-foreground">
        Not registered for a session?{' '}
        <Link to="/student/enroll" className="font-semibold text-brand hover:text-brand-hover">
          Join a session
        </Link>
      </p>
    </div>
  )
}

function SessionSummary({ session }: { session: StudentSessionEnrollment }) {
  return (
    <div className="min-w-0">
      <p className="font-heading font-medium">{session.examTitle}</p>
      <p className="truncate text-sm text-muted-foreground">
        {session.courseName} · {session.sessionName}
        {session.room ? ` · ${session.room}` : ''}
      </p>
      <p className="mt-1 flex items-center gap-1.5 text-xs text-muted-foreground">
        <CalendarClock className="size-3.5" aria-hidden="true" />
        {formatTimeRange(parseApiDate(session.startTime), parseApiDate(session.endTime))}
      </p>
    </div>
  )
}
