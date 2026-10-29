import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
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
import { sessionsApi } from '../api/sessionsApi'
import type { ExamSession } from '../types'

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

  const [sessionName, setSessionName] = useState('')
  const [room, setRoom] = useState('')
  const [maxCapacity, setMaxCapacity] = useState('50')
  const defaultStart = nextHalfHour()
  const [startTime, setStartTime] = useState(toLocalInput(defaultStart))
  const [endTime, setEndTime] = useState(toLocalInput(new Date(defaultStart.getTime() + 60 * 60_000)))

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

  // Total session duration calculated from slot templates or exam duration
  const totalDurationMinutes = useMemo(() => {
    if (primaryExam?.slotTemplates && primaryExam.slotTemplates.length > 0) {
      return primaryExam.slotTemplates.reduce((acc, t) => acc + (t.durationMinutes || 0), 0)
    }
    return primaryExam?.durationMinutes || 60
  }, [primaryExam])

  // Sequentially calculated slots based on primary exam's slot templates
  const inheritedSlots = useMemo(() => {
    if (!primaryExam?.slotTemplates || primaryExam.slotTemplates.length === 0) return []
    const start = new Date(startTime)
    if (Number.isNaN(start.getTime())) return []

    let current = new Date(start)
    return [...primaryExam.slotTemplates]
      .sort((a, b) => a.slotIndex - b.slotIndex)
      .map((t, idx) => {
        const slotStart = new Date(current)
        const slotEnd = new Date(slotStart.getTime() + (t.durationMinutes || 15) * 60_000)
        current = slotEnd
        return {
          slotIndex: t.slotIndex ?? (idx + 1),
          slotName: t.slotName || `Slot ${idx + 1}`,
          durationMinutes: t.durationMinutes || 15,
          maxCapacity: t.maxCapacity || 10,
          startTime: slotStart.toISOString(),
          endTime: slotEnd.toISOString(),
          startDisplay: slotStart.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          endDisplay: slotEnd.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          examPapers: t.examPapers ?? [],
        }
      })
  }, [primaryExam, startTime])

  // Automatically update endTime whenever startTime or totalDurationMinutes changes
  useEffect(() => {
    const s = new Date(startTime)
    if (!Number.isNaN(s.getTime())) {
      const e = new Date(s.getTime() + totalDurationMinutes * 60_000)
      setEndTime(toLocalInput(e))
    }
  }, [startTime, totalDurationMinutes])

  // Automatically update suggested capacity from slot templates if configured
  useEffect(() => {
    if (primaryExam?.slotTemplates && primaryExam.slotTemplates.length > 0) {
      const sumCap = primaryExam.slotTemplates.reduce((acc, t) => acc + (t.maxCapacity || 0), 0)
      if (sumCap > 0) {
        setMaxCapacity(String(sumCap))
      }
    }
  }, [primaryExam])

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
      // 1. Create session (slots are automatically cloned from Exam's SlotTemplates by the backend)
      const created = await sessionsApi.create({
        examId: selectedExamIds[0],
        examIds: selectedExamIds,
        sessionName: sessionName.trim(),
        room: room.trim() || undefined,
        startTime: startIso,
        endTime: endIso,
        maxCapacity: Number(maxCapacity),
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

      // 3. Auto-distribute slots and exams if students enrolled
      if (studentsAdded) {
        if (inheritedSlots.length > 0) {
          try {
            await sessionsApi.distributeSlots(created.sessionId, {
              strategy: 'SLOT_BALANCED',
            })
          } catch {
            // non-fatal
          }
        }
        if (selectedExamIds.length > 1) {
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
        </div>
        <DialogTitle className="mt-1.5 text-xl font-bold">Configure New Exam Session</DialogTitle>
        <DialogDescription className="text-xs text-muted-foreground mt-0.5">
          Configure room info, exam paper, start time, and enroll participating students
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
            {/* Card 1: Basic Information */}
            <div className="rounded-2xl border bg-card p-4 space-y-3">
              <h4 className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
                General Session Information
              </h4>

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
                <FormField
                  label="Room"
                  placeholder="Room 301, Alpha Building (optional)"
                  value={room}
                  onChange={(e) => setRoom(e.target.value)}
                />
                <FormField
                  label="Max Capacity"
                  inputMode="numeric"
                  value={maxCapacity}
                  error={errors.maxCapacity}
                  hint={inheritedSlots.length > 0 ? `Calculated across ${inheritedSlots.length} slots` : undefined}
                  onChange={(e) => {
                    setMaxCapacity(e.target.value.replace(/\D/g, ''))
                    if (errors.maxCapacity) setEditErrors((prev) => ({ ...prev, maxCapacity: '' }))
                  }}
                />
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                <FormField
                  label="Start Time (Session Kickoff)"
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
                  hint={`Auto-calculated: ${totalDurationMinutes} mins total duration`}
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
                    Balanced Random Distribution
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

              {selectedExamIds.length > 1 && (
                <p className="text-[11px] text-muted-foreground">
                  💡 Participating students will be distributed randomly and evenly across the {selectedExamIds.length} exam papers above.
                </p>
              )}
            </div>

            {/* Card 3: Time Slots (Inherited from Exam) */}
            <div className="rounded-2xl border bg-card p-4 space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Clock className="size-4 text-brand" />
                  <h4 className="text-sm font-semibold text-foreground">
                    Time Slots Configuration
                  </h4>
                </div>
                {inheritedSlots.length > 0 ? (
                  <span className="rounded-full bg-brand-soft px-2.5 py-0.5 text-xs font-semibold text-brand">
                    {inheritedSlots.length} slots inherited from Exam
                  </span>
                ) : (
                  <span className="rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-muted-foreground">
                    Continuous Exam Session
                  </span>
                )}
              </div>

              {inheritedSlots.length > 0 ? (
                <div className="space-y-3 pt-1">
                  <p className="text-xs text-muted-foreground">
                    Time slots are defined in the selected Exam. The session automatically schedules each slot sequentially from your Start Time.
                  </p>
                  <div className="grid gap-2 sm:grid-cols-2 max-h-[220px] overflow-y-auto rounded-xl border p-2 bg-muted/10">
                    {inheritedSlots.map((slot) => (
                      <div
                        key={slot.slotIndex}
                        className="flex flex-col gap-1.5 rounded-lg border bg-card p-2.5 text-xs shadow-xs"
                      >
                        <div className="flex items-center justify-between">
                          <span className="font-semibold text-foreground">{slot.slotName}</span>
                          <span className="font-mono text-brand font-medium">
                            {slot.startDisplay} – {slot.endDisplay}
                          </span>
                        </div>
                        <div className="flex items-center justify-between text-[11px] text-muted-foreground">
                          <span>Duration: {slot.durationMinutes} mins</span>
                          <span>Max Capacity: ~{slot.maxCapacity} students</span>
                        </div>
                        {slot.examPapers && slot.examPapers.length > 0 && (
                          <div className="flex flex-wrap items-center gap-1 pt-1 border-t border-border/50">
                            <span className="text-[10px] text-muted-foreground">Assigned papers:</span>
                            {slot.examPapers.map((ep) => (
                              <span
                                key={ep.examId}
                                className="rounded bg-brand-soft px-1.5 py-0.5 font-mono text-[10px] font-medium text-brand"
                              >
                                {ep.examCode}
                              </span>
                            ))}
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              ) : (
                <div className="rounded-xl border border-dashed p-4 text-center text-xs text-muted-foreground space-y-1">
                  <p className="font-medium text-foreground">Continuous Exam Session (No slots configured in Exam)</p>
                  <p>This exam paper does not have predefined slot templates. The entire session will run continuously for {totalDurationMinutes} minutes.</p>
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
                Enrolled students will be automatically balanced across the exam's time slots and exam papers.
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
  )
}
