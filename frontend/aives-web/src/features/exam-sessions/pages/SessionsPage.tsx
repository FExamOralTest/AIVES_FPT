import { CalendarClock, CircleAlert, MapPin, Plus, RotateCw, Users } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/features/auth'
import { SESSION_STATUS_LABEL, SESSION_STATUS_TONE } from '@/lib/domain/sessionStatus'
import { formatTimeRange, parseApiDate } from '@/lib/format/date'
import { useApi } from '@/lib/http/useApi'
import { cn } from '@/lib/utils'
import { sessionsApi } from '../api/sessionsApi'
import { CreateSessionDialog } from '../components/CreateSessionDialog'
import { SessionDetailDialog } from '../components/SessionDetailDialog'
import type { ExamSession, ExamSessionSummary } from '../types'

export default function SessionsPage() {
  const { user } = useAuth()
  const teacherId = user?.userId ?? 0
  const { state, reload, update } = useApi(() => sessionsApi.listMine(teacherId), teacherId, 'Could not load exam sessions.')
  const [params, setParams] = useSearchParams()
  // "Create a session for this exam" on an exam links here with ?examId=…, opening the form preselected.
  const linkedExamId = Number(params.get('examId')) || null
  const [creating, setCreating] = useState(linkedExamId !== null)
  const [viewingId, setViewingId] = useState<number | null>(null)

  function closeCreate() {
    setCreating(false)
    if (linkedExamId) setParams({}, { replace: true })
  }

  function handleCreated(session: ExamSession) {
    update((list) => [toSummary(session), ...list])
    closeCreate()
    setViewingId(session.sessionId) // show the new enroll code right away
  }

  function handleChanged(session: ExamSession) {
    update((list) => list.map((s) => (s.sessionId === session.sessionId ? toSummary(session) : s)))
  }

  const sessions = state.status === 'ready' ? state.data : []

  return (
    <>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-3xl font-medium tracking-tight">Exam sessions</h1>
          <p className="mt-1.5 max-w-2xl text-muted-foreground">
            Schedule sessions from your exams, send the join code to students, and start or end each session.
          </p>
        </div>
        <Button onClick={() => setCreating(true)}>
          <Plus aria-hidden="true" />
          Create session
        </Button>
      </header>

      <section aria-label="Session list" className="mt-6">
        {state.status === 'loading' && (
          <ul aria-busy="true" className="flex flex-col gap-3">
            {[0, 1, 2].map((i) => (
              <li key={i} className="h-24 animate-pulse rounded-2xl bg-muted motion-reduce:animate-none" />
            ))}
          </ul>
        )}

        {state.status === 'error' && (
          <div className="flex flex-col items-center gap-3 rounded-2xl border px-6 py-14 text-center">
            <CircleAlert className="size-7 text-destructive" aria-hidden="true" />
            <p className="max-w-sm text-muted-foreground">{state.message}</p>
            <Button variant="secondary" size="sm" onClick={reload}>
              <RotateCw aria-hidden="true" />
              Try again
            </Button>
          </div>
        )}

        {state.status === 'ready' && sessions.length === 0 && (
          <div className="flex flex-col items-center gap-3 rounded-2xl border-2 border-dashed px-6 py-14 text-center">
            <CalendarClock className="size-8 text-muted-foreground" aria-hidden="true" />
            <p className="font-heading text-lg font-medium">No exam sessions yet</p>
            <p className="max-w-sm text-sm text-muted-foreground">
              Create a session from an exam to get a join code for your students.
            </p>
            <Button onClick={() => setCreating(true)}>
              <Plus aria-hidden="true" />
              Create session
            </Button>
          </div>
        )}

        {state.status === 'ready' && sessions.length > 0 && (
          <ul className="flex flex-col gap-3">
            {sessions.map((s) => (
              <li key={s.sessionId}>
                <SessionRow session={s} onOpen={() => setViewingId(s.sessionId)} />
              </li>
            ))}
          </ul>
        )}
      </section>

      <CreateSessionDialog open={creating} initialExamId={linkedExamId} onClose={closeCreate} onCreated={handleCreated} />
      <SessionDetailDialog sessionId={viewingId} onClose={() => setViewingId(null)} onChanged={handleChanged} />
    </>
  )
}

function SessionRow({ session, onOpen }: { session: ExamSessionSummary; onOpen: () => void }) {
  return (
    <button
      type="button"
      onClick={onOpen}
      className="flex w-full flex-col gap-2 rounded-2xl border p-5 text-left transition-colors hover:border-brand/40 hover:bg-brand-soft/30 sm:flex-row sm:items-center sm:gap-6"
    >
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="font-heading font-medium">{session.sessionName}</h2>
          <span className={cn('rounded-full px-2.5 py-0.5 text-xs font-medium', SESSION_STATUS_TONE[session.status])}>
            {SESSION_STATUS_LABEL[session.status]}
          </span>
        </div>
        <p className="mt-1 truncate text-sm text-muted-foreground">
          {session.examTitle} · <code className="font-mono">{session.examCode}</code>
        </p>
      </div>
      <div className="flex flex-col gap-1 text-sm text-muted-foreground sm:items-end">
        <span className="flex items-center gap-1.5">
          <CalendarClock className="size-4" aria-hidden="true" />
          {formatTimeRange(parseApiDate(session.startTime), parseApiDate(session.endTime))}
        </span>
        <span className="flex items-center gap-3">
          {session.room && (
            <span className="flex items-center gap-1.5">
              <MapPin className="size-4" aria-hidden="true" />
              {session.room}
            </span>
          )}
          <span className="flex items-center gap-1.5">
            <Users className="size-4" aria-hidden="true" />
            {session.enrolledStudents} students
          </span>
        </span>
      </div>
    </button>
  )
}

function toSummary(s: ExamSession): ExamSessionSummary {
  const { sessionId, sessionName, examTitle, examCode, room, startTime, endTime, status, enrolledStudents } = s
  return { sessionId, sessionName, examTitle, examCode, room, startTime, endTime, status, enrolledStudents }
}
