import { CircleAlert, Clock, Layers, Plus, Users, X } from 'lucide-react'
import { useId, useState, type FormEvent } from 'react'
import { FormField } from '@/components/common/FormField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { examsApi, type Exam } from '@/features/exams'
import { parseApiDate } from '@/lib/format/date'
import { toApiError } from '@/lib/http/apiError'
import { useApi } from '@/lib/http/useApi'
import { cn } from '@/lib/utils'
import { sessionsApi } from '../api/sessionsApi'
import type { ExamSession } from '../types'

interface CreateSessionDialogProps {
  open: boolean
  /** Preselect an exam, e.g. when coming from the exam detail's "Create a session" button. */
  initialExamId: number | null
  onClose: () => void
  onCreated: (session: ExamSession) => void
}

export function CreateSessionDialog({ open, initialExamId, onClose, onCreated }: CreateSessionDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-xl">{open && <SessionForm initialExamId={initialExamId} onSaved={onCreated} />}</DialogContent>
    </Dialog>
  )
}

type Field = 'sessionName' | 'room' | 'start' | 'end' | 'maxCapacity'

const SELECT_CLASS =
  'h-11 w-full rounded-[14px] border border-input bg-background px-4 text-sm outline-none focus-visible:border-brand focus-visible:ring-4 focus-visible:ring-brand/15'

interface SessionFormProps {
  /** Edit this session instead of creating one. */
  session?: ExamSession
  initialExamId?: number | null
  onSaved: (session: ExamSession) => void
  /** Edit mode renders inside the detail dialog, so Cancel goes back to it instead of closing. */
  onCancel?: () => void
}

/** Create (no `session`) or edit form; render it inside a DialogContent. */
export function SessionForm({ session, initialExamId = null, onSaved, onCancel }: SessionFormProps) {
  const examSelectId = useId()
  const isEdit = Boolean(session)
  const notStarted = !session || session.status === 'NOT_STARTED'

  const { state: exams } = useApi(
    () => examsApi.listMine(),
    'session-exams',
    'Could not load exams.',
  )

  const [selectedExamIds, setSelectedExamIds] = useState<number[]>(() => {
    if (session?.exams && session.exams.length > 0) {
      return session.exams.map((e) => e.examId)
    }
    if (session?.examId) {
      return [session.examId]
    }
    if (initialExamId) {
      return [initialExamId]
    }
    return []
  })

  const [values, setValues] = useState<Record<Field, string>>(() => {
    if (session) {
      return {
        sessionName: session.sessionName,
        room: session.room ?? '',
        start: toLocalInput(parseApiDate(session.startTime)),
        end: toLocalInput(parseApiDate(session.endTime)),
        maxCapacity: String(session.maxCapacity),
      }
    }
    const start = nextHalfHour()
    return {
      sessionName: '',
      room: '',
      start: toLocalInput(start),
      end: toLocalInput(new Date(start.getTime() + 60 * 60_000)),
      maxCapacity: '50',
    }
  })
  const [initialStart] = useState(values.start)
  const [errors, setErrors] = useState<Partial<Record<Field | 'examId', string>>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [pickerValue, setPickerValue] = useState('')
  const [enableSlots, setEnableSlots] = useState<boolean>(() => {
    return Boolean(session?.slots && session.slots.length > 0)
  })
  const [slicingMode, setSlicingMode] = useState<'BY_STUDENTS' | 'BY_DURATION' | 'BY_COUNT'>('BY_STUDENTS')
  const [studentsPerSlot, setStudentsPerSlot] = useState<number>(10)
  const [durationMinutes, setDurationMinutes] = useState<number>(15)
  const [slotCount, setSlotCount] = useState<number>(() => {
    return session?.slots?.length && session.slots.length > 0 ? session.slots.length : 5
  })

  const set = (field: Field, value: string) => {
    setValues((prev) => ({ ...prev, [field]: value }))
    if (errors[field]) setErrors((prev) => ({ ...prev, [field]: undefined }))
  }

  function addExam(examIdStr: string, list: Exam[]) {
    const id = Number(examIdStr)
    if (!id || selectedExamIds.includes(id)) return
    const exam = list.find((e) => e.examId === id)
    const nextList = [...selectedExamIds, id]
    setSelectedExamIds(nextList)
    setPickerValue('')
    if (errors.examId) setErrors((prev) => ({ ...prev, examId: undefined }))

    // Auto update end time if duration available
    if (exam && selectedExamIds.length === 0) {
      const start = new Date(values.start)
      if (!Number.isNaN(start.getTime())) {
        const end = toLocalInput(new Date(start.getTime() + exam.durationMinutes * 60_000))
        setValues((prev) => ({ ...prev, end }))
      }
    }
  }

  function removeExam(id: number) {
    if (!notStarted) return
    const next = selectedExamIds.filter((x) => x !== id)
    setSelectedExamIds(next)
  }

  function validate() {
    const next: Partial<Record<Field | 'examId', string>> = {}
    if (selectedExamIds.length === 0) next.examId = 'Vui lòng chọn ít nhất 1 bộ đề thi cho ca thi.'
    const name = values.sessionName.trim()
    if (!name) next.sessionName = 'Enter a session name.'
    else if (name.length > 150) next.sessionName = 'Session name can be at most 150 characters.'
    if (values.room.trim().length > 150) next.room = 'Room can be at most 150 characters.'
    const start = new Date(values.start)
    const end = new Date(values.end)
    if (Number.isNaN(start.getTime())) next.start = 'Choose a start time.'
    else if (values.start !== initialStart || !isEdit) {
      if (start.getTime() < Date.now() - 5 * 60_000) next.start = 'The start time cannot be in the past.'
    }
    if (Number.isNaN(end.getTime())) next.end = 'Choose an end time.'
    else if (!next.start && end <= start) next.end = 'The end time must be after the start time.'
    const cap = Number(values.maxCapacity)
    if (!/^\d+$/.test(values.maxCapacity) || cap < 1 || cap > 500) next.maxCapacity = 'Capacity must be between 1 and 500.'
    else if (session && cap < session.enrolledStudents)
      next.maxCapacity = `Capacity cannot be below the ${session.enrolledStudents} students already enrolled.`
    return next
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const next = validate()
    setErrors(next)
    setFormError(null)
    if (Object.keys(next).length) return

    setSubmitting(true)
    const startTime = new Date(values.start).toISOString()
    const endTime = new Date(values.end).toISOString()
    const slotsPayload = enableSlots
      ? calculatedSlots.map((s) => ({
          slotIndex: s.slotIndex,
          slotName: s.slotName,
          startTime: s.startTime,
          endTime: s.endTime,
          maxCapacity: s.maxCapacity,
        }))
      : isEdit
        ? []
        : undefined

    try {
      onSaved(
        session
          ? await sessionsApi.update(session.sessionId, {
              sessionName: values.sessionName.trim(),
              room: values.room.trim(),
              startTime,
              endTime,
              maxCapacity: Number(values.maxCapacity),
              examIds: selectedExamIds.length > 0 ? selectedExamIds : undefined,
              slots: slotsPayload,
            })
          : await sessionsApi.create({
              examId: selectedExamIds[0],
              examIds: selectedExamIds,
              sessionName: values.sessionName.trim(),
              room: values.room.trim() || undefined,
              startTime,
              endTime,
              maxCapacity: Number(values.maxCapacity),
              slots: slotsPayload,
            }),
      )
    } catch (err) {
      const apiError = toApiError(err)
      setErrors(apiError.fieldErrors as Partial<Record<Field | 'examId', string>>)
      setFormError(apiError.message)
    } finally {
      setSubmitting(false)
    }
  }

  const examList = exams.status === 'ready' ? exams.data : []
  const availableExams = examList.filter((e) => !selectedExamIds.includes(e.examId))
  const selectedExamsData = selectedExamIds.map((id) => {
    const foundInList = examList.find((e) => e.examId === id)
    if (foundInList) return foundInList
    const foundInSession = session?.exams?.find((e) => e.examId === id)
    if (foundInSession) {
      return {
        examId: foundInSession.examId,
        examCode: foundInSession.examCode,
        title: foundInSession.title,
        durationMinutes: foundInSession.durationMinutes,
        maxQuestions: foundInSession.totalQuestions,
      } as Exam
    }
    if (session?.examId === id) {
      return {
        examId: session.examId,
        examCode: session.examCode,
        title: session.examTitle,
        durationMinutes: session.durationMinutes,
        maxQuestions: 0,
      } as Exam
    }
    return { examId: id, examCode: `ID #${id}`, title: 'Đề thi', durationMinutes: 0, maxQuestions: 0 } as Exam
  })

  const calculatedSlots = (() => {
    if (!enableSlots) return []
    const start = new Date(values.start)
    const end = new Date(values.end)
    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime()) || end <= start) {
      return []
    }
    const totalMs = end.getTime() - start.getTime()
    const totalDurationMinutes = Math.max(1, Math.round(totalMs / 60000))
    const cap = Number(values.maxCapacity) || 50

    let finalSlotCount = 1
    if (slicingMode === 'BY_STUDENTS') {
      finalSlotCount = Math.max(1, Math.min(20, Math.ceil(cap / Math.max(1, studentsPerSlot))))
    } else if (slicingMode === 'BY_DURATION') {
      finalSlotCount = Math.max(1, Math.min(20, Math.floor(totalDurationMinutes / Math.max(1, durationMinutes))))
    } else {
      finalSlotCount = Math.max(1, Math.min(20, slotCount))
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

  return (
    <form onSubmit={handleSubmit} noValidate className="contents">
      <DialogHeader>
        <DialogTitle>{isEdit ? 'Chỉnh sửa ca thi' : 'Tạo ca thi mới'}</DialogTitle>
        <DialogDescription>
          {isEdit
            ? 'Cập nhật thông tin ca thi, phòng thi, cấu hình đề thi hoặc sức chứa.'
            : 'Hệ thống hỗ trợ cấu hình nhiều bộ đề cho 1 ca thi và tự động phân bổ ngẫu nhiên, cân bằng cho thí sinh.'}
        </DialogDescription>
      </DialogHeader>

      {formError && (
        <Alert variant="destructive" className="rounded-[14px] border-0 bg-[#fef1f0] px-4 py-3.5">
          <CircleAlert />
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      )}

      <div className="flex flex-col gap-4">
        {/* Multi-exam picker section */}
        <div className="flex flex-col gap-2 rounded-2xl border p-4 bg-muted/30">
          <div className="flex items-center justify-between">
            <Label htmlFor={examSelectId} className="text-sm font-semibold">
              Bộ đề thi sử dụng trong ca thi ({selectedExamIds.length})
            </Label>
            {selectedExamIds.length > 1 && (
              <span className="rounded-full bg-brand-soft px-2.5 py-0.5 text-xs font-medium text-brand">
                Trộn ngẫu nhiên & Cân bằng
              </span>
            )}
          </div>

          {/* Selected exam chips */}
          {selectedExamsData.length > 0 ? (
            <div className="flex flex-wrap gap-2 pt-1">
              {selectedExamsData.map((e) => (
                <div
                  key={e.examId}
                  className="flex items-center gap-2 rounded-xl border bg-background px-3 py-1.5 text-xs shadow-sm"
                >
                  <span className="font-mono font-semibold text-brand">{e.examCode}</span>
                  <span className="max-w-[140px] truncate font-medium text-foreground">{e.title}</span>
                  <span className="text-muted-foreground">({e.durationMinutes}m)</span>
                  {notStarted && selectedExamIds.length > 1 && (
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

          {/* Dropdown to add another exam */}
          {notStarted && (
            <div className="mt-2 flex gap-2">
              <select
                id={examSelectId}
                value={pickerValue}
                onChange={(e) => setPickerValue(e.target.value)}
                disabled={exams.status !== 'ready' || availableExams.length === 0}
                className={cn(SELECT_CLASS, errors.examId && 'border-destructive ring-2 ring-destructive/20')}
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
                    {exam.examCode} - {exam.title} ({exam.durationMinutes}m)
                  </option>
                ))}
              </select>
              <Button
                type="button"
                variant="secondary"
                size="sm"
                className="shrink-0 h-11 px-4"
                disabled={!pickerValue}
                onClick={() => addExam(pickerValue, examList)}
              >
                <Plus className="size-4 mr-1" />
                Thêm đề
              </Button>
            </div>
          )}

          {errors.examId && (
            <p className="text-[13px] text-destructive">{errors.examId}</p>
          )}

          {selectedExamIds.length > 1 && (
            <p className="text-xs text-muted-foreground">
              💡 Thí sinh khi tham gia ca thi sẽ được thuật toán phân bổ ngẫu nhiên và cân bằng đều cho {selectedExamIds.length} bộ đề trên (đảm bảo độ chênh lệch giữa các đề ≤ 1 thí sinh).
            </p>
          )}
        </div>

        <FormField
          label="Tên ca thi"
          placeholder="Ca thi sáng 01 - Nhóm 1"
          value={values.sessionName}
          error={errors.sessionName}
          onChange={(e) => set('sessionName', e.target.value)}
        />
        <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
          <FormField
            label="Phòng thi"
            placeholder="Phòng 301, Tòa Alpha (tùy chọn)"
            value={values.room}
            error={errors.room}
            onChange={(e) => set('room', e.target.value)}
          />
          <FormField
            label="Sức chứa tối đa"
            inputMode="numeric"
            value={values.maxCapacity}
            error={errors.maxCapacity}
            onChange={(e) => set('maxCapacity', e.target.value.replace(/\D/g, ''))}
          />
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField
            label="Thời gian bắt đầu"
            type="datetime-local"
            value={values.start}
            error={errors.start}
            onChange={(e) => set('start', e.target.value)}
          />
          <FormField
            label="Thời gian kết thúc"
            type="datetime-local"
            value={values.end}
            error={errors.end}
            onChange={(e) => set('end', e.target.value)}
          />
        </div>

        {/* Time Slots (Kíp thi) Auto-slicer Section */}
        <div className="flex flex-col gap-3 rounded-2xl border p-4 bg-muted/20">
          <div className="flex items-center justify-between">
            <label className="flex items-center gap-2 text-sm font-semibold cursor-pointer select-none">
              <input
                type="checkbox"
                checked={enableSlots}
                disabled={!notStarted}
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
            <div className="flex flex-col gap-3 pt-2">
              {/* Slicing Mode Selector */}
              <div className="grid grid-cols-3 gap-2">
                <button
                  type="button"
                  disabled={!notStarted}
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
                  disabled={!notStarted}
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
                  disabled={!notStarted}
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

              {/* Mode Presets */}
              <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border bg-background p-2.5 text-xs">
                {slicingMode === 'BY_STUDENTS' && (
                  <>
                    <span className="text-muted-foreground">Sĩ số mỗi kíp:</span>
                    <div className="flex items-center gap-1">
                      {[5, 10, 12, 15, 20].map((num) => (
                        <button
                          key={num}
                          type="button"
                          disabled={!notStarted}
                          onClick={() => setStudentsPerSlot(num)}
                          className={cn(
                            'h-6 px-2 rounded-md text-xs font-medium border transition-colors',
                            studentsPerSlot === num
                              ? 'bg-brand text-white border-brand shadow-xs'
                              : 'bg-muted/40 hover:bg-muted text-muted-foreground'
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
                          disabled={!notStarted}
                          onClick={() => setDurationMinutes(mins)}
                          className={cn(
                            'h-6 px-2 rounded-md text-xs font-medium border transition-colors',
                            durationMinutes === mins
                              ? 'bg-brand text-white border-brand shadow-xs'
                              : 'bg-muted/40 hover:bg-muted text-muted-foreground'
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
                          disabled={!notStarted}
                          onClick={() => setSlotCount(n)}
                          className={cn(
                            'h-6 px-2 rounded-md text-xs font-medium border transition-colors',
                            slotCount === n
                              ? 'bg-brand text-white border-brand shadow-xs'
                              : 'bg-muted/40 hover:bg-muted text-muted-foreground'
                          )}
                        >
                          {n}
                        </button>
                      ))}
                    </div>
                  </>
                )}
              </div>

              {/* Calculated preview */}
              {calculatedSlots.length > 0 && (
                <div className="space-y-1.5 pt-1">
                  <span className="text-xs font-medium text-muted-foreground">Khung giờ dự kiến ({calculatedSlots.length} kíp):</span>
                  <div className="grid gap-2 sm:grid-cols-2 max-h-[180px] overflow-y-auto rounded-xl border p-2 bg-background">
                    {calculatedSlots.map((slot) => (
                      <div
                        key={slot.slotIndex}
                        className="flex items-center justify-between rounded-lg border bg-muted/20 px-2.5 py-1.5 text-xs"
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
                  <p className="text-[11px] text-muted-foreground pt-1">
                    💡 Thí sinh kíp nào chỉ được vào phòng thi đúng khung giờ của kíp đó.
                  </p>
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      <DialogFooter>
        {onCancel ? (
          <Button type="button" variant="secondary" onClick={onCancel}>
            Hủy
          </Button>
        ) : (
          <DialogClose asChild>
            <Button type="button" variant="secondary">
              Hủy
            </Button>
          </DialogClose>
        )}
        <Button type="submit" disabled={submitting}>
          {isEdit ? (submitting ? 'Đang lưu…' : 'Lưu thay đổi') : submitting ? 'Đang tạo…' : 'Tạo ca thi'}
        </Button>
      </DialogFooter>
    </form>
  )
}

function nextHalfHour() {
  const d = new Date()
  d.setSeconds(0, 0)
  d.setMinutes(d.getMinutes() < 30 ? 30 : 60)
  return d
}

/** Date → "YYYY-MM-DDTHH:mm" in local time, the format <input type="datetime-local"> uses. */
function toLocalInput(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}
