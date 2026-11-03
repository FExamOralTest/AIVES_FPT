import {
  Building,
  Calendar,
  CalendarClock,
  CircleAlert,
  Clock,
  LayoutDashboard,
  PlusCircle,
  RotateCw,
  X,
} from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { PageBar, TableSkeleton, TOOLBAR_BUTTON } from '@/components/common/FlatPage'
import { Button } from '@/components/ui/button'
import { ROLE_HOME, ROLE_LABEL, useAuth } from '@/features/auth'
import type { SessionStatus } from '@/lib/domain/sessionStatus'
import { useApi } from '@/lib/http/useApi'
import { periodsApi } from '../api/periodsApi'
import { sessionsApi } from '../api/sessionsApi'
import { CreateSessionDialog } from '../components/SessionFormDialog'
import { SessionDetailDialog } from '../components/SessionDetailDialog'
import { SessionTable } from '../components/SessionTable'
import type { ExamPeriod, ExamSession, ExamSessionSummary } from '../types'

export default function SessionsPage() {
  const { user } = useAuth()
  const teacherId = user?.userId ?? 0
  const { state, reload, update } = useApi(
    () => sessionsApi.listMine(teacherId),
    teacherId,
    'Could not load exam sessions.'
  )
  const [params, setParams] = useSearchParams()
  // "Create a session for this exam" on an exam links here with ?examId=…, opening the form preselected.
  const linkedExamId = Number(params.get('examId')) || null
  const [creating, setCreating] = useState(linkedExamId !== null)
  const [viewingId, setViewingId] = useState<number | null>(null)
  const [status, setStatus] = useState<SessionStatus | null>(null)

  // Hierarchical Filter states
  const [periods, setPeriods] = useState<ExamPeriod[]>([])
  const [selectedPeriodId, setSelectedPeriodId] = useState<number | 'ALL'>('ALL')
  const [selectedDate, setSelectedDate] = useState<string>('')
  const [selectedRoom, setSelectedRoom] = useState<string>('')

  // Load available periods for filtering
  useEffect(() => {
    let isMounted = true
    periodsApi.list()
      .then((res) => {
        if (isMounted) setPeriods(res)
      })
      .catch(() => {})
    return () => {
      isMounted = false
    }
  }, [])

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

  // Unique rooms from existing sessions
  const uniqueRooms = useMemo(() => {
    const set = new Set<string>()
    for (const s of sessions) {
      if (s.room && s.room.trim()) set.add(s.room.trim())
    }
    return Array.from(set).sort()
  }, [sessions])

  // Hierarchical filtering (Period -> Date -> Room)
  const filteredSessions = useMemo(() => {
    return sessions.filter((s) => {
      if (selectedPeriodId !== 'ALL' && s.periodId !== selectedPeriodId) {
        return false
      }
      if (selectedRoom && s.room?.toLowerCase() !== selectedRoom.toLowerCase()) {
        return false
      }
      if (selectedDate) {
        const sDate = s.startTime.split('T')[0]
        if (sDate !== selectedDate) return false
      }
      return true
    })
  }, [sessions, selectedPeriodId, selectedRoom, selectedDate])

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

      {/* Hierarchical Filter Toolbar: Period -> Date -> Room */}
      {sessions.length > 0 && (
        <div className="flex flex-wrap items-center justify-between gap-3 border-b bg-card/60 px-4 py-3 sm:px-6">
          <div className="flex flex-wrap items-center gap-2.5">
            {/* Period Filter */}
            <div className="flex items-center gap-1.5 rounded-xl border bg-background px-2.5 py-1 text-xs shadow-2xs">
              <Calendar className="size-3.5 text-brand" />
              <span className="font-medium text-muted-foreground">Period:</span>
              <select
                value={selectedPeriodId}
                onChange={(e) =>
                  setSelectedPeriodId(e.target.value === 'ALL' ? 'ALL' : Number(e.target.value))
                }
                className="bg-transparent font-semibold text-foreground outline-none cursor-pointer text-xs"
              >
                <option value="ALL">All Periods ({sessions.length})</option>
                {periods.map((p) => (
                  <option key={p.periodId} value={p.periodId}>
                    {p.periodName} {p.courseName ? `(${p.courseName})` : ''}
                  </option>
                ))}
              </select>
            </div>

            {/* Date Filter */}
            <div className="flex items-center gap-1.5 rounded-xl border bg-background px-2.5 py-1 text-xs shadow-2xs">
              <Clock className="size-3.5 text-brand" />
              <span className="font-medium text-muted-foreground">Date:</span>
              <input
                type="date"
                value={selectedDate}
                onChange={(e) => setSelectedDate(e.target.value)}
                className="bg-transparent font-medium text-foreground outline-none cursor-pointer text-xs"
              />
              {selectedDate && (
                <button
                  type="button"
                  onClick={() => setSelectedDate('')}
                  className="text-muted-foreground hover:text-foreground ml-1"
                  title="Clear date filter"
                >
                  <X className="size-3" />
                </button>
              )}
            </div>

            {/* Room Filter */}
            {uniqueRooms.length > 0 && (
              <div className="flex items-center gap-1.5 rounded-xl border bg-background px-2.5 py-1 text-xs shadow-2xs">
                <Building className="size-3.5 text-brand" />
                <span className="font-medium text-muted-foreground">Room:</span>
                <select
                  value={selectedRoom}
                  onChange={(e) => setSelectedRoom(e.target.value)}
                  className="bg-transparent font-semibold text-foreground outline-none cursor-pointer text-xs"
                >
                  <option value="">All Rooms</option>
                  {uniqueRooms.map((rm) => (
                    <option key={rm} value={rm}>
                      {rm}
                    </option>
                  ))}
                </select>
              </div>
            )}

            {/* Reset Filters button */}
            {(selectedPeriodId !== 'ALL' || selectedDate || selectedRoom) && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => {
                  setSelectedPeriodId('ALL')
                  setSelectedDate('')
                  setSelectedRoom('')
                }}
                className="h-7 px-2 text-xs text-muted-foreground hover:text-foreground"
              >
                Reset filters
              </Button>
            )}
          </div>

          <div className="text-xs text-muted-foreground">
            Showing <strong className="text-foreground">{filteredSessions.length}</strong> of{' '}
            {sessions.length} sessions
          </div>
        </div>
      )}

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
            sessions={filteredSessions}
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

      <CreateSessionDialog
        open={creating}
        initialExamId={linkedExamId}
        onClose={closeCreate}
        onCreated={handleCreated}
      />
      <SessionDetailDialog
        sessionId={viewingId}
        onClose={() => setViewingId(null)}
        onChanged={handleChanged}
        onDeleted={handleDeleted}
      />
    </>
  )
}

function toSummary(s: ExamSession): ExamSessionSummary {
  const {
    sessionId,
    sessionName,
    examTitle,
    examCode,
    room,
    startTime,
    endTime,
    status,
    enrolledStudents,
    periodId,
    periodName,
    courseId,
    courseName,
    exams,
    slots,
  } = s
  return {
    sessionId,
    sessionName,
    examTitle,
    examCode,
    room,
    startTime,
    endTime,
    status,
    enrolledStudents,
    periodId,
    periodName,
    courseId,
    courseName,
    exams,
    slots,
  }
}
