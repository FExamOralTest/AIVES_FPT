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
  Shuffle,
  Users,
  X,
} from 'lucide-react'
import { useId, useState, type FormEvent } from 'react'
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
import type { CreateSessionSlotInput, ExamSession } from '../types'

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

  // Slots state
  const [enableSlots, setEnableSlots] = useState(false)
  const [slicingMode, setSlicingMode] = useState<'BY_STUDENTS' | 'BY_DURATION' | 'BY_COUNT'>('BY_STUDENTS')
  const [studentsPerSlot, setStudentsPerSlot] = useState(10)
  const [durationMinutes, setDurationMinutes] = useState(15)
  const [customSlotCount, setCustomSlotCount] = useState(5)
  const [slotExamMode, setSlotExamMode] = useState<'EXAM_PER_SLOT' | 'SHUFFLE_IN_SLOT'>('EXAM_PER_SLOT')

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
    return found ?? ({ examId: id, examCode: `ID #${id}`, title: 'Đề thi', durationMinutes: 0, maxQuestions: 0 } as Exam)
  })

  function addExam(examIdStr: string) {
    const id = Number(examIdStr)
    if (!id || selectedExamIds.includes(id)) return
    const exam = examList.find((e) => e.examId === id)
    const nextList = [...selectedExamIds, id]
    setSelectedExamIds(nextList)
    setPickerValue('')
    if (errors.examId) setEditErrors((prev) => ({ ...prev, examId: '' }))

    // Auto set end time if first exam
    if (exam && selectedExamIds.length === 0) {
      const s = new Date(startTime)
      if (!Number.isNaN(s.getTime())) {
        const e = toLocalInput(new Date(s.getTime() + exam.durationMinutes * 60_000))
        setEndTime(e)
      }
    }
  }

  function removeExam(id: number) {
    setSelectedExamIds((prev) => prev.filter((x) => x !== id))
  }

  // Calculate slots preview
  const calculatedSlots = (() => {
    if (!enableSlots) return []
    const start = new Date(startTime)
    const end = new Date(endTime)
    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime()) || end <= start) {
      return []
    }
    const totalMs = end.getTime() - start.getTime()
    const totalDurationMinutes = Math.max(1, Math.round(totalMs / 60000))
    const cap = Number(maxCapacity) || 50

    let finalSlotCount = 1
    if (slicingMode === 'BY_STUDENTS') {
      finalSlotCount = Math.max(1, Math.min(20, Math.ceil(cap / Math.max(1, studentsPerSlot))))
    } else if (slicingMode === 'BY_DURATION') {
      finalSlotCount = Math.max(1, Math.min(20, Math.floor(totalDurationMinutes / Math.max(1, durationMinutes))))
    } else {
      finalSlotCount = Math.max(1, Math.min(20, customSlotCount))
    }

    const slotMs = Math.floor(totalMs / finalSlotCount)
    const perSlotCap = Math.max(1, Math.ceil(cap / finalSlotCount))

    return Array.from({ length: finalSlotCount }, (_, i) => {
      const sStart = new Date(start.getTime() + i * slotMs)
      const sEnd = i === finalSlotCount - 1 ? end : new Date(start.getTime() + (i + 1) * slotMs)
      return {
        slotIndex: i + 1,
        slotName: `Kíp ${i + 1}`,
        startTime: sStart.toISOString(),
        endTime: sEnd.toISOString(),
        startDisplay: sStart.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        endDisplay: sEnd.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        durationMinutes: Math.round((sEnd.getTime() - sStart.getTime()) / 60000),
        maxCapacity: perSlotCap,
      }
    })
  })()

  // Parse entered tokens for students
  const studentTokens = studentText.split(/[\s,;]+/).map((t) => t.trim()).filter(Boolean)

  function validate() {
    const next: Record<string, string> = {}
    if (selectedExamIds.length === 0) next.examId = 'Vui lòng chọn ít nhất 1 bộ đề thi cho ca thi.'
    const name = sessionName.trim()
    if (!name) next.sessionName = 'Vui lòng nhập tên ca thi.'
    else if (name.length > 150) next.sessionName = 'Tên ca thi không được vượt quá 150 ký tự.'
    const start = new Date(startTime)
    const end = new Date(endTime)
    if (Number.isNaN(start.getTime())) next.start = 'Chọn thời gian bắt đầu.'
    if (Number.isNaN(end.getTime())) next.end = 'Chọn thời gian kết thúc.'
    else if (!next.start && end <= start) next.end = 'Thời gian kết thúc phải sau thời gian bắt đầu.'
    const cap = Number(maxCapacity)
    if (!/^\d+$/.test(maxCapacity) || cap < 1 || cap > 500) next.maxCapacity = 'Sĩ số tối đa từ 1 đến 500 sinh viên.'
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
    const slotsPayload: CreateSessionSlotInput[] | undefined = enableSlots
      ? calculatedSlots.map((s) => ({
          slotIndex: s.slotIndex,
          slotName: s.slotName,
          startTime: s.startTime,
          endTime: s.endTime,
          maxCapacity: s.maxCapacity,
        }))
      : undefined

    try {
      // 1. Create session
      const created = await sessionsApi.create({
        examId: selectedExamIds[0],
        examIds: selectedExamIds,
        sessionName: sessionName.trim(),
        room: room.trim() || undefined,
        startTime: startIso,
        endTime: endIso,
        maxCapacity: Number(maxCapacity),
        slots: slotsPayload,
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
        if (enableSlots && calculatedSlots.length > 0) {
          try {
            await sessionsApi.distributeSlots(created.sessionId, {
              strategy: 'SLOT_BALANCED',
              examAssignmentMode: slotExamMode,
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
            Tạo ca thi mới
          </span>
          {selectedExamIds.length > 1 && (
            <span className="rounded-full bg-blue-50 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300 border border-blue-200 px-2.5 py-0.5 text-xs font-medium">
              {selectedExamIds.length} bộ đề
            </span>
          )}
        </div>
        <DialogTitle className="mt-1.5 text-xl font-bold">Thiết lập Ca thi mới</DialogTitle>
        <DialogDescription className="text-xs text-muted-foreground mt-0.5">
          Cấu hình thông tin phòng thi, bộ đề thi, kíp thi và thêm thí sinh tham gia
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
          Thí sinh ca thi
          {(importFromCourse || studentTokens.length > 0) && (
            <span
              className={cn(
                'rounded-full px-2 py-0.5 text-xs font-semibold',
                activeTab === 'STUDENTS' ? 'bg-brand text-white' : 'bg-muted text-muted-foreground'
              )}
            >
              {importFromCourse ? 'Môn học' : studentTokens.length}
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
        {/* TAB 1: TỔNG QUAN & CA THI */}
        {/* ========================================================================= */}
        {activeTab === 'OVERVIEW' && (
          <div className="space-y-4">
            {/* Card 1: Thông tin cơ bản */}
            <div className="rounded-2xl border bg-card p-4 space-y-3">
              <h4 className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
                Thông tin chung ca thi
              </h4>

              <FormField
                label="Tên ca thi"
                placeholder="VD: Ca thi sáng 01 - Lớp SE1601"
                value={sessionName}
                error={errors.sessionName}
                onChange={(e) => {
                  setSessionName(e.target.value)
                  if (errors.sessionName) setEditErrors((prev) => ({ ...prev, sessionName: '' }))
                }}
              />

              <div className="grid gap-3 sm:grid-cols-[1fr_9rem]">
                <FormField
                  label="Phòng thi"
                  placeholder="Phòng 301, Tòa Alpha (tùy chọn)"
                  value={room}
                  onChange={(e) => setRoom(e.target.value)}
                />
                <FormField
                  label="Sức chứa tối đa"
                  inputMode="numeric"
                  value={maxCapacity}
                  error={errors.maxCapacity}
                  onChange={(e) => {
                    setMaxCapacity(e.target.value.replace(/\D/g, ''))
                    if (errors.maxCapacity) setEditErrors((prev) => ({ ...prev, maxCapacity: '' }))
                  }}
                />
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                <FormField
                  label="Thời gian bắt đầu"
                  type="datetime-local"
                  value={startTime}
                  error={errors.start}
                  onChange={(e) => {
                    setStartTime(e.target.value)
                    if (errors.start) setEditErrors((prev) => ({ ...prev, start: '' }))
                  }}
                />
                <FormField
                  label="Thời gian kết thúc"
                  type="datetime-local"
                  value={endTime}
                  error={errors.end}
                  onChange={(e) => {
                    setEndTime(e.target.value)
                    if (errors.end) setEditErrors((prev) => ({ ...prev, end: '' }))
                  }}
                />
              </div>
            </div>

            {/* Card 2: Bộ đề thi cấu hình */}
            <div className="rounded-2xl border bg-card p-4 space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Layers className="size-4 text-brand" />
                  <Label htmlFor={examSelectId} className="text-sm font-semibold">
                    Bộ đề thi sử dụng trong ca thi ({selectedExamIds.length})
                  </Label>
                </div>
                {selectedExamIds.length > 1 && (
                  <span className="rounded-full bg-brand-soft px-2.5 py-0.5 text-xs font-medium text-brand">
                    Trộn ngẫu nhiên & Cân bằng
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
                          title="Gỡ bỏ đề thi"
                        >
                          <X className="size-3.5" />
                        </button>
                      )}
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-xs text-muted-foreground italic">Chưa chọn bộ đề nào cho ca thi.</p>
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
                      ? 'Đang tải danh sách đề thi…'
                      : availableExams.length
                      ? '+ Chọn bộ đề thi để thêm vào ca thi'
                      : 'Đã thêm tất cả đề thi có sẵn'}
                  </option>
                  {availableExams.map((exam) => (
                    <option key={exam.examId} value={exam.examId}>
                      {exam.examCode} - {exam.title} ({exam.durationMinutes} phút)
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
                  Thêm đề
                </Button>
              </div>

              {errors.examId && <p className="text-xs text-destructive">{errors.examId}</p>}

              {selectedExamIds.length > 1 && (
                <p className="text-[11px] text-muted-foreground">
                  💡 Thí sinh khi tham gia ca thi sẽ được thuật toán phân bổ ngẫu nhiên và cân bằng đều cho {selectedExamIds.length} bộ đề trên.
                </p>
              )}
            </div>

            {/* Card 3: Cấu hình Kíp thi thông minh */}
            <div className="rounded-2xl border bg-card p-4 space-y-3">
              <div className="flex items-center justify-between">
                <label className="flex items-center gap-2 text-sm font-semibold cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={enableSlots}
                    onChange={(e) => setEnableSlots(e.target.checked)}
                    className="size-4 rounded border-gray-300 text-brand focus:ring-brand accent-brand"
                  />
                  <Clock className="size-4 text-brand" />
                  <span>Chia ca thi thành nhiều Kíp thi (Time Slots)</span>
                </label>
                {enableSlots && (
                  <span className="rounded-full bg-brand-soft px-2.5 py-0.5 text-xs font-semibold text-brand">
                    {calculatedSlots.length} kíp
                  </span>
                )}
              </div>

              {enableSlots && (
                <div className="space-y-3 pt-2 border-t">
                  {/* Mode selector */}
                  <div className="grid grid-cols-3 gap-2">
                    <button
                      type="button"
                      onClick={() => setSlicingMode('BY_STUDENTS')}
                      className={cn(
                        'flex flex-col items-start gap-1 rounded-xl border p-2.5 text-left transition-all',
                        slicingMode === 'BY_STUDENTS'
                          ? 'border-brand bg-brand-soft/50 ring-2 ring-brand/20 shadow-xs'
                          : 'border-input bg-card hover:bg-muted/50'
                      )}
                    >
                      <div className="flex items-center gap-1.5 text-xs font-bold text-foreground">
                        <Users className="size-3.5 text-brand" />
                        Theo số SV/kíp
                      </div>
                      <span className="text-[11px] text-muted-foreground">VD: 10 SV/kíp</span>
                    </button>

                    <button
                      type="button"
                      onClick={() => setSlicingMode('BY_DURATION')}
                      className={cn(
                        'flex flex-col items-start gap-1 rounded-xl border p-2.5 text-left transition-all',
                        slicingMode === 'BY_DURATION'
                          ? 'border-brand bg-brand-soft/50 ring-2 ring-brand/20 shadow-xs'
                          : 'border-input bg-card hover:bg-muted/50'
                      )}
                    >
                      <div className="flex items-center gap-1.5 text-xs font-bold text-foreground">
                        <Clock className="size-3.5 text-brand" />
                        Theo thời lượng
                      </div>
                      <span className="text-[11px] text-muted-foreground">VD: 15 phút/kíp</span>
                    </button>

                    <button
                      type="button"
                      onClick={() => setSlicingMode('BY_COUNT')}
                      className={cn(
                        'flex flex-col items-start gap-1 rounded-xl border p-2.5 text-left transition-all',
                        slicingMode === 'BY_COUNT'
                          ? 'border-brand bg-brand-soft/50 ring-2 ring-brand/20 shadow-xs'
                          : 'border-input bg-card hover:bg-muted/50'
                      )}
                    >
                      <div className="flex items-center gap-1.5 text-xs font-bold text-foreground">
                        <Layers className="size-3.5 text-brand" />
                        Theo số kíp
                      </div>
                      <span className="text-[11px] text-muted-foreground">VD: 5 kíp</span>
                    </button>
                  </div>

                  {/* Mode presets */}
                  <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border bg-muted/20 p-2.5 text-xs">
                    {slicingMode === 'BY_STUDENTS' && (
                      <>
                        <span className="text-muted-foreground">Sĩ số mỗi kíp:</span>
                        <div className="flex items-center gap-1">
                          {[5, 10, 12, 15, 20].map((num) => (
                            <button
                              key={num}
                              type="button"
                              onClick={() => setStudentsPerSlot(num)}
                              className={cn(
                                'h-6 px-2 rounded-md text-xs font-medium border transition-colors',
                                studentsPerSlot === num
                                  ? 'bg-brand text-white border-brand shadow-xs'
                                  : 'bg-background hover:bg-muted text-muted-foreground'
                              )}
                            >
                              {num} SV
                            </button>
                          ))}
                        </div>
                      </>
                    )}

                    {slicingMode === 'BY_DURATION' && (
                      <>
                        <span className="text-muted-foreground">Thời lượng mỗi kíp:</span>
                        <div className="flex items-center gap-1">
                          {[10, 15, 20, 30].map((mins) => (
                            <button
                              key={mins}
                              type="button"
                              onClick={() => setDurationMinutes(mins)}
                              className={cn(
                                'h-6 px-2 rounded-md text-xs font-medium border transition-colors',
                                durationMinutes === mins
                                  ? 'bg-brand text-white border-brand shadow-xs'
                                  : 'bg-background hover:bg-muted text-muted-foreground'
                              )}
                            >
                              {mins}m
                            </button>
                          ))}
                        </div>
                      </>
                    )}

                    {slicingMode === 'BY_COUNT' && (
                      <>
                        <span className="text-muted-foreground">Số lượng kíp thi:</span>
                        <div className="flex items-center gap-1">
                          {[2, 3, 4, 5, 6, 8].map((n) => (
                            <button
                              key={n}
                              type="button"
                              onClick={() => setCustomSlotCount(n)}
                              className={cn(
                                'h-6 px-2 rounded-md text-xs font-medium border transition-colors',
                                customSlotCount === n
                                  ? 'bg-brand text-white border-brand shadow-xs'
                                  : 'bg-background hover:bg-muted text-muted-foreground'
                              )}
                            >
                              {n}
                            </button>
                          ))}
                        </div>
                      </>
                    )}
                  </div>

                  {/* Slot exam mode */}
                  {selectedExamIds.length > 1 && (
                    <div className="flex items-center justify-between gap-2 rounded-xl border bg-muted/10 p-2.5 text-xs">
                      <span className="font-medium text-foreground flex items-center gap-1.5">
                        <Shuffle className="size-3.5 text-brand" />
                        Chế độ gán đề cho kíp:
                      </span>
                      <select
                        value={slotExamMode}
                        onChange={(e) => setSlotExamMode(e.target.value as 'EXAM_PER_SLOT' | 'SHUFFLE_IN_SLOT')}
                        className="h-7 rounded-lg border border-input bg-background px-2 text-xs font-medium outline-none focus:border-brand"
                      >
                        <option value="EXAM_PER_SLOT">Mỗi kíp 1 đề (Cố định đề)</option>
                        <option value="SHUFFLE_IN_SLOT">Trộn đề trong từng kíp</option>
                      </select>
                    </div>
                  )}

                  {/* Calculated preview */}
                  {calculatedSlots.length > 0 && (
                    <div className="space-y-1.5 pt-1">
                      <span className="text-xs font-medium text-muted-foreground">
                        Khung giờ dự kiến ({calculatedSlots.length} kíp):
                      </span>
                      <div className="grid gap-2 sm:grid-cols-2 max-h-[160px] overflow-y-auto rounded-xl border p-2 bg-muted/10">
                        {calculatedSlots.map((slot) => (
                          <div
                            key={slot.slotIndex}
                            className="flex items-center justify-between rounded-lg border bg-card px-2.5 py-1.5 text-xs shadow-xs"
                          >
                            <div className="flex items-center gap-2">
                              <span className="font-semibold text-foreground">{slot.slotName}</span>
                              <span className="font-mono text-brand font-medium">
                                {slot.startDisplay} – {slot.endDisplay}
                              </span>
                            </div>
                            <span className="text-[11px] text-muted-foreground">
                              {slot.durationMinutes}m · ~{slot.maxCapacity} SV
                            </span>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        )}

        {/* ========================================================================= */}
        {/* TAB 2: THÍ SINH CA THI */}
        {/* ========================================================================= */}
        {activeTab === 'STUDENTS' && (
          <div className="space-y-4">
            {/* Option A: Nhập từ môn học */}
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
                      Tự động nạp toàn bộ sinh viên môn học vào ca thi
                    </span>
                  </div>
                  <p className="text-xs text-muted-foreground mt-0.5">
                    Hệ thống sẽ tự động quét danh sách sinh viên đã ghi danh vào môn học tương ứng và đưa vào ca thi ngay sau khi tạo.
                  </p>
                </div>
              </label>
            </div>

            {/* Option B: Dán MSSV/Email */}
            <div className="rounded-2xl border bg-card p-4 space-y-3">
              <div className="flex items-center justify-between">
                <Label htmlFor="create-students-input" className="text-sm font-semibold text-foreground flex items-center gap-1.5">
                  <Users className="size-4 text-brand" />
                  Dán danh sách sinh viên bổ sung (MSSV, Email, ID)
                </Label>
                {studentTokens.length > 0 && (
                  <span className="rounded-full bg-brand-soft px-2.5 py-0.5 text-xs font-semibold text-brand">
                    {studentTokens.length} sinh viên
                  </span>
                )}
              </div>

              <textarea
                id="create-students-input"
                rows={3}
                value={studentText}
                onChange={(e) => setStudentText(e.target.value)}
                placeholder="VD: SE160001, SE160002, he170003@fpt.edu.vn, 12... (Phân cách bởi dấu phẩy, khoảng trắng hoặc xuống dòng)"
                className="w-full rounded-xl border border-input bg-background p-2.5 text-xs outline-none resize-y min-h-[64px] focus:border-brand focus:ring-3 focus:ring-brand/15"
              />

              <p className="text-[11px] text-muted-foreground">
                Thí sinh được nạp sẽ tự động được phân chia đều vào các Kíp thi và các Bộ đề thi đã chọn.
              </p>

              {/* Tokens chips preview */}
              {studentTokens.length > 0 && (
                <div className="space-y-1.5 pt-1 border-t">
                  <span className="text-xs font-medium text-muted-foreground">
                    Danh sách đã nhận diện ({studentTokens.length}):
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
                        … và {studentTokens.length - 30} thí sinh khác
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
          Hủy
        </Button>

        {activeTab === 'OVERVIEW' ? (
          <>
            <Button
              type="button"
              variant="outline"
              onClick={() => setActiveTab('STUDENTS')}
              className="gap-1.5 text-xs"
            >
              Tiếp tục: Thí sinh ca thi
              <ArrowRight className="size-3.5" />
            </Button>
            <Button type="submit" disabled={submitting} className="gap-1.5">
              {submitting ? <Loader2 className="size-4 animate-spin" /> : null}
              {submitting ? 'Đang tạo…' : 'Tạo ca thi'}
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
              Quay lại thông tin
            </Button>
            <Button type="submit" disabled={submitting} className="gap-1.5">
              {submitting ? <Loader2 className="size-4 animate-spin" /> : null}
              {submitting
                ? 'Đang tạo & nạp sinh viên…'
                : `Tạo ca thi ${studentTokens.length > 0 ? `(${studentTokens.length} SV)` : ''}`}
            </Button>
          </>
        )}
      </DialogFooter>
    </form>
  )
}
