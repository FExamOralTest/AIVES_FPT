import {
  Check,
  CircleAlert,
  Clock,
  Info,
  Layers,
  Pencil,
  Play,
  RotateCw,
  Shuffle,
  Sparkles,
  Square,
  Trash2,
  UserMinus,
  Users,
  XCircle,
} from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { CopyButton } from '@/components/common/CopyButton'
import { Alert, AlertDescription } from '@/components/ui/alert'
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
import type { DistributeExamsResponse, DistributeSlotsResponse, ExamSession } from '../types'
import { AddStudentsForm } from './AddStudentsForm'
import { ConfigureSlotsDialog } from './ConfigureSlotsDialog'
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
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto p-0">
        {sessionId !== null && (
          <SessionDetail key={sessionId} sessionId={sessionId} onChanged={onChanged} onDeleted={onDeleted} />
        )}
      </DialogContent>
    </Dialog>
  )
}

const ACTION: Record<SessionStatus, { label: string; confirm?: string; icon: ReactNode; variant: 'default' | 'secondary' }> = {
  IN_PROGRESS: { label: 'Bắt đầu ca thi', icon: <Play className="size-4" aria-hidden="true" />, variant: 'default' },
  COMPLETED: {
    label: 'Kết thúc ca thi',
    confirm: 'Kết thúc ca thi này? Sinh viên sẽ không thể làm bài thêm. Thao tác này không thể hoàn tác.',
    icon: <Square className="size-4" aria-hidden="true" />,
    variant: 'default',
  },
  CANCELLED: {
    label: 'Hủy ca thi',
    confirm: 'Hủy ca thi này? Sinh viên sẽ không thể vào phòng thi. Thao tác này không thể hoàn tác.',
    icon: <XCircle className="size-4" aria-hidden="true" />,
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
  const [activeTab, setActiveTab] = useState<'OVERVIEW' | 'STUDENTS'>('OVERVIEW')
  const [confirming, setConfirming] = useState<SessionStatus | 'DELETE' | null>(null)
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)
  const [editing, setEditing] = useState(false)
  const [removingId, setRemovingId] = useState<number | null>(null)
  const [removals, setRemovals] = useState(0)
  const [studentError, setStudentError] = useState<string | null>(null)

  // Multi-exam distribution state
  const [distributing, setDistributing] = useState(false)
  const [distributeResult, setDistributeResult] = useState<DistributeExamsResponse | null>(null)
  const [reassigningStudentId, setReassigningStudentId] = useState<number | null>(null)
  const [showConfigureSlots, setShowConfigureSlots] = useState(false)

  // Time slots distribution state
  const [distributingSlots, setDistributingSlots] = useState(false)
  const [distributeSlotsResult, setDistributeSlotsResult] = useState<DistributeSlotsResponse | null>(null)
  const [slotExamMode, setSlotExamMode] = useState<'EXAM_PER_SLOT' | 'SHUFFLE_IN_SLOT'>('EXAM_PER_SLOT')
  const [selectedSlotFilter, setSelectedSlotFilter] = useState<number | 'ALL'>('ALL')
  const [reassigningSlotStudentId, setReassigningSlotStudentId] = useState<number | null>(null)

  if (detail.state.status === 'loading') {
    return (
      <div className="p-6 space-y-4">
        <DialogHeader>
          <DialogTitle>Ca thi</DialogTitle>
          <DialogDescription>Đang tải thông tin…</DialogDescription>
        </DialogHeader>
        <div className="h-44 animate-pulse rounded-2xl bg-muted motion-reduce:animate-none" />
      </div>
    )
  }
  if (detail.state.status === 'error') {
    return (
      <div className="p-6 space-y-4">
        <DialogHeader>
          <DialogTitle>Ca thi</DialogTitle>
          <DialogDescription className="text-destructive">{detail.state.message}</DialogDescription>
        </DialogHeader>
        <Button variant="secondary" className="w-fit" onClick={detail.reload}>
          <RotateCw className="size-4 mr-2" aria-hidden="true" />
          Thử lại
        </Button>
      </div>
    )
  }

  const session = detail.state.data
  const start = parseApiDate(session.startTime)
  const end = parseApiDate(session.endTime)
  const exams = session.exams ?? []
  const slots = session.slots ?? []
  const hasMultipleExams = exams.length > 1

  function applyUpdate(updated: ExamSession) {
    detail.update(() => updated)
    onChanged(updated)
  }

  if (editing) {
    return (
      <div className="p-6">
        <SessionForm
          session={session}
          onCancel={() => setEditing(false)}
          onSaved={(updated) => {
            applyUpdate(updated)
            setEditing(false)
            monitor.reload()
          }}
        />
      </div>
    )
  }

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
      setActionError(toApiError(err, 'Không thể xóa ca thi.').message)
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
      setStudentError(toApiError(err, 'Không thể gỡ sinh viên khỏi ca thi.').message)
    } finally {
      setBusy(false)
    }
  }

  async function handleStudentsAdded() {
    monitor.reload()
    try {
      applyUpdate(await sessionsApi.get(sessionId))
    } catch {
      detail.reload()
    }
  }

  async function handleDistributeExams() {
    setDistributing(true)
    setActionError(null)
    setDistributeResult(null)
    try {
      const res = await sessionsApi.distributeExams(sessionId, { strategy: 'BALANCED_RANDOM' })
      setDistributeResult(res)
      monitor.reload()
      const updated = await sessionsApi.get(sessionId)
      applyUpdate(updated)
    } catch (err) {
      setActionError(toApiError(err, 'Không thể phân bổ đề thi.').message)
    } finally {
      setDistributing(false)
    }
  }

  async function handleAssignStudentExam(studentId: number, examId: number) {
    setReassigningStudentId(studentId)
    setStudentError(null)
    try {
      await sessionsApi.assignStudentExam(sessionId, studentId, examId)
      const targetExam = exams.find((e) => e.examId === examId)
      monitor.update((m) => ({
        ...m,
        students: m.students.map((st) =>
          st.studentId === studentId
            ? {
                ...st,
                assignedExamId: examId,
                assignedExamCode: targetExam?.examCode ?? st.assignedExamCode,
                assignedExamTitle: targetExam?.title ?? st.assignedExamTitle,
              }
            : st,
        ),
      }))
      const updated = await sessionsApi.get(sessionId)
      applyUpdate(updated)
    } catch (err) {
      setStudentError(toApiError(err, 'Không thể gán đề thi cho sinh viên.').message)
    } finally {
      setReassigningStudentId(null)
    }
  }

  async function handleDistributeSlots() {
    setDistributingSlots(true)
    setActionError(null)
    setStudentError(null)
    try {
      const res = await sessionsApi.distributeSlots(sessionId, {
        strategy: 'SLOT_BALANCED',
        examAssignmentMode: slotExamMode,
      })
      setDistributeSlotsResult(res)
      monitor.reload()
      const updated = await sessionsApi.get(sessionId)
      applyUpdate(updated)
    } catch (err) {
      setActionError(toApiError(err, 'Không thể phân bổ kíp thi.').message)
    } finally {
      setDistributingSlots(false)
    }
  }

  async function handleAssignStudentSlot(studentId: number, slotId: number) {
    setReassigningSlotStudentId(studentId)
    setStudentError(null)
    try {
      await sessionsApi.assignStudentSlot(sessionId, studentId, slotId)
      const targetSlot = slots.find((sl) => sl.slotId === slotId)
      monitor.update((m) => ({
        ...m,
        students: m.students.map((st) =>
          st.studentId === studentId
            ? {
                ...st,
                assignedSlotId: slotId,
                assignedSlotName: targetSlot?.slotName ?? st.assignedSlotName,
                slotStartTime: targetSlot?.startTime ?? st.slotStartTime,
                slotEndTime: targetSlot?.endTime ?? st.slotEndTime,
              }
            : st,
        ),
      }))
      const updated = await sessionsApi.get(sessionId)
      applyUpdate(updated)
    } catch (err) {
      setStudentError(toApiError(err, 'Không thể gán kíp thi cho sinh viên.').message)
    } finally {
      setReassigningSlotStudentId(null)
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
      setActionError(toApiError(err, 'Không thể chuyển trạng thái ca thi.').message)
    } finally {
      setBusy(false)
    }
  }

  const transitions = SESSION_TRANSITIONS[session.status]
  const facts = [
    { label: 'Thời gian thi', value: formatTimeRange(start, end) },
    { label: 'Phòng thi', value: session.room || 'Chưa thiết lập' },
    {
      label: 'Sĩ số đăng ký',
      value: `${session.enrolledStudents} / ${session.maxCapacity} sinh viên`,
    },
    { label: 'Môn học', value: session.courseName || 'Chưa phân môn' },
    ...(session.periodName ? [{ label: 'Đợt thi', value: session.periodName }] : []),
  ]

  return (
    <div className="flex flex-col">
      {/* Modal Header */}
      <div className="border-b px-6 py-4">
        <div className="flex items-center gap-2">
          <span className={cn('rounded-full px-2.5 py-0.5 text-xs font-semibold', SESSION_STATUS_TONE[session.status])}>
            {SESSION_STATUS_LABEL[session.status]}
          </span>
          {hasMultipleExams && (
            <span className="rounded-full bg-brand-soft px-2.5 py-0.5 text-xs font-medium text-brand">
              {exams.length} bộ đề thi
            </span>
          )}
          {slots.length > 0 && (
            <span className="rounded-full bg-purple-50 text-purple-700 dark:bg-purple-950/40 dark:text-purple-300 border border-purple-200 px-2.5 py-0.5 text-xs font-medium">
              {slots.length} kíp thi
            </span>
          )}
        </div>
        <DialogTitle className="mt-1.5 text-xl font-bold">{session.sessionName}</DialogTitle>
      </div>

      {/* 2-Tab Navigation Bar */}
      <div className="flex border-b bg-muted/20 px-6">
        <button
          type="button"
          onClick={() => setActiveTab('OVERVIEW')}
          className={cn(
            'flex items-center gap-2 border-b-2 px-4 py-3 text-sm font-medium transition-all outline-none',
            activeTab === 'OVERVIEW'
              ? 'border-brand text-brand font-semibold bg-background -mb-px'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          )}
        >
          <Info className="size-4" />
          Tổng quan & Ca thi
        </button>
        <button
          type="button"
          onClick={() => setActiveTab('STUDENTS')}
          className={cn(
            'flex items-center gap-2 border-b-2 px-4 py-3 text-sm font-medium transition-all outline-none',
            activeTab === 'STUDENTS'
              ? 'border-brand text-brand font-semibold bg-background -mb-px'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          )}
        >
          <Users className="size-4" />
          Thí sinh & Phân bổ
          <span
            className={cn(
              'rounded-full px-2 py-0.5 text-xs font-semibold',
              activeTab === 'STUDENTS' ? 'bg-brand text-white' : 'bg-muted text-muted-foreground'
            )}
          >
            {session.enrolledStudents}
          </span>
        </button>
      </div>

      {/* Content Area */}
      <div className="p-6 space-y-5">
        {/* ========================================================================= */}
        {/* TAB 1: TỔNG QUAN & CA THI */}
        {/* ========================================================================= */}
        {activeTab === 'OVERVIEW' && (
          <div className="space-y-5">
            {/* Join code banner */}
            <div className="flex flex-col items-start gap-3 rounded-2xl bg-brand-soft/80 border border-brand/20 p-5 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="text-xs font-medium text-brand/80">Mã ca thi (Join Code dành cho sinh viên)</p>
                <p className="font-mono text-2xl font-bold tracking-[0.15em] sm:text-3xl text-brand mt-0.5">
                  {session.sessionCode}
                </p>
              </div>
              <CopyButton value={session.sessionCode} label="Sao chép mã" />
            </div>

            {/* Basic Facts Card */}
            <div className="rounded-2xl border bg-card p-4">
              <h4 className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-3">
                Thông tin chung
              </h4>
              <dl className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
                {facts.map((f) => (
                  <div key={f.label} className="flex flex-col">
                    <dt className="text-xs text-muted-foreground">{f.label}</dt>
                    <dd className="mt-0.5 font-medium text-foreground">{f.value}</dd>
                  </div>
                ))}
              </dl>
            </div>

            {/* Multi-exam list section */}
            <section className="rounded-2xl border p-4 bg-muted/10 space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Layers className="size-4 text-brand" />
                  <h4 className="font-semibold text-sm">
                    Bộ đề thi cấu hình ({exams.length > 0 ? exams.length : 1})
                  </h4>
                </div>
              </div>

              {exams.length > 0 ? (
                <div className="grid gap-2.5 sm:grid-cols-2">
                  {exams.map((ex) => (
                    <div key={ex.examId} className="flex items-center justify-between rounded-xl border bg-background p-3 text-xs shadow-xs">
                      <div className="min-w-0 pr-2">
                        <div className="flex items-center gap-1.5">
                          <span className="font-mono font-bold text-brand">{ex.examCode}</span>
                          <span className="truncate font-medium text-foreground">{ex.title}</span>
                        </div>
                        <p className="text-muted-foreground mt-0.5">
                          {ex.totalQuestions} câu · {ex.durationMinutes} phút
                        </p>
                      </div>
                      <div className="shrink-0 text-right">
                        <span className="rounded-full bg-brand-soft px-2.5 py-0.5 font-semibold text-brand text-[11px]">
                          {ex.assignedStudentsCount} SV
                        </span>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-xs text-muted-foreground">
                  Đề thi: <strong>{session.examTitle}</strong> ({session.examCode})
                </p>
              )}
            </section>

            {/* Time Slots (Kíp thi) Section */}
            {slots.length > 0 ? (
              <section aria-labelledby="session-slots" className="flex flex-col gap-3 rounded-2xl border p-4 bg-muted/10">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <Clock className="size-4 text-brand" aria-hidden="true" />
                    <h4 id="session-slots" className="font-semibold text-sm">
                      Cấu hình Kíp thi ({slots.length} kíp)
                    </h4>
                    <span className="rounded-full bg-brand-soft px-2.5 py-0.5 text-xs font-semibold text-brand">
                      {session.durationMinutes} phút/kíp
                    </span>
                  </div>
                  {notStarted && (
                    <Button
                      type="button"
                      size="sm"
                      variant="ghost"
                      onClick={() => setShowConfigureSlots(true)}
                      className="h-7 text-xs text-brand hover:bg-brand-soft gap-1"
                    >
                      <Sparkles className="size-3.5" />
                      Cấu hình lại kíp
                    </Button>
                  )}
                </div>

                <div className="grid gap-2.5 sm:grid-cols-2 md:grid-cols-3">
                  {slots.map((sl) => {
                    const now = Date.now()
                    const slotStart = parseApiDate(sl.startTime).getTime()
                    const slotEnd = parseApiDate(sl.endTime).getTime()
                    const isHappening = now >= slotStart && now <= slotEnd
                    const isPassed = now > slotEnd

                    return (
                      <div
                        key={sl.slotId}
                        className={cn(
                          'flex items-center justify-between rounded-xl border p-3 text-xs shadow-xs transition-all',
                          isHappening
                            ? 'border-emerald-300 bg-emerald-50/70 dark:bg-emerald-950/30 ring-2 ring-emerald-500/20'
                            : isPassed
                            ? 'border-border/60 bg-muted/40 opacity-75'
                            : 'border-input bg-card'
                        )}
                      >
                        <div className="min-w-0 pr-2">
                          <div className="flex items-center gap-1.5">
                            <span className="font-bold text-foreground">{sl.slotName}</span>
                            {isHappening && (
                              <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 dark:bg-emerald-900/60 text-emerald-800 dark:text-emerald-200 px-1.5 py-0.2 text-[10px] font-semibold animate-pulse">
                                <span className="size-1.5 rounded-full bg-emerald-600" />
                                Đang thi
                              </span>
                            )}
                            {isPassed && (
                              <span className="rounded-full bg-muted text-muted-foreground px-1.5 py-0.2 text-[10px]">
                                Đã xong
                              </span>
                            )}
                          </div>
                          <p className="text-brand font-mono font-medium mt-1">
                            {formatTimeRange(parseApiDate(sl.startTime), parseApiDate(sl.endTime))}
                          </p>
                        </div>
                        <div className="shrink-0 text-right">
                          <span className="rounded-full bg-brand-soft px-2.5 py-0.5 font-semibold text-brand text-[11px]">
                            {sl.assignedStudentsCount ?? 0} SV
                          </span>
                        </div>
                      </div>
                    )
                  })}
                </div>
              </section>
            ) : (
              /* Invitation card when no slots exist */
              <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 rounded-2xl border border-dashed border-brand/40 bg-brand-soft/20 p-4">
                <div className="flex items-start gap-3">
                  <span className="flex size-9 items-center justify-center rounded-xl bg-brand-soft text-brand shrink-0">
                    <Clock className="size-4.5" />
                  </span>
                  <div>
                    <h4 className="text-sm font-semibold text-foreground">Ca thi chưa chia Kíp thi</h4>
                    <p className="text-xs text-muted-foreground mt-0.5">
                      Chia ca thi thành nhiều khung giờ nhỏ để chia đợt sinh viên vào thi, kiểm soát phòng thi và chống lộ đề.
                    </p>
                  </div>
                </div>
                {notStarted && (
                  <Button
                    type="button"
                    size="sm"
                    onClick={() => setShowConfigureSlots(true)}
                    className="shrink-0 gap-1.5 text-xs shadow-xs"
                  >
                    <Sparkles className="size-3.5" />
                    Thiết lập Kíp thi
                  </Button>
                )}
              </div>
            )}

            {/* Action buttons control bar */}
            {(transitions.length > 0 || notStarted || deletableStatus) && (
              <div className="flex flex-col gap-3 pt-2 border-t">
                {confirming && (
                  <p role="alert" className="rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 px-4 py-3 text-xs text-amber-900 dark:text-amber-200">
                    {confirming === 'DELETE'
                      ? 'Xác nhận xóa ca thi này vĩnh viễn? Mã ca thi sẽ bị hủy. Thao tác không thể hoàn tác.'
                      : ACTION[confirming].confirm}
                  </p>
                )}
                {actionError && (
                  <p role="alert" className="flex items-center gap-2 rounded-xl bg-destructive/10 px-4 py-3 text-xs text-destructive">
                    <CircleAlert className="size-4 shrink-0" aria-hidden="true" />
                    {actionError}
                  </p>
                )}
                <div className="flex flex-wrap items-center gap-2.5">
                  {notStarted && !confirming && (
                    <Button variant="outline" size="sm" disabled={busy} onClick={() => setEditing(true)}>
                      <Pencil className="size-3.5 mr-1.5" aria-hidden="true" />
                      Chỉnh sửa ca thi
                    </Button>
                  )}
                  {transitions.map((target) => {
                    const action = ACTION[target]
                    const isConfirming = confirming === target
                    if (confirming && !isConfirming) return null
                    return (
                      <Button
                        key={target}
                        size="sm"
                        variant={isConfirming && target === 'CANCELLED' ? 'destructive' : action.variant}
                        disabled={busy}
                        onClick={() => void changeStatus(target)}
                      >
                        {action.icon}
                        <span className="ml-1.5">
                          {isConfirming ? `Xác nhận: ${action.label.toLowerCase()}` : action.label}
                        </span>
                      </Button>
                    )
                  })}
                  {deletableStatus && (!confirming || confirming === 'DELETE') && (
                    <Button
                      size="sm"
                      variant={confirming === 'DELETE' ? 'destructive' : 'ghost'}
                      className={cn(confirming !== 'DELETE' && 'text-destructive hover:bg-destructive/10 hover:text-destructive')}
                      disabled={busy || session.enrolledStudents > 0}
                      onClick={() => void deleteSession()}
                    >
                      <Trash2 className="size-3.5 mr-1.5" aria-hidden="true" />
                      {confirming === 'DELETE' ? 'Xác nhận xóa ca thi' : 'Xóa ca thi'}
                    </Button>
                  )}
                  {confirming && (
                    <Button size="sm" variant="ghost" disabled={busy} onClick={() => setConfirming(null)}>
                      Hủy thao tác
                    </Button>
                  )}
                </div>
                {deletableStatus && session.enrolledStudents > 0 && !confirming && (
                  <p className="text-[12px] text-muted-foreground">
                    Ca thi đã có sinh viên đăng ký không thể xóa. Hãy gỡ toàn bộ sinh viên trước hoặc hủy ca thi.
                  </p>
                )}
              </div>
            )}
          </div>
        )}

        {/* ========================================================================= */}
        {/* TAB 2: THÍ SINH & PHÂN BỔ */}
        {/* ========================================================================= */}
        {activeTab === 'STUDENTS' && (
          <div className="space-y-4">
            {/* Student Enrollment Tools */}
            {canAddStudents && (
              <AddStudentsForm key={removals} sessionId={sessionId} onAdded={() => void handleStudentsAdded()} />
            )}

            {/* Distribution Toolbar */}
            {notStarted && (
              <div className="flex flex-wrap items-center justify-between gap-2.5 rounded-2xl border bg-muted/20 p-3.5">
                <div className="flex items-center gap-2">
                  <Shuffle className="size-4 text-brand" />
                  <span className="text-xs font-semibold">Công cụ trộn & phân bổ:</span>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  {/* Smart Slot Slicer Button */}
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => setShowConfigureSlots(true)}
                    className="h-8 text-xs border-brand text-brand hover:bg-brand-soft gap-1.5"
                  >
                    <Sparkles className="size-3.5" />
                    {slots.length > 0 ? 'Chia lại Kíp thi' : '⚡ Chia Kíp thi tự động'}
                  </Button>

                  {/* Multi-exam distribution button */}
                  {exams.length > 1 && session.enrolledStudents > 0 && (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={distributing || busy}
                      onClick={handleDistributeExams}
                      className="h-8 text-xs border-brand text-brand hover:bg-brand-soft"
                    >
                      <Shuffle className={cn('size-3.5 mr-1.5', distributing && 'animate-spin')} />
                      {distributing ? 'Đang phân bổ đề…' : 'Trộn & Phân bổ đề thi'}
                    </Button>
                  )}

                  {/* Existing slot distribution */}
                  {slots.length > 0 && session.enrolledStudents > 0 && (
                    <div className="flex items-center gap-1.5">
                      {exams.length > 1 && (
                        <select
                          value={slotExamMode}
                          onChange={(e) => setSlotExamMode(e.target.value as 'EXAM_PER_SLOT' | 'SHUFFLE_IN_SLOT')}
                          className="h-8 rounded-lg border border-input bg-background px-2 text-xs font-medium outline-none focus:border-brand"
                          aria-label="Chế độ chia đề thi cho kíp"
                        >
                          <option value="EXAM_PER_SLOT">Mỗi kíp 1 đề (Cố định)</option>
                          <option value="SHUFFLE_IN_SLOT">Trộn đề trong từng kíp</option>
                        </select>
                      )}
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={distributingSlots || busy}
                        onClick={handleDistributeSlots}
                        className="h-8 text-xs border-brand text-brand hover:bg-brand-soft"
                      >
                        <Shuffle className={cn('size-3.5 mr-1.5', distributingSlots && 'animate-spin')} />
                        {distributingSlots ? 'Đang phân bổ kíp…' : 'Phân bổ SV vào kíp'}
                      </Button>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Distribution Alerts */}
            {distributeResult && (
              <Alert className="bg-emerald-50 text-emerald-900 border-emerald-200 py-2.5">
                <Check className="size-4 text-emerald-600" />
                <AlertDescription className="text-xs">
                  Đã phân bổ thành công <strong>{distributeResult.totalStudents}</strong> sinh viên cho <strong>{distributeResult.totalExams}</strong> bộ đề ngẫu nhiên cân bằng:
                  <span className="mt-1 flex flex-wrap gap-1.5 font-mono">
                    {distributeResult.allocations.map((a) => (
                      <span key={a.examId} className="bg-emerald-100/80 px-2 py-0.5 rounded text-[11px]">
                        {a.examCode}: {a.studentCount} SV
                      </span>
                    ))}
                  </span>
                </AlertDescription>
              </Alert>
            )}

            {distributeSlotsResult && (
              <Alert className="bg-emerald-50 text-emerald-900 border-emerald-200 py-2.5">
                <Check className="size-4 text-emerald-600" />
                <AlertDescription className="text-xs">
                  Đã phân bổ thành công <strong>{distributeSlotsResult.totalStudents}</strong> sinh viên đều cho <strong>{distributeSlotsResult.totalSlots}</strong> kíp thi:
                  <span className="mt-1 flex flex-wrap gap-1.5 font-mono">
                    {distributeSlotsResult.allocations.map((a) => (
                      <span key={a.slotId} className="bg-emerald-100/80 px-2 py-0.5 rounded text-[11px]">
                        {a.slotName}: {a.studentCount} SV
                      </span>
                    ))}
                  </span>
                </AlertDescription>
              </Alert>
            )}

            {studentError && (
              <p role="alert" className="flex items-center gap-2 rounded-xl bg-destructive/10 px-3 py-2 text-xs text-destructive">
                <CircleAlert className="size-4 shrink-0" aria-hidden="true" />
                {studentError}
              </p>
            )}

            {/* Student list states */}
            {monitor.state.status === 'loading' && (
              <p className="text-xs text-muted-foreground">Đang tải danh sách thí sinh…</p>
            )}
            {monitor.state.status === 'error' && (
              <p className="text-xs text-destructive">{monitor.state.message}</p>
            )}

            {monitor.state.status === 'ready' && (() => {
              const allStudents = monitor.state.data.students
              if (allStudents.length === 0) {
                return (
                  <p className="rounded-2xl border border-dashed p-6 text-center text-xs text-muted-foreground">
                    Chưa có sinh viên nào trong ca thi. Hãy nhập MSSV/Email ở trên hoặc sinh viên có thể tự vào bằng Mã ca thi.
                  </p>
                )
              }

              const filteredStudents = allStudents.filter(
                (s) => selectedSlotFilter === 'ALL' || s.assignedSlotId === selectedSlotFilter
              )

              return (
                <div className="space-y-2.5">
                  {/* Slot Filter Tabs */}
                  {slots.length > 0 && (
                    <div className="flex flex-wrap items-center gap-1.5 pb-1">
                      <button
                        type="button"
                        onClick={() => setSelectedSlotFilter('ALL')}
                        className={cn(
                          'h-7 px-2.5 rounded-lg text-xs font-medium transition-colors border',
                          selectedSlotFilter === 'ALL'
                            ? 'bg-brand text-white border-brand shadow-xs'
                            : 'bg-background hover:bg-muted text-muted-foreground'
                        )}
                      >
                        Tất cả ({allStudents.length})
                      </button>
                      {slots.map((sl) => {
                        const count = allStudents.filter((st) => st.assignedSlotId === sl.slotId).length
                        return (
                          <button
                            key={sl.slotId}
                            type="button"
                            onClick={() => setSelectedSlotFilter(sl.slotId)}
                            className={cn(
                              'h-7 px-2.5 rounded-lg text-xs font-medium transition-colors border',
                              selectedSlotFilter === sl.slotId
                                ? 'bg-brand text-white border-brand shadow-xs'
                                : 'bg-background hover:bg-muted text-muted-foreground'
                            )}
                          >
                            {sl.slotName} ({count})
                          </button>
                        )
                      })}
                    </div>
                  )}

                  {/* Student Roster List */}
                  <ul className="divide-y rounded-2xl border max-h-[380px] overflow-y-auto bg-card">
                    {filteredStudents.map((s) => (
                      <li
                        key={s.sessionEnrollmentId}
                        className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-xs hover:bg-muted/30 transition-colors"
                      >
                        <div className="min-w-0 flex-1">
                          <p className="truncate font-semibold text-foreground text-sm">
                            {s.studentName}{' '}
                            <span className="font-mono font-normal text-muted-foreground text-xs">
                              #{s.studentId}
                            </span>
                          </p>
                          <p className="truncate text-muted-foreground">{s.studentEmail}</p>
                        </div>

                        {/* Assigned Slot */}
                        {slots.length > 0 && (
                          <div className="flex items-center">
                            {notStarted ? (
                              <select
                                aria-label={`Gán kíp cho ${s.studentName}`}
                                value={s.assignedSlotId ?? ''}
                                disabled={reassigningSlotStudentId === s.studentId}
                                onChange={(e) => handleAssignStudentSlot(s.studentId, Number(e.target.value))}
                                className="h-7 rounded-lg border border-input bg-background px-2 text-xs font-medium outline-none focus:border-brand"
                              >
                                <option value="" disabled>
                                  -- Chọn kíp --
                                </option>
                                {slots.map((sl) => (
                                  <option key={sl.slotId} value={sl.slotId}>
                                    {sl.slotName}
                                  </option>
                                ))}
                              </select>
                            ) : (
                              <span className="rounded-md bg-emerald-50 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300 border border-emerald-200 px-2 py-0.5 text-xs font-medium">
                                {s.assignedSlotName ?? 'Chưa gán kíp'}
                              </span>
                            )}
                          </div>
                        )}

                        {/* Assigned Exam */}
                        <div className="flex items-center gap-2">
                          {notStarted && exams.length > 1 ? (
                            <select
                              aria-label={`Gán đề cho ${s.studentName}`}
                              value={s.assignedExamId ?? ''}
                              disabled={reassigningStudentId === s.studentId}
                              onChange={(e) => handleAssignStudentExam(s.studentId, Number(e.target.value))}
                              className="h-7 rounded-lg border border-input bg-background px-2 text-xs font-mono font-medium outline-none focus:border-brand"
                            >
                              <option value="" disabled>
                                -- Chọn đề --
                              </option>
                              {exams.map((ex) => (
                                <option key={ex.examId} value={ex.examId}>
                                  {ex.examCode} ({ex.title})
                                </option>
                              ))}
                            </select>
                          ) : (
                            <span className="rounded-md bg-brand-soft px-2 py-0.5 font-mono text-xs font-semibold text-brand">
                              {s.assignedExamCode ?? session.examCode}
                            </span>
                          )}

                          <span className="text-[11px] text-muted-foreground hidden md:inline">
                            {s.enrolledAt ? formatDateTime(parseApiDate(s.enrolledAt)) : ''}
                          </span>

                          {/* Student remove button */}
                          {notStarted &&
                            (removingId === s.studentId ? (
                              <div className="flex items-center gap-1">
                                <Button
                                  size="sm"
                                  variant="destructive"
                                  className="h-6 text-[11px] px-2"
                                  disabled={busy}
                                  onClick={() => void removeStudent(s.studentId)}
                                >
                                  Xóa
                                </Button>
                                <Button
                                  size="sm"
                                  variant="ghost"
                                  className="h-6 text-[11px] px-2"
                                  disabled={busy}
                                  onClick={() => setRemovingId(null)}
                                >
                                  Hủy
                                </Button>
                              </div>
                            ) : (
                              <Button
                                size="icon-sm"
                                variant="ghost"
                                disabled={busy}
                                aria-label={`Xóa ${s.studentName} khỏi ca thi`}
                                title="Xóa khỏi ca thi"
                                onClick={() => setRemovingId(s.studentId)}
                                className="text-muted-foreground hover:text-destructive size-7"
                              >
                                <UserMinus className="size-3.5" aria-hidden="true" />
                              </Button>
                            ))}
                        </div>
                      </li>
                    ))}
                  </ul>
                </div>
              )
            })()}
          </div>
        )}
      </div>

      {showConfigureSlots && (
        <ConfigureSlotsDialog
          open={showConfigureSlots}
          session={session}
          onClose={() => setShowConfigureSlots(false)}
          onSaved={(updated) => {
            applyUpdate(updated)
            monitor.reload()
          }}
        />
      )}
    </div>
  )
}
