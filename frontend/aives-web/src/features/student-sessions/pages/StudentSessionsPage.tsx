import { CalendarClock, CircleAlert, Clock, KeyRound, MapPin, Mic, RotateCw, Trophy } from 'lucide-react'
import { useEffect, useId, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/features/auth'
import { SESSION_STATUS_LABEL } from '@/lib/domain/sessionStatus'
import { formatTimeRange, parseApiDate } from '@/lib/format/date'
import { useApi } from '@/lib/http/useApi'
import { cn } from '@/lib/utils'
import { studentSessionsApi } from '../api/studentSessionsApi'
import { getStudentSessionState, SESSION_FILTERS, STUDENT_SESSION_STATES, type SessionFilter } from '../lib/sessionState'
import type { StudentSessionEnrollment } from '../types'

export default function StudentSessionsPage() {
  const { user } = useAuth()
  const { state, reload } = useApi(
    () => studentSessionsApi.listMine(), user?.userId ?? 0, 'Could not load your sessions.',
  )
  const [filter, setFilter] = useState<SessionFilter>('all')
  const [search, setSearch] = useState('')
  const [now, setNow] = useState(Date.now)
  const searchId = useId()

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    function refresh() {
      setNow(Date.now())
      reload()
    }
    window.addEventListener('focus', refresh)
    return () => {
      window.clearInterval(timer)
      window.removeEventListener('focus', refresh)
    }
  }, [reload])

  const sessions = state.status === 'ready' ? state.data : []
  const query = search.trim().toLocaleLowerCase('vi-VN')
  const visibleSessions = sessions.filter((session) => {
    const sessionState = getStudentSessionState(session, now)
    const matchesFilter = filter === 'all' || STUDENT_SESSION_STATES[sessionState].filter === filter
    const matchesSearch = `${session.sessionName} ${session.examTitle} ${session.courseName}`.toLocaleLowerCase('vi-VN').includes(query)
    return matchesFilter && matchesSearch
  })

  return (
    <>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-3xl font-medium tracking-tight">My sessions</h1>
          <p className="mt-1.5 max-w-2xl text-muted-foreground">
            Track the sessions you joined, enter the viva room and see your results.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="secondary" onClick={reload} disabled={state.status === 'loading'}>
            <RotateCw aria-hidden="true" />
            Refresh
          </Button>
          <Button asChild>
            <Link to="/student/enroll"><KeyRound aria-hidden="true" />Join a session</Link>
          </Button>
        </div>
      </header>

      <section aria-label="Filter sessions" className="mt-8 space-y-4">
        <div className="max-w-md space-y-2">
          <Label htmlFor={searchId}>Search sessions</Label>
          <input
            id={searchId}
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Session, exam or course name"
            className="h-11 w-full rounded-xl border border-input bg-background px-4 text-sm outline-none focus:border-brand focus:ring-4 focus:ring-brand/15"
          />
        </div>
        <div role="group" aria-label="Session status" className="flex flex-wrap gap-2">
          {SESSION_FILTERS.map((option) => (
            <Button
              key={option.value}
              variant={filter === option.value ? 'default' : 'secondary'}
              size="sm"
              aria-pressed={filter === option.value}
              onClick={() => setFilter(option.value)}
            >
              {option.label}
            </Button>
          ))}
        </div>
      </section>

      <section aria-label="Your sessions" aria-busy={state.status === 'loading'} className="mt-6">
        {state.status === 'loading' && (
          <div role="status" className="space-y-3">
            <span className="sr-only">Loading your sessions…</span>
            {[0, 1, 2].map((i) => (
              <div key={i} className="h-44 animate-pulse rounded-2xl bg-muted motion-reduce:animate-none" />
            ))}
          </div>
        )}
        {state.status === 'error' && (
          <div role="alert" className="flex flex-col items-center gap-3 rounded-2xl border px-6 py-14 text-center">
            <CircleAlert className="size-8 text-destructive" aria-hidden="true" />
            <p className="max-w-sm text-muted-foreground">{state.message}</p>
            <Button variant="secondary" onClick={reload}><RotateCw aria-hidden="true" />Try again</Button>
          </div>
        )}
        {state.status === 'ready' && sessions.length === 0 && (
          <div className="flex flex-col items-center gap-3 rounded-2xl border-2 border-dashed px-6 py-14 text-center">
            <CalendarClock className="size-8 text-muted-foreground" aria-hidden="true" />
            <h2 className="font-heading text-lg font-medium">You have not joined any session yet</h2>
            <p className="max-w-sm text-sm text-muted-foreground">Enter the code from your lecturer to join your first session.</p>
            <Button asChild><Link to="/student/enroll">Join a session</Link></Button>
          </div>
        )}
        {state.status === 'ready' && sessions.length > 0 && (
          <>
            <p role="status" className="mb-3 text-sm text-muted-foreground">Showing {visibleSessions.length} of {sessions.length} sessions</p>
            {visibleSessions.length === 0 ? (
              <div className="rounded-2xl border px-6 py-12 text-center">
                <p className="text-muted-foreground">No sessions match these filters.</p>
                <Button variant="link" onClick={() => { setFilter('all'); setSearch('') }}>Clear filters</Button>
              </div>
            ) : (
              <ul className="space-y-3">
                {visibleSessions.map((session) => (
                  <li key={session.sessionEnrollmentId}><StudentSessionCard session={session} now={now} /></li>
                ))}
              </ul>
            )}
          </>
        )}
      </section>
    </>
  )
}

function StudentSessionCard({ session, now }: { session: StudentSessionEnrollment; now: number }) {
  const state = getStudentSessionState(session, now)
  const display = STUDENT_SESSION_STATES[state]
  const canEnter = state === 'ready' || state === 'inProgress'

  return (
    <article className="rounded-2xl border p-5 sm:p-6">
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-start">
        <div className="min-w-0 flex-1">
          <p className="text-sm text-muted-foreground">{session.courseName}</p>
          <h2 className="mt-1 break-words font-heading text-lg font-medium">{session.sessionName}</h2>
          <div className="mt-1 flex flex-wrap items-center gap-2">
            <span className="break-words text-sm text-muted-foreground">{session.examTitle}</span>
            {session.examCode && (
              <span className="rounded-md bg-brand-soft px-2 py-0.5 font-mono text-xs font-semibold text-brand">
                {session.examCode}
              </span>
            )}
          </div>
        </div>
        <span className={cn('w-fit shrink-0 rounded-full px-3 py-1 text-xs font-medium', display.tone)}>{display.label}</span>
      </div>
      {session.retakeId && <div className="mt-3 rounded-xl bg-brand-soft p-3 text-sm"><strong>Bạn được phép thi lại</strong><p>{session.retakeReason}</p><p>Thời lượng: {session.retakeDurationMinutes} phút · Thay thế bài #{session.originalResultId}</p></div>}
      {session.replacementSessionId && <p className="mt-3 text-sm">Bài này đã hủy hiệu lực do sự cố. <Link className="text-brand underline" to={`/student/viva?sessionId=${session.replacementSessionId}`}>Xem ca thi bù được cấp</Link></p>}
      <div className="mt-4 space-y-2 text-sm text-muted-foreground">
        {session.assignedSlotName && (
          <div className="flex items-center gap-2 text-xs font-semibold text-brand bg-brand-soft px-3 py-1.5 rounded-xl w-fit">
            <Clock className="size-3.5 shrink-0" aria-hidden="true" />
            <span>
              Kíp thi của bạn: {session.assignedSlotName}{' '}
              {session.slotStartTime && session.slotEndTime
                ? `(${formatTimeRange(parseApiDate(session.slotStartTime), parseApiDate(session.slotEndTime))})`
                : ''}
            </span>
          </div>
        )}
        <p className="flex items-start gap-2">
          <CalendarClock className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          Ca thi: {formatTimeRange(parseApiDate(session.startTime), parseApiDate(session.endTime))}
        </p>
        {session.room && <p className="flex items-center gap-2"><MapPin className="size-4 shrink-0" aria-hidden="true" />{session.room}</p>}
        <p>Session: {SESSION_STATUS_LABEL[session.sessionStatus]} · Your exam: {SESSION_STATUS_LABEL[session.enrollmentStatus]}</p>
      </div>
      <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t pt-4">
        <p className="text-sm text-muted-foreground">
          {state === 'waiting' && (
            session.slotStartTime && now < parseApiDate(session.slotStartTime).getTime()
              ? `Chưa đến khung giờ thi của bạn (${session.assignedSlotName ?? 'Kíp thi'}: ${formatTimeRange(parseApiDate(session.slotStartTime), parseApiDate(session.slotEndTime || session.endTime))}). Vui lòng quay lại đúng giờ.`
              : now < parseApiDate(session.startTime).getTime()
                ? 'Chưa đến giờ thi. Vui lòng quay lại khi ca thi bắt đầu.'
                : 'Đang chờ giảng viên mở ca thi. Nhấn Làm mới để cập nhật.'
          )}
          {state === 'ready' && 'Get your microphone ready and find a quiet place before you start.'}
          {state === 'inProgress' && 'Continue the exam you started.'}
          {state === 'completed' && 'Exam completed. Please wait for your result to be published.'}
          {state === 'published' && 'Your score and feedback have been published.'}
          {state === 'closed' && 'This session is closed; you can no longer enter the viva room.'}
          {state === 'cancelled' && 'The session or your registration was cancelled. Contact your lecturer if you need help.'}
        </p>
        {canEnter && (
          <Button asChild><Link to={`/student/viva?sessionId=${session.sessionId}`}><Mic aria-hidden="true" />{state === 'inProgress' ? 'Continue exam' : 'Start exam'}</Link></Button>
        )}
        {state === 'published' && (
          <Button asChild><Link to={`/student/results?sessionId=${session.sessionId}`}><Trophy aria-hidden="true" />View result</Link></Button>
        )}
      </div>
    </article>
  )
}
