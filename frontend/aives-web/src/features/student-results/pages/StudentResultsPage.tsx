import { AlertCircle, ArrowLeft, CalendarClock, ChevronRight, RotateCw, Trophy } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { studentSessionsApi } from '@/features/student-sessions/api/studentSessionsApi'
import { getStudentSessionState, STUDENT_SESSION_STATES } from '@/features/student-sessions/lib/sessionState'
import type { StudentSessionEnrollment } from '@/features/student-sessions/types'
import { formatTimeRange, parseApiDate } from '@/lib/format/date'
import { useApi } from '@/lib/http/useApi'
import { cn } from '@/lib/utils'
import { studentResultsApi } from '../api/studentResultsApi'
import { ResultDetail } from '../components/ResultDetail'

/**
 * There is no "my results" API yet, so the history is built from the student's enrollments:
 * every session they have finished, published or not. ?sessionId= opens one result.
 */
export default function StudentResultsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const sessionId = Number(searchParams.get('sessionId')) || null

  return (
    <div className="mx-auto max-w-4xl py-6">
      <header className="mb-8">
        <h1 className="font-heading text-3xl font-medium tracking-tight sm:text-4xl">Viva exam results</h1>
        <p className="mt-2 text-muted-foreground">
          See your official score, the examiner's feedback and exam details once they are published.
        </p>
      </header>

      {sessionId ? (
        <ResultView key={sessionId} sessionId={sessionId} onBack={() => setSearchParams({})} />
      ) : (
        <ResultHistory onOpen={(id) => setSearchParams({ sessionId: String(id) })} />
      )}
    </div>
  )
}

function ResultView({ sessionId, onBack }: { sessionId: number; onBack: () => void }) {
  const { state, reload } = useApi(() => studentResultsApi.get(sessionId), sessionId, 'Could not load this result.')

  return (
    <div className="space-y-6">
      <Button variant="ghost" size="sm" onClick={onBack} className="-ml-2">
        <ArrowLeft aria-hidden="true" />
        All results
      </Button>
      {state.status === 'loading' && <div className="h-48 animate-pulse rounded-3xl bg-muted motion-reduce:animate-none" />}
      {state.status === 'error' && (
        <Alert variant="destructive" className="rounded-2xl">
          <AlertCircle className="size-4" />
          <AlertDescription>
            {state.message}{' '}
            <Button variant="link" className="h-auto p-0" onClick={reload}>Try again</Button>
          </AlertDescription>
        </Alert>
      )}
      {state.status === 'ready' && <ResultDetail result={state.data} />}
    </div>
  )
}

function ResultHistory({ onOpen }: { onOpen: (sessionId: number) => void }) {
  const { state, reload } = useApi(studentSessionsApi.listMine, '', 'Could not load your exams.')
  // Captured once so the list doesn't re-classify on every render.
  const [now] = useState(() => Date.now())

  const finished = useMemo(() => {
    if (state.status !== 'ready') return []
    return state.data
      .map((s) => ({ session: s, state: getStudentSessionState(s, now) }))
      .filter(({ state: st }) => st === 'completed' || st === 'published')
      .sort((a, b) => parseApiDate(b.session.startTime).getTime() - parseApiDate(a.session.startTime).getTime())
  }, [state, now])

  if (state.status === 'loading') {
    return (
      <div aria-busy="true" className="space-y-3">
        {[0, 1].map((i) => <div key={i} className="h-20 animate-pulse rounded-2xl bg-muted motion-reduce:animate-none" />)}
      </div>
    )
  }

  if (state.status === 'error') {
    return (
      <Alert variant="destructive" className="rounded-2xl">
        <AlertCircle className="size-4" />
        <AlertDescription>
          {state.message}{' '}
          <Button variant="link" className="h-auto p-0" onClick={reload}>Try again</Button>
        </AlertDescription>
      </Alert>
    )
  }

  if (finished.length === 0) {
    return (
      <div className="flex flex-col items-center gap-2 rounded-3xl border border-dashed p-12 text-center text-muted-foreground">
        <Trophy className="size-9 opacity-50" aria-hidden="true" />
        <p className="font-medium text-foreground">No results yet</p>
        <p className="max-w-sm text-sm">Your results appear here after you finish a viva exam.</p>
        <Button asChild variant="secondary" className="mt-2">
          <Link to="/student/sessions">Go to My sessions</Link>
        </Button>
      </div>
    )
  }

  return (
    <section aria-labelledby="history-heading">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 id="history-heading" className="font-heading text-lg font-medium">
          Your exams ({finished.length})
        </h2>
        <Button variant="ghost" size="sm" onClick={reload}>
          <RotateCw aria-hidden="true" />
          Refresh
        </Button>
      </div>
      <ul className="space-y-3">
        {finished.map(({ session, state: st }) => (
          <li key={session.sessionEnrollmentId}>
            <HistoryItem session={session} published={st === 'published'} onOpen={() => onOpen(session.sessionId)} />
          </li>
        ))}
      </ul>
    </section>
  )
}

function HistoryItem({ session, published, onOpen }: { session: StudentSessionEnrollment; published: boolean; onOpen: () => void }) {
  const meta = STUDENT_SESSION_STATES[published ? 'published' : 'completed']
  return (
    <button
      type="button"
      onClick={onOpen}
      className="flex w-full items-center gap-4 rounded-2xl border p-5 text-left transition-colors hover:border-brand/40 hover:bg-brand-soft/30"
    >
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <p className="font-heading font-medium">{session.examTitle}</p>
          <span className={cn('rounded-full px-2.5 py-0.5 text-xs font-medium', meta.tone)}>{meta.label}</span>
        </div>
        <p className="mt-1 truncate text-sm text-muted-foreground">
          {session.courseName} · {session.sessionName}
        </p>
        <p className="mt-1 flex items-center gap-1.5 text-xs text-muted-foreground">
          <CalendarClock className="size-3.5" aria-hidden="true" />
          {formatTimeRange(parseApiDate(session.startTime), parseApiDate(session.endTime))}
        </p>
      </div>
      <span className="flex shrink-0 items-center gap-1 text-sm font-medium text-brand">
        {published ? 'View result' : 'View status'}
        <ChevronRight className="size-4" aria-hidden="true" />
      </span>
    </button>
  )
}
