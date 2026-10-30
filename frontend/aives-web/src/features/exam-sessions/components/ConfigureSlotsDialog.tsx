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
import { parseApiDate } from '@/lib/format/date'
import { toApiError } from '@/lib/http/apiError'
import { cn } from '@/lib/utils'
import { sessionsApi } from '../api/sessionsApi'
import type { CreateSessionSlotInput, ExamSession } from '../types'

export type SlicingMode = 'BY_COUNT' | 'BY_DURATION' | 'BY_STUDENTS'

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
  const [slicingMode, setSlicingMode] = useState<SlicingMode>('BY_COUNT')
  const [customSlotCount, setCustomSlotCount] = useState<number>(
    session.slots && session.slots.length > 0 ? session.slots.length : 2
  )
  const [durationMinutes, setDurationMinutes] = useState<number>(30)
  const [studentsPerSlot, setStudentsPerSlot] = useState<number>(15)
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

  // Calculate slots dynamically
  const calculated = (() => {
    let finalSlotCount = 1

    if (slicingMode === 'BY_COUNT') {
      finalSlotCount = Math.max(1, Math.min(20, customSlotCount))
    } else if (slicingMode === 'BY_DURATION') {
      const perSlotMin = Math.max(1, durationMinutes)
      finalSlotCount = Math.max(1, Math.min(20, Math.floor(totalDurationMinutes / perSlotMin)))
    } else {
      const perSlot = Math.max(1, studentsPerSlot)
      finalSlotCount = Math.max(1, Math.min(20, Math.ceil(studentCount / perSlot)))
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
      const slotIndex = i + 1

      let allowedExamIds: number[] = []
      let assignedExamSummary: string | undefined

      if (hasMultipleExams && examAssignmentMode === 'EXAM_PER_SLOT') {
        const assignedExam = exams[i % exams.length]
        allowedExamIds = [assignedExam.examId]
        assignedExamSummary = `${assignedExam.examCode} - ${assignedExam.title}`
      } else if (exams.length > 0) {
        allowedExamIds = exams.map((e) => e.examId)
        assignedExamSummary = hasMultipleExams ? 'All session exams (Shuffled)' : `${exams[0].examCode} - ${exams[0].title}`
      }

      slots.push({
        slotIndex,
        slotName: `Slot ${slotIndex}`,
        startTime: sStart.toISOString(),
        endTime: sEnd.toISOString(),
        maxCapacity: perSlotCap,
        durationMinutes: slotDuration,
        startDisplay: sStart.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        endDisplay: sEnd.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        allowedExamIds,
        assignedExamSummary,
      })
    }

    return {
      slotCount: finalSlotCount,
      slots,
      perSlotCap,
    }
  })()

  async function handleApply(distributeStudentsAfter = false) {
    setSaving(true)
    setError(null)
    try {
      const slotsPayload: CreateSessionSlotInput[] = calculated.slots.map((s) => ({
        slotIndex: s.slotIndex,
        slotName: s.slotName,
        startTime: s.startTime,
        endTime: s.endTime,
        maxCapacity: s.maxCapacity,
        allowedExamIds: s.allowedExamIds,
      }))

      await sessionsApi.update(session.sessionId, {
        slots: slotsPayload,
      })

      if (distributeStudentsAfter && session.enrolledStudents > 0) {
        await sessionsApi.distributeSlots(session.sessionId, {
          strategy: 'SLOT_BALANCED',
          examAssignmentMode,
        })
      }

      const refreshed = await sessionsApi.get(session.sessionId)
      onSaved(refreshed)
      onClose()
    } catch (err) {
      setError(toApiError(err, 'Could not save slot configuration.').message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={(val) => !val && !saving && onClose()}>
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2 text-xl font-bold">
            <Layers className="size-5 text-brand" />
            Configure Time Slots
          </DialogTitle>
          <DialogDescription>
            Divide this exam session ({totalDurationMinutes} mins total) into sequential time slots and assign exam papers to each slot.
          </DialogDescription>
        </DialogHeader>

        {error && (
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        <div className="space-y-4 pt-1">
          {/* Slicing Mode Tabs */}
          <div className="space-y-2">
            <label id={modeId} className="block text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Slicing Strategy
            </label>
            <div className="grid grid-cols-3 gap-2">
              <button
                type="button"
                className={cn(
                  'flex flex-col items-center justify-center gap-1 rounded-xl border p-2.5 text-center transition-colors',
                  slicingMode === 'BY_COUNT'
                    ? 'border-brand bg-brand-soft text-brand font-semibold shadow-xs'
                    : 'border-border bg-card text-muted-foreground hover:bg-muted/50'
                )}
                onClick={() => setSlicingMode('BY_COUNT')}
              >
                <Layers className="size-4" />
                <span className="text-xs">By Slot Count</span>
              </button>

              <button
                type="button"
                className={cn(
                  'flex flex-col items-center justify-center gap-1 rounded-xl border p-2.5 text-center transition-colors',
                  slicingMode === 'BY_DURATION'
                    ? 'border-brand bg-brand-soft text-brand font-semibold shadow-xs'
                    : 'border-border bg-card text-muted-foreground hover:bg-muted/50'
                )}
                onClick={() => setSlicingMode('BY_DURATION')}
              >
                <Clock className="size-4" />
                <span className="text-xs">By Duration</span>
              </button>

              <button
                type="button"
                className={cn(
                  'flex flex-col items-center justify-center gap-1 rounded-xl border p-2.5 text-center transition-colors',
                  slicingMode === 'BY_STUDENTS'
                    ? 'border-brand bg-brand-soft text-brand font-semibold shadow-xs'
                    : 'border-border bg-card text-muted-foreground hover:bg-muted/50'
                )}
                onClick={() => setSlicingMode('BY_STUDENTS')}
              >
                <Users className="size-4" />
                <span className="text-xs">By Students</span>
              </button>
            </div>
          </div>

          {/* Slicing Parameters Input */}
          <div className="rounded-xl border bg-card p-3.5 shadow-2xs space-y-3">
            {slicingMode === 'BY_COUNT' && (
              <div>
                <label className="block text-xs font-medium text-foreground">
                  Number of time slots ({customSlotCount} slots)
                </label>
                <div className="flex items-center gap-3 mt-1.5">
                  <input
                    type="range"
                    min={1}
                    max={12}
                    value={customSlotCount}
                    onChange={(e) => setCustomSlotCount(Number(e.target.value))}
                    className="flex-1 accent-brand"
                  />
                  <input
                    type="number"
                    min={1}
                    max={20}
                    value={customSlotCount}
                    onChange={(e) => setCustomSlotCount(Number(e.target.value))}
                    className="w-16 rounded-lg border bg-background px-2.5 py-1 text-sm font-mono text-center"
                  />
                </div>
                <p className="text-[11px] text-muted-foreground mt-1">
                  Each slot will run for approximately {Math.round(totalDurationMinutes / Math.max(1, customSlotCount))} minutes.
                </p>
              </div>
            )}

            {slicingMode === 'BY_DURATION' && (
              <div>
                <label className="block text-xs font-medium text-foreground">
                  Duration per slot (minutes)
                </label>
                <div className="flex items-center gap-3 mt-1.5">
                  <input
                    type="number"
                    min={5}
                    max={totalDurationMinutes}
                    step={5}
                    value={durationMinutes}
                    onChange={(e) => setDurationMinutes(Number(e.target.value))}
                    className="w-24 rounded-lg border bg-background px-2.5 py-1 text-sm font-mono"
                  />
                  <span className="text-xs text-muted-foreground">
                    Yields {calculated.slotCount} sequential slot(s) for the {totalDurationMinutes}-minute session.
                  </span>
                </div>
              </div>
            )}

            {slicingMode === 'BY_STUDENTS' && (
              <div>
                <label className="block text-xs font-medium text-foreground">
                  Target students per slot
                </label>
                <div className="flex items-center gap-3 mt-1.5">
                  <input
                    type="number"
                    min={1}
                    max={100}
                    value={studentsPerSlot}
                    onChange={(e) => setStudentsPerSlot(Number(e.target.value))}
                    className="w-24 rounded-lg border bg-background px-2.5 py-1 text-sm font-mono"
                  />
                  <span className="text-xs text-muted-foreground">
                    Yields {calculated.slotCount} slot(s) for ~{studentCount} student(s).
                  </span>
                </div>
              </div>
            )}

            {/* Exam paper distribution rule */}
            {hasMultipleExams && (
              <div className="pt-2 border-t space-y-2">
                <span className="text-xs font-medium text-foreground flex items-center gap-1.5">
                  <Shuffle className="size-3.5 text-brand" />
                  Multiple Exam Papers Assignment:
                </span>
                <div className="grid gap-2 sm:grid-cols-2 text-xs">
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
                      <span className="font-semibold block text-foreground">One paper per slot</span>
                      <span className="text-[11px] text-muted-foreground">Slot 1 gets Paper A, Slot 2 gets Paper B...</span>
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
                      <span className="font-semibold block text-foreground">Shuffle papers in slot</span>
                      <span className="text-[11px] text-muted-foreground">Distribute papers among students in each slot</span>
                    </div>
                  </label>
                </div>
              </div>
            )}
          </div>

          {/* Timeline Live Preview */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-foreground flex items-center gap-1.5">
                <Clock className="size-3.5 text-brand" />
                Time Slots Schedule ({calculated.slotCount} slots):
              </span>
              <span className="text-[11px] text-muted-foreground">
                ~{calculated.perSlotCap} students/slot
              </span>
            </div>

            <div className="grid gap-2 sm:grid-cols-2 max-h-[260px] overflow-y-auto rounded-2xl border p-2 bg-muted/10">
              {calculated.slots.map((s) => (
                <div
                  key={s.slotIndex}
                  className="flex flex-col gap-1.5 rounded-xl border bg-card p-3 text-xs shadow-xs"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-1.5">
                      <span className="font-bold text-foreground">{s.slotName}</span>
                      <span className="font-mono text-brand font-semibold">
                        {s.startDisplay} – {s.endDisplay}
                      </span>
                    </div>
                    <span className="rounded-full bg-brand-soft px-2 py-0.5 text-[11px] font-semibold text-brand shrink-0">
                      #{s.slotIndex}
                    </span>
                  </div>

                  <div className="flex items-center justify-between text-[11px] text-muted-foreground">
                    <span>Duration: {s.durationMinutes} mins</span>
                    <span>Cap: ~{s.maxCapacity} students</span>
                  </div>

                  {s.assignedExamSummary && (
                    <div className="pt-1 border-t text-[11px] flex items-center gap-1">
                      <span className="text-muted-foreground">Paper:</span>
                      <span className="font-medium text-foreground truncate">{s.assignedExamSummary}</span>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>
        </div>

        <DialogFooter className="gap-2 sm:gap-0 pt-2">
          <Button type="button" variant="ghost" onClick={onClose} disabled={saving}>
            Cancel
          </Button>

          {session.enrolledStudents > 0 ? (
            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => void handleApply(false)}
                disabled={saving}
              >
                {saving ? <Loader2 className="size-4 animate-spin" /> : 'Save Slots Only'}
              </Button>
              <Button
                type="button"
                onClick={() => void handleApply(true)}
                disabled={saving}
                className="gap-1.5"
              >
                {saving ? <Loader2 className="size-4 animate-spin" /> : <Sparkles className="size-4" />}
                Save & Distribute to Students
              </Button>
            </div>
          ) : (
            <Button
              type="button"
              onClick={() => void handleApply(false)}
              disabled={saving}
              className="gap-1.5"
            >
              {saving ? <Loader2 className="size-4 animate-spin" /> : <Check className="size-4" />}
              Save Slot Configuration
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
