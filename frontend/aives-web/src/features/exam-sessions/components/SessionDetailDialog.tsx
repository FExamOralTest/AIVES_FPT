import { CircleAlert, Play, RotateCw, Square, Users, XCircle } from 'lucide-react'
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

interface SessionDetailDialogProps {
  sessionId: number | null
  onClose: () => void
  /** Called after a status change so the list can reflect it. */
  onChanged: (session: ExamSession) => void
}

export function SessionDetailDialog({ sessionId, onClose, onChanged }: SessionDetailDialogProps) {
  return (
    <Dialog open={sessionId !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-2xl">
        {sessionId !== null && <SessionDetail key={sessionId} sessionId={sessionId} onChanged={onChanged} />}
      </DialogContent>
    </Dialog>
  )
}

const ACTION: Record<SessionStatus, { label: string; confirm?: string; icon: ReactNode; variant: 'default' | 'secondary' }> = {
  IN_PROGRESS: { label: 'Bắt đầu ca thi', icon: <Play aria-hidden="true" />, variant: 'default' },
  COMPLETED: {
    label: 'Kết thúc ca thi',
    confirm: 'Kết thúc ca thi? Sinh viên sẽ không vào thi được nữa. Không thể hoàn tác.',
    icon: <Square aria-hidden="true" />,
    variant: 'default',
  },
  CANCELLED: {
    label: 'Huỷ ca thi',
    confirm: 'Huỷ ca thi? Sinh viên không thể tham gia hay vào thi. Không thể hoàn tác.',
    icon: <XCircle aria-hidden="true" />,
    variant: 'secondary',
  },
  NOT_STARTED: { label: '', icon: null, variant: 'secondary' },
}

function SessionDetail({ sessionId, onChanged }: { sessionId: number; onChanged: SessionDetailDialogProps['onChanged'] }) {
  const detail = useApi(() => sessionsApi.get(sessionId), sessionId, 'Không tải được ca thi.')
  const monitor = useApi(() => sessionsApi.monitor(sessionId), sessionId, 'Không tải được danh sách sinh viên.')
  const [confirming, setConfirming] = useState<SessionStatus | null>(null)
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  if (detail.state.status === 'loading') {
    return (
      <>
        <DialogHeader>
          <DialogTitle>Ca thi</DialogTitle>
          <DialogDescription>Đang tải…</DialogDescription>
        </DialogHeader>
        <div className="h-40 animate-pulse rounded-2xl bg-muted motion-reduce:animate-none" />
      </>
    )
  }
  if (detail.state.status === 'error') {
    return (
      <>
        <DialogHeader>
          <DialogTitle>Ca thi</DialogTitle>
          <DialogDescription className="text-destructive">{detail.state.message}</DialogDescription>
        </DialogHeader>
        <Button variant="secondary" className="w-fit" onClick={detail.reload}>
          <RotateCw aria-hidden="true" />
          Thử lại
        </Button>
      </>
    )
  }

  const session = detail.state.data
  const start = parseApiDate(session.startTime)
  const end = parseApiDate(session.endTime)

  async function changeStatus(target: SessionStatus) {
    if (ACTION[target].confirm && confirming !== target) {
      setConfirming(target)
      return
    }
    setBusy(true)
    setActionError(null)
    try {
      const updated = await sessionsApi.updateStatus(sessionId, target)
      detail.update(() => updated)
      onChanged(updated)
      setConfirming(null)
    } catch (err) {
      setActionError(toApiError(err, 'Không đổi được trạng thái ca thi.').message)
    } finally {
      setBusy(false)
    }
  }

  const transitions = SESSION_TRANSITIONS[session.status]
  const facts = [
    { label: 'Đề thi', value: `${session.examTitle} (${session.examCode})` },
    { label: 'Thời gian', value: formatTimeRange(start, end) },
    { label: 'Phòng thi', value: session.room || 'Chưa có' },
    { label: 'Đã tham gia', value: `${session.enrolledStudents}/${session.maxCapacity} sinh viên` },
  ]

  return (
    <>
      <DialogHeader>
        <span className={cn('w-fit rounded-full px-2.5 py-1 text-xs font-medium', SESSION_STATUS_TONE[session.status])}>
          {SESSION_STATUS_LABEL[session.status]}
        </span>
        <DialogTitle>{session.sessionName}</DialogTitle>
        <DialogDescription>Gửi mã tham gia dưới đây cho sinh viên.</DialogDescription>
      </DialogHeader>

      <div className="flex flex-col items-start gap-3 rounded-2xl bg-brand-soft p-5 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-[13px] text-muted-foreground">Mã tham gia</p>
          <p className="font-mono text-2xl font-semibold tracking-[0.15em] sm:text-3xl">{session.sessionCode}</p>
        </div>
        <CopyButton value={session.sessionCode} label="Sao chép mã" />
      </div>

      <dl className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
        {facts.map((f) => (
          <div key={f.label}>
            <dt className="text-muted-foreground">{f.label}</dt>
            <dd className="mt-0.5 font-medium">{f.value}</dd>
          </div>
        ))}
      </dl>

      {transitions.length > 0 && (
        <section aria-label="Đổi trạng thái" className="flex flex-col gap-3">
          {confirming && (
            <p role="alert" className="rounded-[14px] bg-warning-soft px-4 py-3 text-sm text-warning">
              {ACTION[confirming].confirm}
            </p>
          )}
          {actionError && (
            <p role="alert" className="flex items-center gap-2 rounded-[14px] bg-[#fef1f0] px-4 py-3 text-sm text-destructive">
              <CircleAlert className="size-4 shrink-0" aria-hidden="true" />
              {actionError}
            </p>
          )}
          <div className="flex flex-wrap gap-2">
            {transitions.map((target) => {
              const action = ACTION[target]
              const isConfirming = confirming === target
              return (
                <Button
                  key={target}
                  variant={isConfirming && target === 'CANCELLED' ? 'destructive' : action.variant}
                  disabled={busy}
                  onClick={() => void changeStatus(target)}
                >
                  {action.icon}
                  {isConfirming ? `Xác nhận: ${action.label.toLowerCase()}` : action.label}
                </Button>
              )
            })}
            {confirming && (
              <Button variant="ghost" disabled={busy} onClick={() => setConfirming(null)}>
                Không
              </Button>
            )}
          </div>
        </section>
      )}

      <section aria-labelledby="session-students">
        <h3 id="session-students" className="flex items-center gap-2 font-heading font-medium">
          <Users className="size-4" aria-hidden="true" />
          Sinh viên đã tham gia
        </h3>
        {monitor.state.status === 'loading' && <p className="mt-2 text-sm text-muted-foreground">Đang tải…</p>}
        {monitor.state.status === 'error' && <p className="mt-2 text-sm text-destructive">{monitor.state.message}</p>}
        {monitor.state.status === 'ready' &&
          (monitor.state.data.students.length === 0 ? (
            <p className="mt-2 rounded-[14px] border border-dashed p-4 text-sm text-muted-foreground">
              Chưa có sinh viên nào nhập mã tham gia.
            </p>
          ) : (
            <ul className="mt-3 divide-y rounded-[14px] border">
              {monitor.state.data.students.map((s) => (
                <li key={s.sessionEnrollmentId} className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 text-sm">
                  <div className="min-w-0">
                    <p className="truncate font-medium">{s.studentName}</p>
                    <p className="truncate text-muted-foreground">{s.studentEmail}</p>
                  </div>
                  <p className="text-muted-foreground">
                    {s.enrolledAt ? `Tham gia ${formatDateTime(parseApiDate(s.enrolledAt))}` : ''}
                  </p>
                </li>
              ))}
            </ul>
          ))}
      </section>
    </>
  )
}
