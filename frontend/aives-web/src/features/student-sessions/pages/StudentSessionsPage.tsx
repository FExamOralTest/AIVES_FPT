import { CalendarClock, CircleAlert, KeyRound, MapPin, Mic, RotateCw, Trophy } from 'lucide-react'
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
    () => studentSessionsApi.listMine(), user?.userId ?? 0, 'Không tải được danh sách ca thi đã đăng ký.',
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
          <h1 className="font-heading text-3xl font-medium tracking-tight">Ca thi của tôi</h1>
          <p className="mt-1.5 max-w-2xl text-muted-foreground">
            Theo dõi các ca thi đã đăng ký, vào phòng thi và xem kết quả của bạn.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="secondary" onClick={reload} disabled={state.status === 'loading'}>
            <RotateCw aria-hidden="true" />
            Làm mới
          </Button>
          <Button asChild>
            <Link to="/student/enroll"><KeyRound aria-hidden="true" />Tham gia ca thi</Link>
          </Button>
        </div>
      </header>

      <section aria-label="Lọc ca thi" className="mt-8 space-y-4">
        <div className="max-w-md space-y-2">
          <Label htmlFor={searchId}>Tìm ca thi</Label>
          <input
            id={searchId}
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Tên ca thi, đề thi hoặc môn học"
            className="h-11 w-full rounded-xl border border-input bg-background px-4 text-sm outline-none focus:border-brand focus:ring-4 focus:ring-brand/15"
          />
        </div>
        <div role="group" aria-label="Trạng thái ca thi" className="flex flex-wrap gap-2">
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

      <section aria-label="Danh sách ca thi đã đăng ký" aria-busy={state.status === 'loading'} className="mt-6">
        {state.status === 'loading' && (
          <div role="status" className="space-y-3">
            <span className="sr-only">Đang tải danh sách ca thi…</span>
            {[0, 1, 2].map((i) => (
              <div key={i} className="h-44 animate-pulse rounded-2xl bg-muted motion-reduce:animate-none" />
            ))}
          </div>
        )}
        {state.status === 'error' && (
          <div role="alert" className="flex flex-col items-center gap-3 rounded-2xl border px-6 py-14 text-center">
            <CircleAlert className="size-8 text-destructive" aria-hidden="true" />
            <p className="max-w-sm text-muted-foreground">{state.message}</p>
            <Button variant="secondary" onClick={reload}><RotateCw aria-hidden="true" />Thử lại</Button>
          </div>
        )}
        {state.status === 'ready' && sessions.length === 0 && (
          <div className="flex flex-col items-center gap-3 rounded-2xl border-2 border-dashed px-6 py-14 text-center">
            <CalendarClock className="size-8 text-muted-foreground" aria-hidden="true" />
            <h2 className="font-heading text-lg font-medium">Bạn chưa đăng ký ca thi nào</h2>
            <p className="max-w-sm text-sm text-muted-foreground">Nhập mã giảng viên cung cấp để tham gia ca thi đầu tiên.</p>
            <Button asChild><Link to="/student/enroll">Tham gia ca thi</Link></Button>
          </div>
        )}
        {state.status === 'ready' && sessions.length > 0 && (
          <>
            <p role="status" className="mb-3 text-sm text-muted-foreground">Hiển thị {visibleSessions.length} / {sessions.length} ca thi</p>
            {visibleSessions.length === 0 ? (
              <div className="rounded-2xl border px-6 py-12 text-center">
                <p className="text-muted-foreground">Không có ca thi phù hợp với bộ lọc.</p>
                <Button variant="link" onClick={() => { setFilter('all'); setSearch('') }}>Xóa bộ lọc</Button>
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
          <p className="mt-1 break-words text-sm text-muted-foreground">{session.examTitle}</p>
        </div>
        <span className={cn('w-fit shrink-0 rounded-full px-3 py-1 text-xs font-medium', display.tone)}>{display.label}</span>
      </div>
      <div className="mt-4 space-y-2 text-sm text-muted-foreground">
        <p className="flex items-start gap-2">
          <CalendarClock className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          {formatTimeRange(parseApiDate(session.startTime), parseApiDate(session.endTime))}
        </p>
        {session.room && <p className="flex items-center gap-2"><MapPin className="size-4 shrink-0" aria-hidden="true" />{session.room}</p>}
        <p>Ca thi: {SESSION_STATUS_LABEL[session.sessionStatus]} · Bài thi: {SESSION_STATUS_LABEL[session.enrollmentStatus]}</p>
      </div>
      <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t pt-4">
        <p className="text-sm text-muted-foreground">
          {state === 'waiting' && (now < parseApiDate(session.startTime).getTime()
            ? 'Chưa đến giờ thi. Bạn có thể quay lại khi ca thi bắt đầu.'
            : 'Đang chờ giảng viên mở ca thi. Nhấn Làm mới để cập nhật.')}
          {state === 'ready' && 'Chuẩn bị micro và nơi yên tĩnh trước khi vào thi.'}
          {state === 'inProgress' && 'Tiếp tục bài thi bạn đang làm.'}
          {state === 'completed' && 'Bài thi đã hoàn thành. Vui lòng đợi giảng viên công bố kết quả.'}
          {state === 'published' && 'Điểm và nhận xét của bạn đã được công bố.'}
          {state === 'closed' && 'Ca thi đã đóng, bạn không thể vào phòng thi.'}
          {state === 'cancelled' && 'Ca thi hoặc đăng ký đã bị hủy. Liên hệ giảng viên nếu cần hỗ trợ.'}
        </p>
        {canEnter && (
          <Button asChild><Link to={`/student/viva?sessionId=${session.sessionId}`}><Mic aria-hidden="true" />{state === 'inProgress' ? 'Tiếp tục thi' : 'Vào thi'}</Link></Button>
        )}
        {state === 'published' && (
          <Button asChild><Link to={`/student/results?sessionId=${session.sessionId}`}><Trophy aria-hidden="true" />Xem kết quả</Link></Button>
        )}
      </div>
    </article>
  )
}
