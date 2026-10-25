import { CalendarClock, CircleAlert, LayoutDashboard, PlusCircle, RotateCw } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { PageBar, TableSkeleton, TOOLBAR_BUTTON } from '@/components/common/FlatPage'
import { Button } from '@/components/ui/button'
import { ROLE_HOME, ROLE_LABEL, useAuth } from '@/features/auth'
import type { SessionStatus } from '@/lib/domain/sessionStatus'
import { useApi } from '@/lib/http/useApi'
import { sessionsApi } from '../api/sessionsApi'
import { CreateSessionDialog } from '../components/SessionFormDialog'
import { SessionDetailDialog } from '../components/SessionDetailDialog'
import { SessionTable } from '../components/SessionTable'
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
  const [status, setStatus] = useState<SessionStatus | null>(null)

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

  function handleDeleted(sessionId: number) {
    update((list) => list.filter((s) => s.sessionId !== sessionId))
    setViewingId(null)
  }

  const sessions = state.status === 'ready' ? state.data : []

  return (
    <>
      <PageBar
        parent={{ label: ROLE_LABEL.STAFF, to: ROLE_HOME.STAFF, icon: LayoutDashboard }}
        title="Exam sessions"
        icon={CalendarClock}
        actions={[
          { label: 'New session', icon: PlusCircle, onSelect: () => setCreating(true) },
          { label: 'Reload list', icon: RotateCw, onSelect: reload },
        ]}
      />

      <section aria-label="Session list" aria-busy={state.status === 'loading'}>
        {state.status === 'loading' && <TableSkeleton />}

        {state.status === 'error' && (
          <div className="flex flex-col items-center gap-3 px-6 py-14 text-center">
            <CircleAlert className="size-7 text-destructive" aria-hidden="true" />
            <p className="max-w-sm text-muted-foreground">{state.message}</p>
            <Button variant="secondary" size="sm" onClick={reload}>
              <RotateCw aria-hidden="true" />
              Try again
            </Button>
          </div>
        )}

        {state.status === 'ready' && sessions.length === 0 && (
          <div className="flex flex-col items-center gap-3 px-6 py-14 text-center">
            <CalendarClock className="size-8 text-muted-foreground" aria-hidden="true" />
            <p className="font-heading text-lg font-medium">No exam sessions yet</p>
            <p className="max-w-sm text-sm text-muted-foreground">
              Create a session from an exam to get a join code for your students.
            </p>
            <Button variant="brand" className={TOOLBAR_BUTTON} onClick={() => setCreating(true)}>
              <PlusCircle aria-hidden="true" />
              New session
            </Button>
          </div>
        )}

        {state.status === 'ready' && sessions.length > 0 && (
          <SessionTable
            sessions={sessions}
            status={status}
            onStatusChange={setStatus}
            onOpen={(s) => setViewingId(s.sessionId)}
            openLabel="Open session"
            primaryAction={
              <Button variant="brand" className={TOOLBAR_BUTTON} onClick={() => setCreating(true)}>
                <PlusCircle aria-hidden="true" />
                New session
              </Button>
            }
          />
        )}
      </section>

      <CreateSessionDialog open={creating} initialExamId={linkedExamId} onClose={closeCreate} onCreated={handleCreated} />
      <SessionDetailDialog sessionId={viewingId} onClose={() => setViewingId(null)}
        onChanged={handleChanged}
        onDeleted={handleDeleted}
      />
    </>
  )
}

function toSummary(s: ExamSession): ExamSessionSummary {
  const { sessionId, sessionName, examTitle, examCode, room, startTime, endTime, status, enrolledStudents } = s
  return { sessionId, sessionName, examTitle, examCode, room, startTime, endTime, status, enrolledStudents }
}
