import {
  Check,
  CircleAlert,
  Clock,
  Info,
  Layers,
  Loader2,
  Pencil,
  Play,
  RotateCw,
  Save,
  Shuffle,
  Sparkles,
  Square,
  Trash2,
  UserMinus,
  Users,
  X,
  XCircle,
} from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { CopyButton } from '@/components/common/CopyButton'
import { FormField } from '@/components/common/FormField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import {
  SESSION_STATUS_LABEL,
  SESSION_STATUS_TONE,
  SESSION_TRANSITIONS,
  type SessionStatus,
} from '@/lib/domain/sessionStatus'
import { examsApi } from '@/features/exams'
import { formatDateTime, formatTimeRange, parseApiDate } from '@/lib/format/date'
import { toApiError } from '@/lib/http/apiError'
import { useApi } from '@/lib/http/useApi'
import { cn } from '@/lib/utils'
import { sessionsApi } from '../api/sessionsApi'
import type { DistributeExamsResponse, DistributeSlotsResponse, ExamSession } from '../types'
import { AddStudentsForm } from './AddStudentsForm'
import { ConfigureSlotsDialog } from './ConfigureSlotsDialog'

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
  IN_PROGRESS: { label: 'Start session', icon: <Play className="size-4" aria-hidden="true" />, variant: 'default' },
  COMPLETED: {
    label: 'Complete session',
    confirm: 'Complete this session? Students will no longer be able to submit. This action cannot be undone.',
    icon: <Square className="size-4" aria-hidden="true" />,
    variant: 'default',
  },
  CANCELLED: {
    label: 'Cancel session',
    confirm: 'Cancel this session? Students will not be able to enter the exam room. This action cannot be undone.',
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

  // Inline edit state
  const [editSessionName, setEditSessionName] = useState('')
  const [editRoom, setEditRoom] = useState('')
  const [editMaxCapacity, setEditMaxCapacity] = useState('50')
  const [editStartTime, setEditStartTime] = useState('')
  const [editEndTime, setEditEndTime] = useState('')
  const [editExamIds, setEditExamIds] = useState<number[]>([])
  const [savingEdit, setSavingEdit] = useState(false)
  const [editErrors, setEditErrors] = useState<{
    form?: string
    name?: string
    time?: string
    capacity?: string
    exams?: string
  }>({})

  const { state: teacherExamsState } = useApi(
    () => examsApi.listMine(),
    'session-detail-teacher-exams',
    'Could not load exams.'
  )

  if (detail.state.status === 'loading') {
    return (
      <div className="p-6 space-y-4">
        <DialogHeader>
          <DialogTitle>Exam Session</DialogTitle>
          <DialogDescription>Loading session details…</DialogDescription>
        </DialogHeader>
        <div className="h-44 animate-pulse rounded-2xl bg-muted motion-reduce:animate-none" />
      </div>
    )
  }
  if (detail.state.status === 'error') {
    return (
      <div className="p-6 space-y-4">
        <DialogHeader>
          <DialogTitle>Exam Session</DialogTitle>
          <DialogDescription className="text-destructive">{detail.state.message}</DialogDescription>
        </DialogHeader>
        <Button variant="secondary" className="w-fit" onClick={detail.reload}>
          <RotateCw className="size-4 mr-2" aria-hidden="true" />
          Try again
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
  const teacherExams = teacherExamsState.status === 'ready' ? teacherExamsState.data : []

  function applyUpdate(updated: ExamSession) {
    detail.update(() => updated)
    onChanged(updated)
  }

  function toLocalInput(d: Date) {
    const pad = (n: number) => String(n).padStart(2, '0')
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
  }

  function startEditing() {
    setEditSessionName(session.sessionName)
    setEditRoom(session.room || '')
    setEditMaxCapacity(String(session.maxCapacity))
    setEditStartTime(toLocalInput(start))
    setEditEndTime(toLocalInput(end))
    const currentIds =
      exams.length > 0
        ? exams.map((e) => e.examId)
        : session.examId
        ? [session.examId]
        : []
    setEditExamIds(currentIds)
    setEditErrors({})
    setEditing(true)
  }

  function cancelEditing() {
    setEditing(false)
    setEditErrors({})
  }

  function handleAddExamToEdit(examIdStr: string) {
    const id = Number(examIdStr)
    if (!id || editExamIds.includes(id)) return
    setEditExamIds((prev) => [...prev, id])
    setEditErrors((prev) => ({ ...prev, exams: undefined }))
  }

  function handleRemoveExamFromEdit(examId: number) {
    if (editExamIds.length <= 1) {
      setEditErrors((prev) => ({ ...prev, exams: 'The session must have at least 1 exam paper.' }))
      return
    }
    setEditExamIds((prev) => prev.filter((id) => id !== examId))
    setEditErrors((prev) => ({ ...prev, exams: undefined }))
  }

  async function handleSaveInlineEdit() {
    const errs: typeof editErrors = {}
    if (!editSessionName.trim()) {
      errs.name = 'Please enter session name.'
    }
    const cap = Number(editMaxCapacity)
    if (!cap || cap < 1) {
      errs.capacity = 'Maximum capacity must be greater than 0.'
    } else if (cap < session.enrolledStudents) {
      errs.capacity = `Capacity cannot be lower than current enrolled students (${session.enrolledStudents}).`
    }

    const sDate = new Date(editStartTime)
    const eDate = new Date(editEndTime)
    if (isNaN(sDate.getTime()) || isNaN(eDate.getTime()) || sDate >= eDate) {
      errs.time = 'Start time must be before end time.'
    }

    if (editExamIds.length === 0) {
      errs.exams = 'Please select at least 1 exam paper.'
    }

    if (Object.keys(errs).length > 0) {
      setEditErrors(errs)
      return
    }

    setSavingEdit(true)
    setEditErrors({})
    try {
      const updated = await sessionsApi.update(sessionId, {
        sessionName: editSessionName.trim(),
        room: editRoom.trim() || undefined,
        maxCapacity: cap,
        startTime: sDate.toISOString(),
        endTime: eDate.toISOString(),
        examIds: editExamIds,
      })
      applyUpdate(updated)
      setEditing(false)
      monitor.reload()
    } catch (err) {
      setEditErrors({ form: toApiError(err, 'Could not save session changes.').message })
    } finally {
      setSavingEdit(false)
    }
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
      setActionError(toApiError(err, 'Could not delete session.').message)
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
      setStudentError(toApiError(err, 'Could not remove student from session.').message)
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
      setActionError(toApiError(err, 'Could not distribute exams.').message)
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
      setStudentError(toApiError(err, 'Could not assign exam to student.').message)
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
      setActionError(toApiError(err, 'Could not distribute time slots.').message)
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
      setStudentError(toApiError(err, 'Could not assign time slot to student.').message)
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
      setActionError(toApiError(err, 'Could not change session status.').message)
    } finally {
      setBusy(false)
    }
  }

  const transitions = SESSION_TRANSITIONS[session.status]
  const facts = [
    { label: 'Exam Time', value: formatTimeRange(start, end) },
    { label: 'Room', value: session.room || 'Not set' },
    {
      label: 'Enrollment',
      value: `${session.enrolledStudents} / ${session.maxCapacity} students`,
    },
    { label: 'Course', value: session.courseName || 'Unassigned' },
    ...(session.periodName ? [{ label: 'Exam Period', value: session.periodName }] : []),
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
              {exams.length} exam papers
            </span>
          )}
          {slots.length > 0 && (
            <span className="rounded-full bg-purple-50 text-purple-700 dark:bg-purple-950/40 dark:text-purple-300 border border-purple-200 px-2.5 py-0.5 text-xs font-medium">
              {slots.length} time slots
            </span>
          )}
        </div>
        <DialogTitle className="mt-1.5 text-xl font-bold flex items-center gap-2">
          {session.sessionName}
          {editing && (
            <span className="text-xs font-normal text-amber-700 bg-amber-50 dark:bg-amber-950/40 border border-amber-200 rounded-full px-2 py-0.5">
              Editing Mode
            </span>
          )}
        </DialogTitle>
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
          Overview & Session
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
          Students & Allocation
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
        {/* TAB 1: OVERVIEW & SESSION */}
        {/* ========================================================================= */}
        {activeTab === 'OVERVIEW' && (
          <div className="space-y-5">
            {/* Join code banner */}
            <div className="flex flex-col gap-3 rounded-2xl bg-brand-soft/80 border border-brand/20 p-5">
              <div className="flex flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <p className="text-xs font-medium text-brand/80">
                    {slots.length > 0 ? 'Main Session Join Code (Mã phòng thi chung)' : 'Session Join Code (for students)'}
                  </p>
                  <p className="font-mono text-2xl font-bold tracking-[0.15em] sm:text-3xl text-brand mt-0.5">
                    {session.sessionCode}
                  </p>
                </div>
                <CopyButton value={session.sessionCode} label="Copy session code" />
              </div>

              {slots.length > 0 && (
                <div className="flex items-start gap-2.5 rounded-xl bg-background/80 border border-brand/25 px-3.5 py-2.5 text-xs text-foreground mt-1 shadow-xs">
                  <span className="flex size-5 items-center justify-center rounded-full bg-brand/10 text-brand font-bold shrink-0 mt-0.5">
                    ⚡
                  </span>
                  <div className="space-y-0.5">
                    <p className="font-semibold text-brand">
                      Ca thi đã được chia thành {slots.length} kíp thi (time slots).
                    </p>
                    <p className="text-muted-foreground text-[11px] leading-relaxed">
                      Để phân bổ sinh viên vào đúng ca thi và khung giờ, hãy gửi <strong>Mã Kíp thi riêng (Slot Join Code)</strong> ở danh sách kíp thi bên dưới cho sinh viên từng ca.
                    </p>
                  </div>
                </div>
              )}
            </div>

            {/* Basic Facts Card / In-place Edit Form */}
            {editing ? (
              <div className="rounded-2xl border bg-card p-5 space-y-4 shadow-xs">
                <div className="flex items-center justify-between border-b pb-3">
                  <h4 className="text-xs font-bold uppercase tracking-wider text-brand flex items-center gap-1.5">
                    <Pencil className="size-3.5" /> Edit Session Information
                  </h4>
                  <span className="text-xs text-muted-foreground">Modify directly in this session</span>
                </div>

                <FormField
                  label="Session Name"
                  required
                  error={editErrors.name}
                  value={editSessionName}
                  onChange={(e) => {
                    setEditSessionName(e.target.value)
                    if (editErrors.name) setEditErrors((prev) => ({ ...prev, name: undefined }))
                  }}
                  placeholder="e.g. Morning Session 01 - Class PRJ301"
                />

                <div className="grid gap-4 sm:grid-cols-2">
                  <FormField
                    label="Room"
                    value={editRoom}
                    onChange={(e) => setEditRoom(e.target.value)}
                    placeholder="e.g. Room 301, Alpha Building (optional)"
                  />
                  <FormField
                    label="Max Capacity (students)"
                    type="number"
                    required
                    min={session.enrolledStudents || 1}
                    max={500}
                    value={editMaxCapacity}
                    error={editErrors.capacity}
                    onChange={(e) => {
                      setEditMaxCapacity(e.target.value)
                      if (editErrors.capacity) setEditErrors((prev) => ({ ...prev, capacity: undefined }))
                    }}
                  />
                </div>

                <div className="grid gap-4 sm:grid-cols-2">
                  <FormField
                    label="Start Time"
                    type="datetime-local"
                    required
                    error={editErrors.time}
                    value={editStartTime}
                    onChange={(e) => {
                      setEditStartTime(e.target.value)
                      if (editErrors.time) setEditErrors((prev) => ({ ...prev, time: undefined }))
                    }}
                  />
                  <FormField
                    label="End Time"
                    type="datetime-local"
                    required
                    value={editEndTime}
                    onChange={(e) => setEditEndTime(e.target.value)}
                  />
                </div>
              </div>
            ) : (
              <div className="rounded-2xl border bg-card p-4">
                <h4 className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-3">
                  General Information
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
            )}

            {/* Multi-exam list section */}
            {editing ? (
              <section className="rounded-2xl border p-4 bg-muted/10 space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Layers className="size-4 text-brand" />
                    <h4 className="font-semibold text-sm">
                      Configured Exam Papers ({editExamIds.length})
                    </h4>
                  </div>
                </div>

                {editErrors.exams && (
                  <p className="text-xs text-destructive">{editErrors.exams}</p>
                )}

                <div className="grid gap-2.5 sm:grid-cols-2">
                  {editExamIds.map((id) => {
                    const ex = teacherExams.find((e) => e.examId === id) || exams.find((e) => e.examId === id)
                    return (
                      <div key={id} className="flex items-center justify-between rounded-xl border bg-background p-3 text-xs shadow-xs">
                        <div className="min-w-0 pr-2">
                          <div className="flex items-center gap-1.5">
                            <span className="font-mono font-bold text-brand">{ex?.examCode ?? `Exam #${id}`}</span>
                            <span className="truncate font-medium text-foreground">{ex?.title ?? ''}</span>
                          </div>
                          {ex && (
                            <p className="text-muted-foreground mt-0.5">
                              {'totalQuestions' in ex ? ex.totalQuestions : (ex.maxQuestions ?? ex.questions?.length ?? 0)} questions · {ex.durationMinutes} mins
                            </p>
                          )}
                        </div>
                        <Button
                          type="button"
                          size="icon-sm"
                          variant="ghost"
                          onClick={() => handleRemoveExamFromEdit(id)}
                          disabled={editExamIds.length <= 1}
                          title={editExamIds.length <= 1 ? 'Session must have at least 1 exam' : 'Remove exam'}
                          className="text-muted-foreground hover:text-destructive size-7 shrink-0"
                        >
                          <X className="size-3.5" />
                        </Button>
                      </div>
                    )
                  })}
                </div>

                {/* Dropdown to add more exams */}
                <div className="flex items-center gap-2 pt-1">
                  <select
                    value=""
                    onChange={(e) => handleAddExamToEdit(e.target.value)}
                    className="h-9 flex-1 rounded-lg border border-input bg-background px-3 text-xs focus:border-brand outline-none"
                  >
                    <option value="" disabled>
                      + Add more exam papers to session...
                    </option>
                    {teacherExams
                      .filter((e) => !editExamIds.includes(e.examId))
                      .filter((e) => !session.courseId || e.courseId === session.courseId)
                      .map((e) => (
                        <option key={e.examId} value={e.examId}>
                          {e.examCode} — {e.title} ({e.maxQuestions ?? e.questions?.length ?? 0} questions, {e.durationMinutes} mins)
                        </option>
                      ))}
                  </select>
                </div>
              </section>
            ) : (
              <section className="rounded-2xl border p-4 bg-muted/10 space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Layers className="size-4 text-brand" />
                    <h4 className="font-semibold text-sm">
                      Configured Exam Papers ({exams.length > 0 ? exams.length : 1})
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
                            {ex.totalQuestions} questions · {ex.durationMinutes} mins
                          </p>
                        </div>
                        <div className="shrink-0 text-right">
                          <span className="rounded-full bg-brand-soft px-2.5 py-0.5 font-semibold text-brand text-[11px]">
                            {ex.assignedStudentsCount} students
                          </span>
                        </div>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-xs text-muted-foreground">
                    Exam: <strong>{session.examTitle}</strong> ({session.examCode})
                  </p>
                )}
              </section>
            )}

            {/* Time Slots (Kíp thi) Section */}
            {slots.length > 0 ? (
              <section aria-labelledby="session-slots" className="flex flex-col gap-3 rounded-2xl border p-4 bg-muted/10">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <Clock className="size-4 text-brand" aria-hidden="true" />
                    <h4 id="session-slots" className="font-semibold text-sm">
                      Time Slots Configuration ({slots.length} slots)
                    </h4>
                    <span className="rounded-full bg-brand-soft px-2.5 py-0.5 text-xs font-semibold text-brand">
                      {session.durationMinutes} mins/slot
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
                      Reconfigure slots
                    </Button>
                  )}
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  {slots.map((sl) => {
                    const now = Date.now()
                    const slotStart = parseApiDate(sl.startTime).getTime()
                    const slotEnd = parseApiDate(sl.endTime).getTime()
                    const isHappening = now >= slotStart && now <= slotEnd
                    const isPassed = now > slotEnd
                    const slotCode = sl.slotCode || `${session.sessionCode}-S${sl.slotIndex}`
                    const slotDuration = Math.max(1, Math.round((slotEnd - slotStart) / 60000))

                    return (
                      <div
                        key={sl.slotId}
                        className={cn(
                          'flex flex-col justify-between rounded-xl border p-3.5 text-xs shadow-xs transition-all gap-3',
                          isHappening
                            ? 'border-emerald-300 bg-emerald-50/70 dark:bg-emerald-950/30 ring-2 ring-emerald-500/20'
                            : isPassed
                            ? 'border-border/60 bg-muted/40 opacity-75'
                            : 'border-input bg-card'
                        )}
                      >
                        <div className="flex items-start justify-between gap-2">
                          <div className="space-y-1">
                            <div className="flex items-center gap-1.5 flex-wrap">
                              <span className="font-bold text-foreground text-sm">{sl.slotName}</span>
                              {isHappening && (
                                <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 dark:bg-emerald-900/60 text-emerald-800 dark:text-emerald-200 px-2 py-0.5 text-[10px] font-semibold animate-pulse">
                                  <span className="size-1.5 rounded-full bg-emerald-600" />
                                  In progress
                                </span>
                              )}
                              {isPassed && (
                                <span className="rounded-full bg-muted text-muted-foreground px-2 py-0.5 text-[10px] font-medium">
                                  Ended
                                </span>
                              )}
                              {!isHappening && !isPassed && (
                                <span className="rounded-full bg-sky-100 text-sky-800 dark:bg-sky-950/50 dark:text-sky-300 px-2 py-0.5 text-[10px] font-medium">
                                  Upcoming
                                </span>
                              )}
                            </div>
                            <p className="text-brand font-medium flex items-center gap-1 font-mono">
                              <Clock className="size-3.5 shrink-0" />
                              <span>{formatTimeRange(parseApiDate(sl.startTime), parseApiDate(sl.endTime))}</span>
                              <span className="text-muted-foreground font-sans font-normal">({slotDuration} mins)</span>
                            </p>
                          </div>

                          <span className="rounded-full bg-brand-soft px-2.5 py-0.5 font-semibold text-brand text-[11px] shrink-0">
                            {sl.assignedStudentsCount ?? 0} students
                          </span>
                        </div>

                        {/* Slot Code with Copy Button */}
                        <div className="rounded-lg border bg-muted/30 p-2.5 flex items-center justify-between gap-2">
                          <div className="min-w-0">
                            <span className="text-[10px] uppercase font-semibold tracking-wider text-muted-foreground block">
                              Mã kíp thi (Slot Join Code)
                            </span>
                            <span className="font-mono text-xs font-bold tracking-wider text-brand truncate block">
                              {slotCode}
                            </span>
                          </div>
                          <CopyButton value={slotCode} label="Copy code" />
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
                    <h4 className="text-sm font-semibold text-foreground">Session has no Time Slots yet</h4>
                    <p className="text-xs text-muted-foreground mt-0.5">
                      Split session into smaller time windows to stagger student entry, manage room capacity, and prevent exam leaking.
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
                    Configure Time Slots
                  </Button>
                )}
              </div>
            )}

            {/* Action buttons control bar */}
            {editing ? (
              <div className="flex flex-col gap-3 pt-3 border-t">
                {editErrors.form && (
                  <div className="flex items-center gap-2 rounded-xl bg-destructive/10 px-4 py-3 text-xs text-destructive">
                    <CircleAlert className="size-4 shrink-0" aria-hidden="true" />
                    {editErrors.form}
                  </div>
                )}
                <div className="flex items-center justify-end gap-2.5">
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    disabled={savingEdit}
                    onClick={cancelEditing}
                  >
                    Cancel
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    disabled={savingEdit}
                    onClick={() => void handleSaveInlineEdit()}
                    className="gap-1.5"
                  >
                    {savingEdit ? (
                      <>
                        <Loader2 className="size-3.5 animate-spin" />
                        Saving...
                      </>
                    ) : (
                      <>
                        <Save className="size-3.5" />
                        Save Changes
                      </>
                    )}
                  </Button>
                </div>
              </div>
            ) : (
              (transitions.length > 0 || notStarted || deletableStatus) && (
                <div className="flex flex-col gap-3 pt-2 border-t">
                  {confirming && (
                    <p role="alert" className="rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 px-4 py-3 text-xs text-amber-900 dark:text-amber-200">
                      {confirming === 'DELETE'
                        ? 'Permanently delete this exam session? The join code will be invalidated. This action cannot be undone.'
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
                      <Button variant="outline" size="sm" disabled={busy} onClick={startEditing}>
                        <Pencil className="size-3.5 mr-1.5" aria-hidden="true" />
                        Edit Session
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
                            {isConfirming ? `Confirm: ${action.label.toLowerCase()}` : action.label}
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
                        {confirming === 'DELETE' ? 'Confirm Delete Session' : 'Delete Session'}
                      </Button>
                    )}
                    {confirming && (
                      <Button size="sm" variant="ghost" disabled={busy} onClick={() => setConfirming(null)}>
                        Cancel Action
                      </Button>
                    )}
                  </div>
                  {deletableStatus && session.enrolledStudents > 0 && !confirming && (
                    <p className="text-[12px] text-muted-foreground">
                      Sessions with enrolled students cannot be deleted. Please remove all students first or cancel the session.
                    </p>
                  )}
                </div>
              )
            )}
          </div>
        )}

        {/* ========================================================================= */}
        {/* TAB 2: STUDENTS & ALLOCATION */}
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
                  <span className="text-xs font-semibold">Distribution & Shuffle Tools:</span>
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
                    {slots.length > 0 ? 'Reconfigure Time Slots' : '⚡ Auto-slice Time Slots'}
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
                      {distributing ? 'Distributing exams…' : 'Shuffle & Distribute Exams'}
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
                          aria-label="Slot exam assignment mode"
                        >
                          <option value="EXAM_PER_SLOT">One fixed exam per slot</option>
                          <option value="SHUFFLE_IN_SLOT">Shuffle exams within slot</option>
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
                        {distributingSlots ? 'Distributing slots…' : 'Distribute Students to Slots'}
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
                  Successfully distributed <strong>{distributeResult.totalStudents}</strong> students across <strong>{distributeResult.totalExams}</strong> exams with balanced randomness:
                  <span className="mt-1 flex flex-wrap gap-1.5 font-mono">
                    {distributeResult.allocations.map((a) => (
                      <span key={a.examId} className="bg-emerald-100/80 px-2 py-0.5 rounded text-[11px]">
                        {a.examCode}: {a.studentCount} students
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
                  Successfully distributed <strong>{distributeSlotsResult.totalStudents}</strong> students evenly across <strong>{distributeSlotsResult.totalSlots}</strong> time slots:
                  <span className="mt-1 flex flex-wrap gap-1.5 font-mono">
                    {distributeSlotsResult.allocations.map((a) => (
                      <span key={a.slotId} className="bg-emerald-100/80 px-2 py-0.5 rounded text-[11px]">
                        {a.slotName}: {a.studentCount} students
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
              <p className="text-xs text-muted-foreground">Loading students…</p>
            )}
            {monitor.state.status === 'error' && (
              <p className="text-xs text-destructive">{monitor.state.message}</p>
            )}

            {monitor.state.status === 'ready' && (() => {
              const allStudents = monitor.state.data.students
              if (allStudents.length === 0) {
                return (
                  <p className="rounded-2xl border border-dashed p-6 text-center text-xs text-muted-foreground">
                    No students in this session yet. Enter Student Codes/Emails above or students can join using the Join Code.
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
                        All ({allStudents.length})
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
                                aria-label={`Assign slot to ${s.studentName}`}
                                value={s.assignedSlotId ?? ''}
                                disabled={reassigningSlotStudentId === s.studentId}
                                onChange={(e) => handleAssignStudentSlot(s.studentId, Number(e.target.value))}
                                className="h-7 rounded-lg border border-input bg-background px-2 text-xs font-medium outline-none focus:border-brand"
                              >
                                <option value="" disabled>
                                  -- Select slot --
                                </option>
                                {slots.map((sl) => (
                                  <option key={sl.slotId} value={sl.slotId}>
                                    {sl.slotName}
                                  </option>
                                ))}
                              </select>
                            ) : (
                              <span className="rounded-md bg-emerald-50 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300 border border-emerald-200 px-2 py-0.5 text-xs font-medium">
                                {s.assignedSlotName ?? 'Unassigned slot'}
                              </span>
                            )}
                          </div>
                        )}

                        {/* Assigned Exam */}
                        <div className="flex items-center gap-2">
                          {notStarted && exams.length > 1 ? (
                            <select
                              aria-label={`Assign exam to ${s.studentName}`}
                              value={s.assignedExamId ?? ''}
                              disabled={reassigningStudentId === s.studentId}
                              onChange={(e) => handleAssignStudentExam(s.studentId, Number(e.target.value))}
                              className="h-7 rounded-lg border border-input bg-background px-2 text-xs font-mono font-medium outline-none focus:border-brand"
                            >
                              <option value="" disabled>
                                -- Select exam --
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
                                  Remove
                                </Button>
                                <Button
                                  size="sm"
                                  variant="ghost"
                                  className="h-6 text-[11px] px-2"
                                  disabled={busy}
                                  onClick={() => setRemovingId(null)}
                                >
                                  Cancel
                                </Button>
                              </div>
                            ) : (
                              <Button
                                size="icon-sm"
                                variant="ghost"
                                disabled={busy}
                                aria-label={`Remove ${s.studentName} from session`}
                                title="Remove from session"
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
