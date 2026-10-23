import {
  AlertCircle,
  Check,
  Clock,
  Layers,
  Loader2,
  Shuffle,
  Sparkles,
  Users,
} from 'lucide-react'
import { useId, useState } from 'react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { formatTimeRange, parseApiDate } from '@/lib/format/date'
import { toApiError } from '@/lib/http/apiError'
import { cn } from '@/lib/utils'
import { sessionsApi } from '../api/sessionsApi'
import type { CreateSessionSlotInput, ExamSession } from '../types'

export type SlicingMode = 'BY_STUDENTS' | 'BY_DURATION' | 'BY_COUNT'

interface ConfigureSlotsDialogProps {
  open: boolean
  session: ExamSession
  onClose: () => void
  onSaved: (updated: ExamSession) => void
}

export function ConfigureSlotsDialog({
  open,
  session,
  onClose,
  onSaved,
}: ConfigureSlotsDialogProps) {
  const modeId = useId()
  const [slicingMode, setSlicingMode] = useState<SlicingMode>('BY_STUDENTS')
  const [studentsPerSlot, setStudentsPerSlot] = useState<number>(10)
  const [durationMinutes, setDurationMinutes] = useState<number>(15)
  const [customSlotCount, setCustomSlotCount] = useState<number>(
    session.slots && session.slots.length > 0 ? session.slots.length : 5
  )
  const [examAssignmentMode, setExamAssignmentMode] = useState<'EXAM_PER_SLOT' | 'SHUFFLE_IN_SLOT'>('EXAM_PER_SLOT')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const start = parseApiDate(session.startTime)
  const end = parseApiDate(session.endTime)
  const totalMs = Math.max(0, end.getTime() - start.getTime())
  const totalDurationMinutes = Math.max(1, Math.round(totalMs / 60000))

  const studentCount = session.enrolledStudents > 0 ? session.enrolledStudents : session.maxCapacity
  const exams = session.exams ?? []
  const hasMultipleExams = exams.length > 1

  // Compute calculated slots based on selected mode
  const calculated = (() => {
    let finalSlotCount = 1

    if (slicingMode === 'BY_STUDENTS') {
      const perSlot = Math.max(1, studentsPerSlot)
      finalSlotCount = Math.max(1, Math.min(20, Math.ceil(studentCount / perSlot)))
    } else if (slicingMode === 'BY_DURATION') {
      const perSlotMin = Math.max(1, durationMinutes)
      finalSlotCount = Math.max(1, Math.min(20, Math.floor(totalDurationMinutes / perSlotMin)))
    } else {
      finalSlotCount = Math.max(1, Math.min(20, customSlotCount))
    }

    const slotMs = Math.floor(totalMs / finalSlotCount)
    const perSlotCap = Math.max(1, Math.ceil(studentCount / finalSlotCount))

    const slots: (CreateSessionSlotInput & {
      durationMinutes: number
      startDisplay: string
      endDisplay: string
      assignedExamSummary?: string
    })[] = []

    for (let i = 0; i < finalSlotCount; i++) {
      const sStart = new Date(start.getTime() + i * slotMs)
      const sEnd = i === finalSlotCount - 1 ? end : new Date(start.getTime() + (i + 1) * slotMs)
      const slotDuration = Math.max(1, Math.round((sEnd.getTime() - sStart.getTime()) / 60000))

      let assignedExamSummary: string | undefined
      if (hasMultipleExams && examAssignmentMode === 'EXAM_PER_SLOT') {
        const assignedExam = exams[i % exams.length]
        assignedExamSummary = `${assignedExam.examCode} - ${assignedExam.title}`
      }

      slots.push({
        slotIndex: i + 1,
        slotName: `Kíp ${i + 1}`,
        startTime: sStart.toISOString(),
        endTime: sEnd.toISOString(),
        maxCapacity: perSlotCap,
        durationMinutes: slotDuration,
        startDisplay: sStart.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        endDisplay: sEnd.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        assignedExamSummary,
      })
    }

    return {
      slotCount: finalSlotCount,
      slots,
      perSlotCap,
    }
  })()

  async function handleApply() {
    setSaving(true)
    setError(null)
    try {
      const slotsPayload: CreateSessionSlotInput[] = calculated.slots.map((s) => ({
        slotIndex: s.slotIndex,
        slotName: s.slotName,
        startTime: s.startTime,
        endTime: s.endTime,
        maxCapacity: s.maxCapacity,
      }))

      // 1. Update session slots
      await sessionsApi.update(session.sessionId, {
        sessionName: session.sessionName,
        room: session.room ?? '',
        startTime: session.startTime,
        endTime: session.endTime,
        maxCapacity: session.maxCapacity,
        slots: slotsPayload,
      })

      // 2. If there are enrolled students, distribute them automatically
      if (session.enrolledStudents > 0) {
        await sessionsApi.distributeSlots(session.sessionId, {
          strategy: 'SLOT_BALANCED',
          examAssignmentMode,
        })
      }

      // 3. Fetch fresh session detail
      const updated = await sessionsApi.get(session.sessionId)
      onSaved(updated)
      onClose()
    } catch (err) {
      setError(toApiError(err, 'Không thể lưu cấu hình kíp thi.').message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <div className="flex items-center gap-2">
            <span className="flex size-8 items-center justify-center rounded-xl bg-brand-soft text-brand">
              <Sparkles className="size-4" />
            </span>
            <div>
              <DialogTitle className="text-lg font-bold">Cấu hình & Chia Kíp thi thông minh</DialogTitle>
              <DialogDescription className="text-xs">
                Tự động tính toán khung giờ, chia đều thí sinh và phân bổ đề thi cho từng kíp.
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        {error && (
          <Alert variant="destructive" className="py-2.5">
            <AlertCircle className="size-4" />
            <AlertDescription className="text-xs">{error}</AlertDescription>
          </Alert>
        )}

        <div className="space-y-4 pt-1">
          {/* Quick Context Summary Banner */}
          <div className="grid grid-cols-3 gap-2 rounded-xl border bg-muted/30 p-3 text-xs">
            <div>
              <span className="text-muted-foreground block">Thời gian ca thi</span>
              <span className="font-semibold text-foreground">
                {formatTimeRange(start, end)} ({totalDurationMinutes} phút)
              </span>
            </div>
            <div>
              <span className="text-muted-foreground block">Thí sinh hiện tại</span>
              <span className="font-semibold text-foreground">
                {session.enrolledStudents} SV (tối đa {session.maxCapacity})
              </span>
            </div>
            <div>
              <span className="text-muted-foreground block">Bộ đề thi</span>
              <span className="font-semibold text-foreground">
                {exams.length > 0 ? `${exams.length} bộ đề` : '1 bộ đề'}
              </span>
            </div>
          </div>

          {/* Mode Selector Tabs */}
          <div className="space-y-2">
            <label id={modeId} className="text-xs font-semibold text-foreground">
              Phương thức chia kíp thi:
            </label>
            <div className="grid grid-cols-3 gap-2">
              <button
                type="button"
                onClick={() => setSlicingMode('BY_STUDENTS')}
                className={cn(
                  'flex flex-col items-start gap-1 rounded-xl border p-3 text-left transition-all',
                  slicingMode === 'BY_STUDENTS'
                    ? 'border-brand bg-brand-soft/50 ring-2 ring-brand/20 shadow-xs'
                    : 'border-input bg-card hover:bg-muted/50'
                )}
              >
                <div className="flex items-center gap-1.5 text-xs font-bold text-foreground">
                  <Users className="size-3.5 text-brand" />
                  Theo số SV/kíp
                </div>
                <p className="text-[11px] text-muted-foreground leading-snug">
                  Cố định sĩ số (VD: 10 SV/kíp), tự tính số kíp
                </p>
              </button>

              <button
                type="button"
                onClick={() => setSlicingMode('BY_DURATION')}
                className={cn(
                  'flex flex-col items-start gap-1 rounded-xl border p-3 text-left transition-all',
                  slicingMode === 'BY_DURATION'
                    ? 'border-brand bg-brand-soft/50 ring-2 ring-brand/20 shadow-xs'
                    : 'border-input bg-card hover:bg-muted/50'
                )}
              >
                <div className="flex items-center gap-1.5 text-xs font-bold text-foreground">
                  <Clock className="size-3.5 text-brand" />
                  Theo thời lượng
                </div>
                <p className="text-[11px] text-muted-foreground leading-snug">
                  Cố định phút/kíp (VD: 15p), tự xếp ca liên tiếp
                </p>
              </button>

              <button
                type="button"
                onClick={() => setSlicingMode('BY_COUNT')}
                className={cn(
                  'flex flex-col items-start gap-1 rounded-xl border p-3 text-left transition-all',
                  slicingMode === 'BY_COUNT'
                    ? 'border-brand bg-brand-soft/50 ring-2 ring-brand/20 shadow-xs'
                    : 'border-input bg-card hover:bg-muted/50'
                )}
              >
                <div className="flex items-center gap-1.5 text-xs font-bold text-foreground">
                  <Layers className="size-3.5 text-brand" />
                  Theo số kíp
                </div>
                <p className="text-[11px] text-muted-foreground leading-snug">
                  Chọn số kíp cố định (VD: 5 kíp), chia đều giờ
                </p>
              </button>
            </div>
          </div>

          {/* Mode Configuration Controls */}
          <div className="rounded-2xl border bg-muted/20 p-3.5 space-y-3">
            {slicingMode === 'BY_STUDENTS' && (
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <span className="text-xs font-semibold text-foreground">Sĩ số mục tiêu cho mỗi kíp:</span>
                  <p className="text-[11px] text-muted-foreground">
                    Hệ thống sẽ lấy {studentCount} SV chia cho {studentsPerSlot} SV/kíp = {calculated.slotCount} kíp.
                  </p>
                </div>
                <div className="flex items-center gap-1.5">
                  {[5, 10, 12, 15, 20, 25].map((num) => (
                    <button
                      key={num}
                      type="button"
                      onClick={() => setStudentsPerSlot(num)}
                      className={cn(
                        'h-7 px-2.5 rounded-lg text-xs font-medium border transition-colors',
                        studentsPerSlot === num
                          ? 'bg-brand text-white border-brand shadow-xs'
                          : 'bg-background hover:bg-muted text-muted-foreground'
                      )}
                    >
                      {num} SV
                    </button>
                  ))}
                </div>
              </div>
            )}

            {slicingMode === 'BY_DURATION' && (
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <span className="text-xs font-semibold text-foreground">Thời lượng thi mỗi kíp:</span>
                  <p className="text-[11px] text-muted-foreground">
                    Tổng {totalDurationMinutes} phút chia cho {durationMinutes} phút/kíp = {calculated.slotCount} kíp.
                  </p>
                </div>
                <div className="flex items-center gap-1.5">
                  {[10, 15, 20, 30, 45].map((mins) => (
                    <button
                      key={mins}
                      type="button"
                      onClick={() => setDurationMinutes(mins)}
                      className={cn(
                        'h-7 px-2.5 rounded-lg text-xs font-medium border transition-colors',
                        durationMinutes === mins
                          ? 'bg-brand text-white border-brand shadow-xs'
                          : 'bg-background hover:bg-muted text-muted-foreground'
                      )}
                    >
                      {mins} phút
                    </button>
                  ))}
                </div>
              </div>
            )}

            {slicingMode === 'BY_COUNT' && (
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <span className="text-xs font-semibold text-foreground">Số lượng kíp thi muốn chia:</span>
                  <p className="text-[11px] text-muted-foreground">
                    Mỗi kíp sẽ kéo dài ~{Math.round(totalDurationMinutes / customSlotCount)} phút.
                  </p>
                </div>
                <div className="flex items-center gap-1.5">
                  {[2, 3, 4, 5, 6, 8, 10].map((n) => (
                    <button
                      key={n}
                      type="button"
                      onClick={() => setCustomSlotCount(n)}
                      className={cn(
                        'h-7 px-2.5 rounded-lg text-xs font-medium border transition-colors',
                        customSlotCount === n
                          ? 'bg-brand text-white border-brand shadow-xs'
                          : 'bg-background hover:bg-muted text-muted-foreground'
                      )}
                    >
                      {n} kíp
                    </button>
                  ))}
                </div>
              </div>
            )}

            {/* Exam assignment mode in slot */}
            {hasMultipleExams && (
              <div className="pt-2 border-t flex flex-col gap-1.5">
                <span className="text-xs font-semibold text-foreground flex items-center gap-1.5">
                  <Shuffle className="size-3.5 text-brand" />
                  Quy tắc gán đề thi cho kíp:
                </span>
                <div className="grid grid-cols-2 gap-2 text-xs">
                  <label className="flex items-center gap-2 rounded-xl border bg-background p-2.5 cursor-pointer">
                    <input
                      type="radio"
                      name="examMode"
                      value="EXAM_PER_SLOT"
                      checked={examAssignmentMode === 'EXAM_PER_SLOT'}
                      onChange={() => setExamAssignmentMode('EXAM_PER_SLOT')}
                      className="text-brand accent-brand"
                    />
                    <div>
                      <span className="font-semibold block text-foreground">Mỗi kíp 1 đề cố định</span>
                      <span className="text-[11px] text-muted-foreground">Kíp 1 đề A, Kíp 2 đề B... chống lộ đề</span>
                    </div>
                  </label>

                  <label className="flex items-center gap-2 rounded-xl border bg-background p-2.5 cursor-pointer">
                    <input
                      type="radio"
                      name="examMode"
                      value="SHUFFLE_IN_SLOT"
                      checked={examAssignmentMode === 'SHUFFLE_IN_SLOT'}
                      onChange={() => setExamAssignmentMode('SHUFFLE_IN_SLOT')}
                      className="text-brand accent-brand"
                    />
                    <div>
                      <span className="font-semibold block text-foreground">Trộn đa đề trong từng kíp</span>
                      <span className="text-[11px] text-muted-foreground">Mỗi kíp chia đều các đề cho SV ngồi cạnh nhau</span>
                    </div>
                  </label>
                </div>
              </div>
            )}
          </div>

          {/* Live Timeline Preview */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-foreground flex items-center gap-1.5">
                <Clock className="size-3.5 text-brand" />
                Xem trước Lịch Kíp thi ({calculated.slotCount} kíp):
              </span>
              <span className="text-[11px] text-muted-foreground">
                ~{calculated.perSlotCap} sinh viên/kíp
              </span>
            </div>

            <div className="grid gap-2 sm:grid-cols-2 max-h-[220px] overflow-y-auto rounded-2xl border p-2 bg-muted/10">
              {calculated.slots.map((s) => (
                <div
                  key={s.slotIndex}
                  className="flex items-center justify-between rounded-xl border bg-card p-2.5 text-xs shadow-xs"
                >
                  <div className="min-w-0 pr-2">
                    <div className="flex items-center gap-1.5">
                      <span className="font-bold text-foreground">{s.slotName}</span>
                      <span className="font-mono text-brand font-semibold">
                        {s.startDisplay} – {s.endDisplay}
                      </span>
                    </div>
                    {s.assignedExamSummary ? (
                      <p className="text-[11px] text-muted-foreground truncate mt-0.5 font-mono">
                        Đề: {s.assignedExamSummary}
                      </p>
                    ) : (
                      <p className="text-[11px] text-muted-foreground mt-0.5">
                        {s.durationMinutes} phút · Dự kiến {s.maxCapacity} SV
                      </p>
                    )}
                  </div>
                  <span className="rounded-full bg-brand-soft px-2 py-0.5 text-[11px] font-semibold text-brand shrink-0">
                    Kíp {s.slotIndex}
                  </span>
                </div>
              ))}
            </div>
          </div>
        </div>

        <DialogFooter className="gap-2 sm:gap-0 pt-2">
          <Button type="button" variant="ghost" onClick={onClose} disabled={saving}>
            Hủy
          </Button>
          <Button type="button" onClick={handleApply} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : <Check className="size-4" />}
            {saving
              ? 'Đang xử lý…'
              : session.enrolledStudents > 0
              ? 'Áp dụng & Phân bổ ngay cho SV'
              : 'Lưu cấu hình kíp thi'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
