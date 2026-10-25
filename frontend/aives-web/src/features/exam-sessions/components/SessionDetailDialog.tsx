import { CircleAlert, Pencil, Play, RotateCw, Square, Trash2, UserMinus, Users, XCircle } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { CopyButton } from '@/components/common/CopyButton'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import {
  SESSION_STATUS_LABEL,
  SESSION_STATUS_TONE,
  SESSION_TRANSITIONS,
  type SessionStatus,
} from '@/lib/domain/sessionStatus'
import { formatDateTime, formatTimeRange, parseApiDate } from '@/lib/format/date'
import { toApiError } from '@/lib/http/apiError'
import { useApi } from '@/lib/http/useApi'
import { cn } from '@/lib/utils'
import { sessionsApi } from '../api/sessionsApi'
import type { ExamSession } from '../types'
import { AddStudentsForm } from './AddStudentsForm'
import { SessionForm } from './SessionFormDialog'

interface SessionDetailDialogProps {
  sessionId: number | null
  onClose: () => void
  /** Called after an edit, status change or enrollment change so the list can reflect it. */
  onChanged: (session: ExamSession) => void
  /** Called after the session was deleted; the dialog should close. */
  onDeleted: (sessionId: number) => void
}

export function SessionDetailDialog({ sessionId, onClose, onChanged, onDeleted }: SessionDetailDialogProps) {
  return (
    <Dialog open={sessionId !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-2xl">
        {sessionId !== null && (
          <SessionDetail key={sessionId} sessionId={sessionId} onChanged={onChanged} onDeleted={onDeleted} />
        )}
      </DialogContent>
    </Dialog>
  )
}

const ACTION: Record<SessionStatus, { label: string; confirm?: string; icon: ReactNode; variant: 'default' | 'secondary' }> = {
  IN_PROGRESS: { label: 'Start session', icon: <Play aria-hidden="true" />, variant: 'default' },
  COMPLETED: {
    label: 'End session',
    confirm: 'End this session? Students will no longer be able to take the exam. This cannot be undone.',
    icon: <Square aria-hidden="true" />,
    variant: 'default',
  },
  CANCELLED: {
    label: 'Cancel session',
    confirm: 'Cancel this session? Students will not be able to join or take the exam. This cannot be undone.',
    icon: <XCircle aria-hidden="true" />,
    variant: 'secondary',
  },
  NOT_STARTED: { label: '', icon: null, variant: 'secondary' },
}

function SessionDetail({
  sessionId,
  onChanged,
  onDeleted,
}: { sessionId: number } & Pick<SessionDetailDialogProps, 'onChanged' | 'onDeleted'>) {
  const detail = useApi(() => sessionsApi.get(sessionId), sessionId, 'Could not load the session.')
  const monitor = useApi(() => sessionsApi.monitor(sessionId), sessionId, 'Could not load the student list.')
  const [confirming, setConfirming] = useState<SessionStatus | 'DELETE' | null>(null)
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)
  const [editing, setEditing] = useState(false)
  const [removingId, setRemovingId] = useState<number | null>(null)
  // Bumped on each removal to clear the add form's last result, which may mention the removed student.
  const [removals, setRemovals] = useState(0)
  const [studentError, setStudentError] = useState<string | null>(null)

  if (detail.state.status === 'loading') {
    return (
      <>
        <DialogHeader>
          <DialogTitle>Exam session</DialogTitle>
          <DialogDescription>Loading…</DialogDescription>
        </DialogHeader>
        <div className="h-40 animate-pulse rounded-2xl bg-muted motion-reduce:animate-none" />
      </>
    )
  }
  if (detail.state.status === 'error') {
    return (
      <>
        <DialogHeader>
          <DialogTitle>Exam session</DialogTitle>
          <DialogDescription className="text-destructive">{detail.state.message}</DialogDescription>
        </DialogHeader>
        <Button variant="secondary" className="w-fit" onClick={detail.reload}>
          <RotateCw aria-hidden="true" />
          Try again
        </Button>
      </>
    )
  }

  const session = detail.state.data
  const start = parseApiDate(session.startTime)
  const end = parseApiDate(session.endTime)

  function applyUpdate(updated: ExamSession) {
    detail.update(() => updated)
    onChanged(updated)
  }

  if (editing) {
    return (
      <SessionForm
        session={session}
        onCancel={() => setEditing(false)}
        onSaved={(updated) => {
          applyUpdate(updated)
          setEditing(false)
        }}
      />
    )
  }

  // Backend rules: details and students can change only before the session starts (it would still allow
  // editing a finished session or removing a student who already took the exam, so the UI stops that).
  const notStarted = session.status === 'NOT_STARTED'
  const canAddStudents = session.status === 'NOT_STARTED' || session.status === 'IN_PROGRESS'
  const deletableStatus = session.status === 'NOT_STARTED' || session.status === 'CANCELLED'

  async function deleteSession() {
    if (confirming !== 'DELETE') {
      setConfirming('DELETE')
      return
    }
    setBusy(true)
    setActionError(null)
    try {
      await sessionsApi.remove(sessionId)
      onDeleted(sessionId)
    } catch (err) {
      setActionError(toApiError(err, 'Could not delete the session.').message)
      setBusy(false)
    }
  }

  async function removeStudent(studentId: number) {
    setBusy(true)
    setStudentError(null)
    try {
      await sessionsApi.removeStudent(sessionId, studentId)
      monitor.update((m) => ({
        ...m,
        currentEnrollment: Math.max(0, m.currentEnrollment - 1),
        students: m.students.filter((s) => s.studentId !== studentId),
      }))
      applyUpdate({ ...session, enrolledStudents: Math.max(0, session.enrolledStudents - 1) })
      setRemovingId(null)
      setRemovals((n) => n + 1)
    } catch (err) {
      setStudentError(toApiError(err, 'Could not remove the student.').message)
    } finally {
      setBusy(false)
    }
  }

  // The bulk response only has IDs, so reload both to show the new names and count.
  async function handleStudentsAdded() {
    monitor.reload()
    try {
      applyUpdate(await sessionsApi.get(sessionId))
    } catch {
      detail.reload()
    }
  }

  async function changeStatus(target: SessionStatus) {
    if (ACTION[target].confirm && confirming !== target) {
      setConfirming(target)
      return
    }
    setBusy(true)
    setActionError(null)
    try {
      applyUpdate(await sessionsApi.updateStatus(sessionId, target))
      setConfirming(null)
    } catch (err) {
      setActionError(toApiError(err, 'Could not change the session status.').message)
    } finally {
      setBusy(false)
    }
  }

  const transitions = SESSION_TRANSITIONS[session.status]
  const facts = [
    { label: 'Exam', value: `${session.examTitle} (${session.examCode})` },
    { label: 'Time', value: formatTimeRange(start, end) },
    { label: 'Room', value: session.room || 'Not set' },
    { label: 'Joined', value: `${session.enrolledStudents}/${session.maxCapacity} students` },
  ]

  return (
    <>
      <DialogHeader>
        <span className={cn('w-fit rounded-full px-2.5 py-1 text-xs font-medium', SESSION_STATUS_TONE[session.status])}>
          {SESSION_STATUS_LABEL[session.status]}
        </span>
        <DialogTitle>{session.sessionName}</DialogTitle>
        <DialogDescription>Send the join code below to your students.</DialogDescription>
      </DialogHeader>

      <div className="flex flex-col items-start gap-3 rounded-2xl bg-brand-soft p-5 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-[13px] text-muted-foreground">Join code</p>
          <p className="font-mono text-2xl font-semibold tracking-[0.15em] sm:text-3xl">{session.sessionCode}</p>
        </div>
        <CopyButton value={session.sessionCode} label="Copy code" />
      </div>

      <dl className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
        {facts.map((f) => (
          <div key={f.label}>
            <dt className="text-muted-foreground">{f.label}</dt>
            <dd className="mt-0.5 font-medium">{f.value}</dd>
          </div>
        ))}
      </dl>

      {(transitions.length > 0 || notStarted || deletableStatus) && (
        <section aria-label="Manage session" className="flex flex-col gap-3">
          {confirming && (
            <p role="alert" className="rounded-[14px] bg-warning-soft px-4 py-3 text-sm text-warning">
              {confirming === 'DELETE'
                ? 'Delete this session permanently? Its join code will stop working. This cannot be undone.'
                : ACTION[confirming].confirm}
            </p>
          )}
          {actionError && (
            <p role="alert" className="flex items-center gap-2 rounded-[14px] bg-[#fef1f0] px-4 py-3 text-sm text-destructive">
              <CircleAlert className="size-4 shrink-0" aria-hidden="true" />
              {actionError}
            </p>
          )}
          <div className="flex flex-wrap gap-2">
            {notStarted && !confirming && (
              <Button variant="secondary" disabled={busy} onClick={() => setEditing(true)}>
                <Pencil aria-hidden="true" />
                Edit
              </Button>
            )}
            {transitions.map((target) => {
              const action = ACTION[target]
              const isConfirming = confirming === target
              if (confirming && !isConfirming) return null
              return (
                <Button
                  key={target}
                  variant={isConfirming && target === 'CANCELLED' ? 'destructive' : action.variant}
                  disabled={busy}
                  onClick={() => void changeStatus(target)}
                >
                  {action.icon}
                  {isConfirming ? `Confirm: ${action.label.toLowerCase()}` : action.label}
                </Button>
              )
            })}
            {deletableStatus && (!confirming || confirming === 'DELETE') && (
              <Button
                variant={confirming === 'DELETE' ? 'destructive' : 'ghost'}
                className={cn(confirming !== 'DELETE' && 'text-destructive hover:bg-destructive/10 hover:text-destructive')}
                // The backend refuses to delete a session that still has students (explained below the buttons).
                disabled={busy || session.enrolledStudents > 0}
                onClick={() => void deleteSession()}
              >
                <Trash2 aria-hidden="true" />
                {confirming === 'DELETE' ? 'Confirm: delete session' : 'Delete'}
              </Button>
            )}
            {confirming && (
              <Button variant="ghost" disabled={busy} onClick={() => setConfirming(null)}>
                No
              </Button>
            )}
          </div>
          {deletableStatus && session.enrolledStudents > 0 && !confirming && (
            <p className="text-[13px] text-muted-foreground">
              A session with students cannot be deleted. Remove the students first, or cancel the session.
            </p>
          )}
        </section>
      )}

      <section aria-labelledby="session-students" className="flex flex-col gap-3">
        <h3 id="session-students" className="flex items-center gap-2 font-heading font-medium">
          <Users className="size-4" aria-hidden="true" />
          Students
        </h3>
        {canAddStudents && (
          <AddStudentsForm key={removals} sessionId={sessionId} onAdded={() => void handleStudentsAdded()} />
        )}
        {studentError && (
          <p role="alert" className="flex items-center gap-2 rounded-[14px] bg-[#fef1f0] px-4 py-3 text-sm text-destructive">
            <CircleAlert className="size-4 shrink-0" aria-hidden="true" />
            {studentError}
          </p>
        )}
        {monitor.state.status === 'loading' && <p className="text-sm text-muted-foreground">Loading…</p>}
        {monitor.state.status === 'error' && <p className="text-sm text-destructive">{monitor.state.message}</p>}
        {monitor.state.status === 'ready' &&
          (monitor.state.data.students.length === 0 ? (
            <p className="rounded-[14px] border border-dashed p-4 text-sm text-muted-foreground">
              No students yet. Students join with the code above{canAddStudents ? ', or add them by ID' : ''}.
            </p>
          ) : (
            <ul className="divide-y rounded-[14px] border">
              {monitor.state.data.students.map((s) => (
                <li key={s.sessionEnrollmentId} className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 text-sm">
                  <div className="min-w-0">
                    <p className="truncate font-medium">
                      {s.studentName} <span className="font-normal text-muted-foreground">#{s.studentId}</span>
                    </p>
                    <p className="truncate text-muted-foreground">{s.studentEmail}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <p className="text-muted-foreground">
                      {s.enrolledAt ? `Joined ${formatDateTime(parseApiDate(s.enrolledAt))}` : ''}
                    </p>
                    {notStarted &&
                      (removingId === s.studentId ? (
                        <>
                          <Button size="sm" variant="destructive" disabled={busy} onClick={() => void removeStudent(s.studentId)}>
                            Remove
                          </Button>
                          <Button size="sm" variant="ghost" disabled={busy} onClick={() => setRemovingId(null)}>
                            No
                          </Button>
                        </>
                      ) : (
                        <Button
                          size="icon-sm"
                          variant="ghost"
                          disabled={busy}
                          aria-label={`Remove ${s.studentName} from this session`}
                          title="Remove from this session"
                          onClick={() => setRemovingId(s.studentId)}
                          className="text-muted-foreground hover:text-destructive"
                        >
                          <UserMinus className="size-4" aria-hidden="true" />
                        </Button>
                      ))}
                  </div>
                </li>
              ))}
            </ul>
          ))}
      </section>
    </>
  )
}
