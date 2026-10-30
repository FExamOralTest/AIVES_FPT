import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
  Calendar,
  Clock,
  GraduationCap,
  Info,
  Layers,
  Loader2,
  Plus,
  Users,
  X,
} from 'lucide-react'
import { useEffect, useId, useMemo, useState, type FormEvent } from 'react'
import { FormField } from '@/components/common/FormField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { examsApi, type Exam } from '@/features/exams'
import { toApiError } from '@/lib/http/apiError'
import { useApi } from '@/lib/http/useApi'
import { cn } from '@/lib/utils'
import { periodsApi } from '../api/periodsApi'
import { sessionsApi } from '../api/sessionsApi'
import type { CreateSessionSlotInput, ExamPeriod, ExamSession } from '../types'
import { CreatePeriodDialog } from './CreatePeriodDialog'

interface CreateSessionDialogProps {
  open: boolean
  initialExamId: number | null
  onClose: () => void
  onCreated: (session: ExamSession) => void
}

export function CreateSessionDialog({ open, initialExamId, onClose, onCreated }: CreateSessionDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto p-0">
        {open && <CreateSessionForm initialExamId={initialExamId} onClose={onClose} onCreated={onCreated} />}
      </DialogContent>
    </Dialog>
  )
}

function nextHalfHour() {
  const d = new Date()
  d.setSeconds(0, 0)
  d.setMinutes(d.getMinutes() < 30 ? 30 : 60)
  return d
}

function toLocalInput(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function formatTimeOnly(iso: string) {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })
}

function CreateSessionForm({
  initialExamId = null,
  onClose,
  onCreated,
}: {
  initialExamId: number | null
  onClose: () => void
  onCreated: (session: ExamSession) => void
}) {
  const examSelectId = useId()
  const [activeTab, setActiveTab] = useState<'OVERVIEW' | 'STUDENTS'>('OVERVIEW')

  const { state: exams } = useApi(
    () => examsApi.listMine(),
    'session-exams',
    'Could not load exams.'
  )

  const examList = exams.status === 'ready' ? exams.data : []

  const [selectedExamIds, setSelectedExamIds] = useState<number[]>(() => {
    return initialExamId ? [initialExamId] : []
  })

  // Periods state
  const [periods, setPeriods] = useState<ExamPeriod[]>([])
  const [loadingPeriods, setLoadingPeriods] = useState(false)
  const [selectedPeriodId, setSelectedPeriodId] = useState<string>('')
  const [openCreatePeriod, setOpenCreatePeriod] = useState(false)

  const [sessionName, setSessionName] = useState('')
  const [room, setRoom] = useState('')
  const [maxCapacity, setMaxCapacity] = useState('50')
  const defaultStart = nextHalfHour()
  const [startTime, setStartTime] = useState(toLocalInput(defaultStart))
  const [endTime, setEndTime] = useState(toLocalInput(new Date(defaultStart.getTime() + 60 * 60_000)))

  // Quick Slot Generation state
  const [slotMode, setSlotMode] = useState<'NONE' | 'COUNT' | 'DURATION'>('NONE')
  const [quickSlotCount, setQuickSlotCount] = useState('2')
  const [quickSlotDuration, setQuickSlotDuration] = useState('30')

  // Students state (Tab 2)
  const [importFromCourse, setImportFromCourse] = useState(false)
  const [studentText, setStudentText] = useState('')

  const [pickerValue, setPickerValue] = useState('')
  const [errors, setEditErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const availableExams = examList.filter((e) => !selectedExamIds.includes(e.examId))
  const selectedExamsData = selectedExamIds.map((id) => {
    const found = examList.find((e) => e.examId === id)
    return found ?? ({ examId: id, examCode: `ID #${id}`, title: 'Exam', durationMinutes: 0, maxQuestions: 0 } as Exam)
  })

  const primaryExam = selectedExamsData[0] as Exam | undefined

  // Total session duration (minutes) default to exam duration or 60 mins
  const totalDurationMinutes = primaryExam?.durationMinutes || 60

  // Automatically fetch periods when primary exam's courseId changes
  useEffect(() => {
    let isMounted = true
    const courseId = primaryExam?.courseId
    setLoadingPeriods(true)
    periodsApi.list(courseId)
      .then((res) => {
        if (isMounted) {
          setPeriods(res)
          if (res.length > 0 && !selectedPeriodId) {
            setSelectedPeriodId(String(res[0].periodId))
          }
        }
      })
      .catch(() => {})
      .finally(() => {
        if (isMounted) setLoadingPeriods(false)
      })
    return () => {
      isMounted = false
    }
  }, [primaryExam?.courseId])

  // Automatically update endTime whenever startTime or totalDurationMinutes changes
  useEffect(() => {
    const s = new Date(startTime)
    if (!Number.isNaN(s.getTime())) {
      const e = new Date(s.getTime() + totalDurationMinutes * 60_000)
      setEndTime(toLocalInput(e))
    }
  }, [startTime, totalDurationMinutes])

  function addExam(examIdStr: string) {
    const id = Number(examIdStr)
    if (!id || selectedExamIds.includes(id)) return
    const nextList = [...selectedExamIds, id]
    setSelectedExamIds(nextList)
    setPickerValue('')
    if (errors.examId) setEditErrors((prev) => ({ ...prev, examId: '' }))
  }

  function removeExam(id: number) {
    setSelectedExamIds((prev) => prev.filter((x) => x !== id))
  }

  // Parse entered tokens for students
  const studentTokens = studentText.split(/[\s,;]+/).map((t) => t.trim()).filter(Boolean)

  // Compute preview generated slots if Quick Split is active
  const previewSlots = useMemo(() => {
    if (slotMode === 'NONE') return []
    const s = new Date(startTime)
    if (Number.isNaN(s.getTime())) return []

    const totalMins = totalDurationMinutes
    const count =
      slotMode === 'COUNT'
        ? Math.max(2, Number(quickSlotCount) || 2)
        : Math.max(1, Math.floor(totalMins / (Number(quickSlotDuration) || 30)))
    const durPerSlot =
      slotMode === 'COUNT'
        ? Math.max(5, Math.floor(totalMins / count))
        : Math.max(5, Number(quickSlotDuration) || 30)

    const res: CreateSessionSlotInput[] = []
    let currentStart = new Date(s)

    for (let i = 1; i <= count; i++) {
      const slotEnd = new Date(currentStart.getTime() + durPerSlot * 60_000)
      res.push({
        slotIndex: i,
        slotName: `Slot ${i}`,
        startTime: currentStart.toISOString(),
        endTime: slotEnd.toISOString(),
        maxCapacity: Math.ceil((Number(maxCapacity) || 50) / count),
        allowedExamIds: selectedExamIds.length > 0 ? selectedExamIds : undefined,
      })
      currentStart = slotEnd
    }
    return res
  }, [slotMode, quickSlotCount, quickSlotDuration, startTime, totalDurationMinutes, maxCapacity, selectedExamIds])

  function validate() {
    const next: Record<string, string> = {}
    if (selectedExamIds.length === 0) next.examId = 'Please select at least 1 exam paper for this session.'
    const name = sessionName.trim()
    if (!name) next.sessionName = 'Please enter session name.'
    else if (name.length > 150) next.sessionName = 'Session name cannot exceed 150 characters.'
    const start = new Date(startTime)
    const end = new Date(endTime)
    if (Number.isNaN(start.getTime())) next.start = 'Select start time.'
    if (Number.isNaN(end.getTime())) next.end = 'Invalid end time calculation.'
    else if (!next.start && end <= start) next.end = 'End time must be after start time.'
    const cap = Number(maxCapacity)
    if (!/^\d+$/.test(maxCapacity) || cap < 1 || cap > 500) next.maxCapacity = 'Maximum capacity must be between 1 and 500 students.'
    return next
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const nextErrors = validate()
    setEditErrors(nextErrors)
    setFormError(null)
    if (Object.keys(nextErrors).length > 0) {
      setActiveTab('OVERVIEW')
      return
    }

    setSubmitting(true)
    const startIso = new Date(startTime).toISOString()
    const endIso = new Date(endTime).toISOString()

    try {
      // 1. Create session with slots and period
      const created = await sessionsApi.create({
        examId: selectedExamIds[0],
        examIds: selectedExamIds,
        courseId: primaryExam?.courseId,
        periodId: selectedPeriodId ? Number(selectedPeriodId) : undefined,
        sessionName: sessionName.trim(),
        room: room.trim() || undefined,
        startTime: startIso,
        endTime: endIso,
        maxCapacity: Number(maxCapacity),
        slots: previewSlots.length > 0 ? previewSlots : undefined,
      })

      // 2. Add students if configured
      let studentsAdded = false
      if (importFromCourse) {
        try {
          await sessionsApi.enrollCourseStudents(created.sessionId)
          studentsAdded = true
        } catch {
          // ignore or fallback
        }
      }

      if (studentTokens.length > 0) {
        try {
          await sessionsApi.addStudents(created.sessionId, { identifiers: studentTokens })
          studentsAdded = true
        } catch {
          // ignore or log
        }
      }

      // 3. Auto-distribute slots and exams if configured
      if (studentsAdded) {
        if (previewSlots.length > 0) {
          try {
            await sessionsApi.distributeSlots(created.sessionId, { strategy: 'BALANCED_RANDOM' })
          } catch {
            // non-fatal
          }
        } else if (selectedExamIds.length > 1) {
          try {
            await sessionsApi.distributeExams(created.sessionId, { strategy: 'BALANCED_RANDOM' })
          } catch {
            // non-fatal
          }
        }
      }

      // 4. Load fresh session & pass to callback
      const finalSession = await sessionsApi.get(created.sessionId)
      onCreated(finalSession)
      onClose()
    } catch (err) {
      const apiError = toApiError(err)
      setFormError(apiError.message)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <>
      <form onSubmit={handleSubmit} noValidate className="flex flex-col">
        {/* Header */}
        <div className="border-b px-6 py-4">
          <div className="flex items-center gap-2">
            <span className="rounded-full bg-brand-soft px-2.5 py-0.5 text-xs font-semibold text-brand">
              Create New Session
            </span>
            {selectedExamIds.length > 1 && (
              <span className="rounded-full bg-blue-50 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300 border border-blue-200 px-2.5 py-0.5 text-xs font-medium">
                {selectedExamIds.length} exams
              </span>
            )}
            {previewSlots.length > 0 && (
              <span className="rounded-full bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300 border border-emerald-200 px-2.5 py-0.5 text-xs font-medium">
                {previewSlots.length} time slots
              </span>
            )}
          </div>
          <DialogTitle className="mt-1.5 text-xl font-bold">Configure New Exam Session</DialogTitle>
          <DialogDescription className="text-xs text-muted-foreground mt-0.5">
            Organize session schedule by academic period, room, time slots, and enrolled students
          </DialogDescription>
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
            Overview & Schedule
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
            Enrolled Students
            {(importFromCourse || studentTokens.length > 0) && (
              <span
                className={cn(
                  'rounded-full px-2 py-0.5 text-xs font-semibold',
                  activeTab === 'STUDENTS' ? 'bg-brand text-white' : 'bg-muted text-muted-foreground'
                )}
              >
                {importFromCourse ? 'Course' : studentTokens.length}
              </span>
            )}
          </button>
        </div>

        {formError && (
          <div className="px-6 pt-4">
            <Alert variant="destructive" className="py-2.5">
              <AlertCircle className="size-4" />
              <AlertDescription className="text-xs">{formError}</AlertDescription>
            </Alert>
          </div>
        )}

        {/* Tab Content Area */}
        <div className="p-6 space-y-5">
          {/* ========================================================================= */}
          {/* TAB 1: OVERVIEW & SCHEDULE */}
          {/* ========================================================================= */}
          {activeTab === 'OVERVIEW' && (
            <div className="space-y-4">
              {/* Card 1: Academic Period & General Info */}
              <div className="rounded-2xl border bg-card p-4 space-y-3">
                <h4 className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
                  General Session Information
                </h4>

                {/* Period Selector row */}
                <div className="grid gap-3 sm:grid-cols-[1fr_auto] items-end">
                  <div className="space-y-1">
                    <Label htmlFor="session-period-select" className="text-xs font-semibold text-foreground flex items-center gap-1.5">
                      <Calendar className="size-3.5 text-brand" />
                      Academic Period / Semester
                    </Label>
                    <select
                      id="session-period-select"
                      value={selectedPeriodId}
                      onChange={(e) => setSelectedPeriodId(e.target.value)}
                      disabled={loadingPeriods}
                      className="h-10 w-full rounded-xl border border-input bg-background px-3 text-xs outline-none focus:border-brand"
                    >
                      <option value="">-- No specific period / General --</option>
                      {periods.map((p) => (
                        <option key={p.periodId} value={p.periodId}>
                          {p.periodName} {p.courseName ? `(${p.courseName})` : ''}
                        </option>
                      ))}
                    </select>
                  </div>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="h-10 px-3 text-xs"
                    disabled={!primaryExam?.courseId}
                    onClick={() => setOpenCreatePeriod(true)}
                    title={!primaryExam?.courseId ? 'Select an exam paper first to link a period to its course' : 'Create new period'}
                  >
                    <Plus className="size-3.5 mr-1" />
                    New Period
                  </Button>
                </div>

                <FormField
                  label="Session Name"
                  placeholder="e.g. Morning Session 01 - Class SE1601"
                  value={sessionName}
                  error={errors.sessionName}
                  onChange={(e) => {
                    setSessionName(e.target.value)
                    if (errors.sessionName) setEditErrors((prev) => ({ ...prev, sessionName: '' }))
                  }}
                />

                <div className="grid gap-3 sm:grid-cols-[1fr_9rem]">
                  <div>
                    <FormField
                      label="Room"
                      placeholder="e.g. Room 301, Lab AI, Online Zoom"
                      value={room}
                      list="session-room-suggestions"
                      onChange={(e) => setRoom(e.target.value)}
                    />
                    <datalist id="session-room-suggestions">
                      <option value="Room 101" />
                      <option value="Room 102" />
                      <option value="Room 201" />
                      <option value="Room 301" />
                      <option value="Room 302" />
                      <option value="Lab AI Alpha" />
                      <option value="Lab AI Beta" />
                      <option value="Online - Zoom" />
                      <option value="Online - Google Meet" />
                    </datalist>
                  </div>

                  <FormField
                    label="Max Capacity"
                    inputMode="numeric"
                    value={maxCapacity}
                    error={errors.maxCapacity}
                    hint="Max capacity"
                    onChange={(e) => {
                      setMaxCapacity(e.target.value.replace(/\D/g, ''))
                      if (errors.maxCapacity) setEditErrors((prev) => ({ ...prev, maxCapacity: '' }))
                    }}
                  />
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <FormField
                    label="Start Time (Kickoff)"
                    type="datetime-local"
                    value={startTime}
                    error={errors.start}
                    onChange={(e) => {
                      setStartTime(e.target.value)
                      if (errors.start) setEditErrors((prev) => ({ ...prev, start: '' }))
                    }}
                  />
                  <FormField
                    label="End Time (Auto-calculated)"
                    type="datetime-local"
                    value={endTime}
                    error={errors.end}
                    readOnly
                    className="bg-muted/30 cursor-not-allowed"
                    hint={`Calculated: ${totalDurationMinutes} mins total duration`}
                  />
                </div>
              </div>

              {/* Card 2: Configured Exam Papers */}
              <div className="rounded-2xl border bg-card p-4 space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Layers className="size-4 text-brand" />
                    <Label htmlFor={examSelectId} className="text-sm font-semibold">
                      Exam papers in this session ({selectedExamIds.length})
                    </Label>
                  </div>
                  {selectedExamIds.length > 1 && (
                    <span className="rounded-full bg-brand-soft px-2.5 py-0.5 text-xs font-medium text-brand">
                      Balanced Distribution
                    </span>
                  )}
                </div>

                {/* Chips selected exams */}
                {selectedExamsData.length > 0 ? (
                  <div className="flex flex-wrap gap-2 pt-1">
                    {selectedExamsData.map((e) => (
                      <div
                        key={e.examId}
                        className="flex items-center gap-2 rounded-xl border bg-muted/30 px-3 py-1.5 text-xs shadow-xs"
                      >
                        <span className="font-mono font-bold text-brand">{e.examCode}</span>
                        <span className="max-w-[150px] truncate font-medium text-foreground">{e.title}</span>
                        <span className="text-muted-foreground">({e.durationMinutes}m)</span>
                        {selectedExamIds.length > 1 && (
                          <button
                            type="button"
                            onClick={() => removeExam(e.examId)}
                            className="ml-1 rounded-full p-0.5 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
                            title="Remove exam"
                          >
                            <X className="size-3.5" />
                          </button>
                        )}
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-xs text-muted-foreground italic">No exam papers selected yet.</p>
                )}

                {/* Add exam dropdown */}
                <div className="flex gap-2 pt-1">
                  <select
                    id={examSelectId}
                    value={pickerValue}
                    onChange={(e) => setPickerValue(e.target.value)}
                    disabled={exams.status !== 'ready' || availableExams.length === 0}
                    className={cn(
                      'h-10 w-full rounded-xl border border-input bg-background px-3 text-xs outline-none focus:border-brand',
                      errors.examId && 'border-destructive ring-2 ring-destructive/20'
                    )}
                  >
                    <option value="">
                      {exams.status === 'loading'
                        ? 'Loading exams…'
                        : availableExams.length
                        ? '+ Select exam paper to add to session'
                        : 'All available exams have been added'}
                    </option>
                    {availableExams.map((exam) => (
                      <option key={exam.examId} value={exam.examId}>
                        {exam.examCode} - {exam.title} ({exam.durationMinutes} mins)
                      </option>
                    ))}
                  </select>
                  <Button
                    type="button"
                    variant="secondary"
                    size="sm"
                    className="shrink-0 h-10 px-3 text-xs"
                    disabled={!pickerValue}
                    onClick={() => addExam(pickerValue)}
                  >
                    <Plus className="size-3.5 mr-1" />
                    Add exam
                  </Button>
                </div>

                {errors.examId && <p className="text-xs text-destructive">{errors.examId}</p>}
              </div>

              {/* Card 3: Quick Time Slot Setup */}
              <div className="rounded-2xl border bg-card p-4 space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Clock className="size-4 text-brand" />
                    <h4 className="text-sm font-semibold text-foreground">
                      Time Slots (Optional Quick Setup)
                    </h4>
                  </div>
                  <span className="text-[11px] text-muted-foreground">
                    Full editor available in Session Details
                  </span>
                </div>

                {/* Slicing mode selector */}
                <div className="grid grid-cols-3 gap-2">
                  <button
                    type="button"
                    onClick={() => setSlotMode('NONE')}
                    className={cn(
                      'rounded-xl border px-3 py-2 text-xs font-medium transition-all text-left',
                      slotMode === 'NONE'
                        ? 'border-brand bg-brand-soft/50 text-brand font-semibold ring-1 ring-brand'
                        : 'border-border bg-background hover:bg-muted/30 text-muted-foreground'
                    )}
                  >
                    <div className="font-semibold text-foreground">Single Slot</div>
                    <div className="text-[10px] text-muted-foreground">One slot for whole session</div>
                  </button>

                  <button
                    type="button"
                    onClick={() => setSlotMode('COUNT')}
                    className={cn(
                      'rounded-xl border px-3 py-2 text-xs font-medium transition-all text-left',
                      slotMode === 'COUNT'
                        ? 'border-brand bg-brand-soft/50 text-brand font-semibold ring-1 ring-brand'
                        : 'border-border bg-background hover:bg-muted/30 text-muted-foreground'
                    )}
                  >
                    <div className="font-semibold text-foreground">Split into N Slots</div>
                    <div className="text-[10px] text-muted-foreground">Fixed number of slots</div>
                  </button>

                  <button
                    type="button"
                    onClick={() => setSlotMode('DURATION')}
                    className={cn(
                      'rounded-xl border px-3 py-2 text-xs font-medium transition-all text-left',
                      slotMode === 'DURATION'
                        ? 'border-brand bg-brand-soft/50 text-brand font-semibold ring-1 ring-brand'
                        : 'border-border bg-background hover:bg-muted/30 text-muted-foreground'
                    )}
                  >
                    <div className="font-semibold text-foreground">By Slot Duration</div>
                    <div className="text-[10px] text-muted-foreground">Split by duration in mins</div>
                  </button>
                </div>

                {/* Inputs for slot modes */}
                {slotMode === 'COUNT' && (
                  <div className="flex items-center gap-3 pt-1">
                    <Label className="text-xs font-medium text-foreground whitespace-nowrap">
                      Number of slots:
                    </Label>
                    <input
                      type="number"
                      min={2}
                      max={10}
                      value={quickSlotCount}
                      onChange={(e) => setQuickSlotCount(e.target.value)}
                      className="w-20 rounded-xl border border-input bg-background px-3 py-1.5 text-xs text-center font-bold"
                    />
                    <span className="text-xs text-muted-foreground">
                      → approx {Math.max(5, Math.floor(totalDurationMinutes / (Number(quickSlotCount) || 2)))} mins per slot
                    </span>
                  </div>
                )}

                {slotMode === 'DURATION' && (
                  <div className="flex items-center gap-3 pt-1">
                    <Label className="text-xs font-medium text-foreground whitespace-nowrap">
                      Duration per slot (minutes):
                    </Label>
                    <input
                      type="number"
                      min={5}
                      max={180}
                      step={5}
                      value={quickSlotDuration}
                      onChange={(e) => setQuickSlotDuration(e.target.value)}
                      className="w-20 rounded-xl border border-input bg-background px-3 py-1.5 text-xs text-center font-bold"
                    />
                    <span className="text-xs text-muted-foreground">
                      → creates {Math.max(1, Math.floor(totalDurationMinutes / (Number(quickSlotDuration) || 30)))} consecutive slots
                    </span>
                  </div>
                )}

                {/* Live timeline preview of slots */}
                {previewSlots.length > 0 && (
                  <div className="space-y-1.5 rounded-xl bg-muted/30 p-2.5 border">
                    <div className="flex items-center justify-between text-[11px] font-semibold text-muted-foreground">
                      <span>Generated Time Slots Preview ({previewSlots.length})</span>
                      <span>Balanced capacity ~{previewSlots[0].maxCapacity} / slot</span>
                    </div>
                    <div className="grid gap-1.5 sm:grid-cols-2">
                      {previewSlots.map((s) => (
                        <div
                          key={s.slotIndex}
                          className="flex items-center justify-between rounded-lg border bg-background px-2.5 py-1 text-xs"
                        >
                          <div className="flex items-center gap-1.5">
                            <span className="font-semibold text-brand">{s.slotName}</span>
                            <span className="text-muted-foreground">
                              {formatTimeOnly(s.startTime)} – {formatTimeOnly(s.endTime)}
                            </span>
                          </div>
                          <span className="text-[11px] text-muted-foreground">
                            cap: {s.maxCapacity}
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* ========================================================================= */}
          {/* TAB 2: ENROLLED STUDENTS */}
          {/* ========================================================================= */}
          {activeTab === 'STUDENTS' && (
            <div className="space-y-4">
              {/* Option A: Import from course */}
              <div className="rounded-2xl border bg-card p-4 space-y-2">
                <label className="flex items-start gap-3 cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={importFromCourse}
                    onChange={(e) => setImportFromCourse(e.target.checked)}
                    className="mt-0.5 size-4 rounded border-gray-300 text-brand focus:ring-brand accent-brand"
                  />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <GraduationCap className="size-4 text-brand" />
                      <span className="text-sm font-semibold text-foreground">
                        Automatically enroll all course students into this session
                      </span>
                    </div>
                    <p className="text-xs text-muted-foreground mt-0.5">
                      The system will automatically scan students registered in the corresponding course and enroll them into the session right upon creation.
                    </p>
                  </div>
                </label>
              </div>

              {/* Option B: Paste Student Codes / Emails / IDs */}
              <div className="rounded-2xl border bg-card p-4 space-y-3">
                <div className="flex items-center justify-between">
                  <Label htmlFor="create-students-input" className="text-sm font-semibold text-foreground flex items-center gap-1.5">
                    <Users className="size-4 text-brand" />
                    Paste additional student list (Student Code, Email, ID)
                  </Label>
                  {studentTokens.length > 0 && (
                    <span className="rounded-full bg-brand-soft px-2.5 py-0.5 text-xs font-semibold text-brand">
                      {studentTokens.length} students
                    </span>
                  )}
                </div>

                <textarea
                  id="create-students-input"
                  rows={3}
                  value={studentText}
                  onChange={(e) => setStudentText(e.target.value)}
                  placeholder="e.g. SE160001, SE160002, he170003@fpt.edu.vn, 12... (Separated by commas, spaces, or line breaks)"
                  className="w-full rounded-xl border border-input bg-background p-2.5 text-xs outline-none resize-y min-h-[64px] focus:border-brand focus:ring-3 focus:ring-brand/15"
                />

                <p className="text-[11px] text-muted-foreground">
                  Enrolled students will be automatically balanced across the configured time slots and exam papers.
                </p>

                {/* Tokens chips preview */}
                {studentTokens.length > 0 && (
                  <div className="space-y-1.5 pt-1 border-t">
                    <span className="text-xs font-medium text-muted-foreground">
                      Recognized list ({studentTokens.length}):
                    </span>
                    <div className="flex flex-wrap gap-1.5 max-h-[120px] overflow-y-auto p-1">
                      {studentTokens.slice(0, 30).map((tok, idx) => (
                        <span
                          key={idx}
                          className="rounded-lg border bg-muted/40 px-2 py-0.5 font-mono text-[11px] text-foreground"
                        >
                          {tok}
                        </span>
                      ))}
                      {studentTokens.length > 30 && (
                        <span className="text-[11px] text-muted-foreground italic px-1 self-center">
                          … and {studentTokens.length - 30} other students
                        </span>
                      )}
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <DialogFooter className="border-t px-6 py-3.5 gap-2 sm:gap-2">
          <Button type="button" variant="ghost" onClick={onClose} disabled={submitting}>
            Cancel
          </Button>

          {activeTab === 'OVERVIEW' ? (
            <>
              <Button
                type="button"
                variant="outline"
                onClick={() => setActiveTab('STUDENTS')}
                className="gap-1.5 text-xs"
              >
                Continue: Enrolled Students
                <ArrowRight className="size-3.5" />
              </Button>
              <Button type="submit" disabled={submitting} className="gap-1.5">
                {submitting ? <Loader2 className="size-4 animate-spin" /> : null}
                {submitting ? 'Creating…' : 'Create Session'}
              </Button>
            </>
          ) : (
            <>
              <Button
                type="button"
                variant="outline"
                onClick={() => setActiveTab('OVERVIEW')}
                className="gap-1.5 text-xs"
              >
                <ArrowLeft className="size-3.5" />
                Back to Overview
              </Button>
              <Button type="submit" disabled={submitting} className="gap-1.5">
                {submitting ? <Loader2 className="size-4 animate-spin" /> : null}
                {submitting
                  ? 'Creating & enrolling students…'
                  : `Create Session ${studentTokens.length > 0 ? `(${studentTokens.length} students)` : ''}`}
              </Button>
            </>
          )}
        </DialogFooter>
      </form>

      {/* Mini dialog for creating new exam period */}
      {openCreatePeriod && primaryExam?.courseId && (
        <CreatePeriodDialog
          open={openCreatePeriod}
          courseId={primaryExam.courseId}
          courseName={primaryExam.courseName}
          onClose={() => setOpenCreatePeriod(false)}
          onCreated={(created) => {
            setPeriods((prev) => [created, ...prev])
            setSelectedPeriodId(String(created.periodId))
          }}
        />
      )}
    </>
  )
}
