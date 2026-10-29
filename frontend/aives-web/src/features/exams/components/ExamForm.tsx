import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import {
  AlertCircle,
  Check,
  ChevronDown,
  ChevronRight,
  Clock,
  Layers,
  Loader2,
  Plus,
  RefreshCw,
  Sparkles,
  Trash2,
  Users,
} from 'lucide-react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { teacherQuestionsApi } from '@/features/teacher-questions/api/teacherQuestionsApi'
import type { QuestionItemDto } from '@/features/teacher-questions/types'
import { toApiError } from '@/lib/http/apiError'
import { cn } from '@/lib/utils'
import { examsApi } from '../api/examsApi'
import type {
  AutoPartitionPreviewResponse,
  CreateExamSlotTemplateInput,
  CreateSubPaperInput,
  Exam,
  UpdateExamInput,
} from '../types'

const inputClass = 'mt-1 block w-full rounded-xl border bg-background px-3 py-2 text-sm'

function getLevelBadge(level?: number | null) {
  if (level === 1) {
    return <span className="rounded-md bg-emerald-500/15 border border-emerald-500/20 px-2 py-0.5 text-[10px] font-semibold text-emerald-700 dark:text-emerald-400 shrink-0">Easy</span>
  }
  if (level === 2) {
    return <span className="rounded-md bg-amber-500/15 border border-amber-500/20 px-2 py-0.5 text-[10px] font-semibold text-amber-700 dark:text-amber-400 shrink-0">Medium</span>
  }
  return <span className="rounded-md bg-rose-500/15 border border-rose-500/20 px-2 py-0.5 text-[10px] font-semibold text-rose-700 dark:text-rose-400 shrink-0">Hard</span>
}

export function ExamForm({
  exam,
  onSubmit,
  onSaved,
  onCancel,
  onPending,
  submitLabel = 'Save exam',
}: {
  exam: Exam
  onSubmit: (input: UpdateExamInput) => Promise<Exam>
  onSaved: (exam: Exam) => void
  onCancel: () => void
  onPending: (pending: boolean) => void
  submitLabel?: string
}) {
  const [title, setTitle] = useState(exam.title)
  const [duration, setDuration] = useState(exam.durationMinutes)
  const [maxQuestions, setMaxQuestions] = useState(exam.maxQuestions)
  const [selected, setSelected] = useState(
    () => new Map(exam.questions.map((question) => [question.questionId, question.questionScore]))
  )
  const [questions, setQuestions] = useState<QuestionItemDto[]>([])
  const [loading, setLoading] = useState(true)
  const [loaded, setLoaded] = useState(false)
  const [pending, setPending] = useState(false)
  const [suggesting, setSuggesting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [rationale, setRationale] = useState<string | null>(null)

  // Hybrid Mode: 'AUTO_PARTITION' (Mode B) vs 'MANUAL' (Mode A)
  const [slotMode, setSlotMode] = useState<'AUTO_PARTITION' | 'MANUAL'>('AUTO_PARTITION')
  const [questionsPerSlot, setQuestionsPerSlot] = useState(10)
  const [partitioning, setPartitioning] = useState(false)
  const [partitionPreview, setPartitionPreview] = useState<AutoPartitionPreviewResponse | null>(null)
  const [expandedSlots, setExpandedSlots] = useState<Record<number, boolean>>({})

  // Exam Slot Templates state
  const [slotTemplates, setSlotTemplates] = useState<CreateExamSlotTemplateInput[]>(() => {
    return (
      exam.slotTemplates?.map((st) => ({
        slotIndex: st.slotIndex,
        slotName: st.slotName,
        durationMinutes: st.durationMinutes,
        maxCapacity: st.maxCapacity,
        examPaperIds: st.examPaperIds ?? [],
      })) ?? [
        { slotIndex: 1, slotName: 'Slot 1', durationMinutes: 30, maxCapacity: 20, examPaperIds: [] },
        { slotIndex: 2, slotName: 'Slot 2', durationMinutes: 30, maxCapacity: 20, examPaperIds: [] },
      ]
    )
  })

  // Sibling exams in the same course for manual multi-paper slot mapping
  const [siblingExams, setSiblingExams] = useState<Exam[]>([])

  useEffect(() => {
    let active = true
    teacherQuestionsApi
      .getAllQuestions(exam.courseId, 'APPROVED')
      .then((items) => {
        if (active) {
          setQuestions(items)
          setLoaded(true)
          setLoading(false)
        }
      })
      .catch((err) => {
        if (active) {
          setError(toApiError(err).message)
          setLoading(false)
        }
      })

    examsApi
      .listMine()
      .then((allExams) => {
        if (active) {
          setSiblingExams(
            allExams.filter((e) => e.courseId === exam.courseId && e.examId !== exam.examId)
          )
        }
      })
      .catch(() => {})

    return () => {
      active = false
    }
  }, [exam.courseId, exam.examId])

  function toggle(question: QuestionItemDto) {
    setSelected((previous) => {
      const next = new Map(previous)
      if (next.has(question.questionId)) next.delete(question.questionId)
      else next.set(question.questionId, question.maxScore ?? 1)
      return next
    })
    if (partitionPreview) {
      setPartitionPreview(null)
    }
  }

  function addSlotTemplate() {
    setSlotTemplates((prev) => [
      ...prev,
      {
        slotIndex: prev.length + 1,
        slotName: `Slot ${prev.length + 1}`,
        durationMinutes: duration || 30,
        maxCapacity: 20,
        examPaperIds: [],
      },
    ])
    if (partitionPreview) setPartitionPreview(null)
  }

  function removeSlotTemplate(index: number) {
    setSlotTemplates((prev) =>
      prev.filter((_, i) => i !== index).map((st, i) => ({ ...st, slotIndex: i + 1 }))
    )
    if (partitionPreview) setPartitionPreview(null)
  }

  function updateSlotTemplate<K extends keyof CreateExamSlotTemplateInput>(
    index: number,
    field: K,
    value: CreateExamSlotTemplateInput[K]
  ) {
    setSlotTemplates((prev) =>
      prev.map((st, i) => (i === index ? { ...st, [field]: value } : st))
    )
  }

  const slotCount = slotTemplates.length
  const poolTarget = slotCount * questionsPerSlot
  const poolSelected = selected.size
  const isPoolSufficient = poolSelected >= poolTarget

  async function handleAutoPartition() {
    if (slotCount === 0) {
      setError('Please add at least 1 time slot before auto-generating papers.')
      return
    }
    if (!isPoolSufficient) {
      setError(`Insufficient questions in pool: selected ${poolSelected} / required ${poolTarget} questions (${slotCount} slots × ${questionsPerSlot} questions).`)
      return
    }

    setPartitioning(true)
    setError(null)
    try {
      const response = await examsApi.previewSlotPartition({
        courseId: exam.courseId,
        parentExamTitle: title.trim(),
        parentExamCode: exam.examCode,
        slotCount,
        questionsPerSlot,
        questionIds: Array.from(selected.keys()),
        slotTemplates,
      })
      setPartitionPreview(response)
      const exp: Record<number, boolean> = {}
      response.slots.forEach((s) => {
        exp[s.slotIndex] = true
      })
      setExpandedSlots(exp)
    } catch (err) {
      setError(toApiError(err).message)
    } finally {
      setPartitioning(false)
    }
  }

  async function suggest() {
    if (suggesting || pending) return
    setSuggesting(true)
    onPending(true)
    setError(null)
    try {
      const targetCount = slotMode === 'AUTO_PARTITION' && slotCount > 0 ? Math.max(maxQuestions, poolTarget) : maxQuestions
      const result = await teacherQuestionsApi.suggestQuestions(exam.courseId, targetCount)
      setSelected(
        new Map(
          result.suggestedQuestions.map((question) => [
            question.questionId,
            selected.get(question.questionId) ?? question.maxScore ?? 1,
          ])
        )
      )
      setRationale(result.rationale)
      if (partitionPreview) setPartitionPreview(null)
    } catch (err) {
      setError(toApiError(err).message)
    } finally {
      setSuggesting(false)
      onPending(false)
    }
  }

  async function save(event: FormEvent) {
    event.preventDefault()
    if (pending || suggesting || !loaded) return

    if (!title.trim()) {
      setError('Enter an exam title.')
      return
    }

    if (slotMode === 'MANUAL') {
      if (selected.size > maxQuestions) {
        setError(`Selected questions (${selected.size}) exceeds Max Questions limit (${maxQuestions}).`)
        return
      }
    } else {
      // Mode B (Auto-Partition)
      if (slotCount > 0 && !isPoolSufficient) {
        setError(`Insufficient pool: selected ${poolSelected} / required ${poolTarget} questions for ${slotCount} slots × ${questionsPerSlot} questions. Please select ${poolTarget - poolSelected} more questions.`)
        return
      }
    }

    if (
      [...selected.values()].some(
        (score) => !Number.isFinite(score) || score < 0.01 || score > 100
      )
    ) {
      setError('Each question score must be between 0.01 and 100.')
      return
    }

    setPending(true)
    onPending(true)
    setError(null)

    try {
      let subPapersPayload: CreateSubPaperInput[] | undefined = undefined

      if (slotMode === 'AUTO_PARTITION' && partitionPreview && partitionPreview.slots.length > 0) {
        subPapersPayload = partitionPreview.slots.map((s) => ({
          slotIndex: s.slotIndex,
          title: s.subPaperTitle,
          examCode: s.subPaperCode,
          questions: s.questions.map((q) => ({
            questionId: q.questionId,
            questionScore: selected.get(q.questionId) ?? q.questionScore ?? 1,
          })),
        }))
      }

      const effectiveMaxQuestions = slotMode === 'AUTO_PARTITION' ? Math.max(maxQuestions, selected.size) : maxQuestions

      const saved = await onSubmit({
        title: title.trim(),
        durationMinutes: duration,
        maxQuestions: effectiveMaxQuestions,
        autoGenerateSlotPapers: slotMode === 'AUTO_PARTITION',
        questionsPerSlot: slotMode === 'AUTO_PARTITION' ? questionsPerSlot : undefined,
        generatedSubPapers: subPapersPayload,
        questions: [...selected].map(([questionId, questionScore]) => ({
          questionId,
          questionScore,
        })),
        slotTemplates:
          slotTemplates.length > 0
            ? slotTemplates.map((st, idx) => ({
                slotIndex: idx + 1,
                slotName: st.slotName.trim() || `Slot ${idx + 1}`,
                durationMinutes: Number(st.durationMinutes) || duration || 30,
                maxCapacity: Number(st.maxCapacity) || 20,
                examPaperIds: st.examPaperIds,
              }))
            : undefined,
      })
      onSaved(saved)
    } catch (err) {
      setError(toApiError(err).message)
    } finally {
      setPending(false)
      onPending(false)
    }
  }

  return (
    <form onSubmit={save} className="space-y-5">
      {error && (
        <Alert variant="destructive" className="py-2.5">
          <AlertCircle className="size-4" />
          <AlertDescription className="text-xs">{error}</AlertDescription>
        </Alert>
      )}

      <fieldset disabled={pending || suggesting} className="space-y-4">
        {/* Title */}
        <label className="block text-sm font-semibold">
          Exam Title
          <input
            required
            maxLength={255}
            placeholder="e.g. PRJ301 - Final Oral Defense Exam"
            className={inputClass}
            value={title}
            onChange={(event) => setTitle(event.target.value)}
          />
        </label>

        {/* Duration & Max Questions */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <label className="text-sm font-semibold">
            Default Slot Duration (minutes)
            <input
              required
              type="number"
              min={1}
              max={480}
              className={inputClass}
              value={duration}
              onChange={(event) => setDuration(Number(event.target.value))}
            />
          </label>
          <label className="text-sm font-semibold">
            {slotMode === 'AUTO_PARTITION' ? 'Target Pool Capacity' : 'Max Questions'}
            <input
              required
              type="number"
              min={1}
              max={200}
              className={inputClass}
              value={maxQuestions}
              onChange={(event) => setMaxQuestions(Number(event.target.value))}
            />
          </label>
        </div>

        {/* ========================================================================= */}
        {/* TIME SLOT TEMPLATES & HYBRID WORKFLOW MANAGER */}
        {/* ========================================================================= */}
        <div className="rounded-2xl border bg-muted/20 p-5 space-y-4">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b pb-3.5">
            <div className="flex items-center gap-2.5">
              <span className="flex size-8 items-center justify-center rounded-xl bg-brand-soft text-brand">
                <Layers className="size-4.5" />
              </span>
              <div>
                <h4 className="text-sm font-bold text-foreground">
                  Default Time Slot Templates
                </h4>
                <p className="text-xs text-muted-foreground mt-0.5">
                  Define time slots for this exam paper and configure how questions are assigned.
                </p>
              </div>
            </div>

            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={addSlotTemplate}
              className="h-8 text-xs gap-1.5 border-brand/50 text-brand hover:bg-brand-soft shadow-2xs self-start sm:self-auto"
            >
              <Plus className="size-3.5" />
              Add Time Slot
            </Button>
          </div>

          {/* Mode Switcher Tabs */}
          <div className="flex items-center p-1 rounded-xl bg-background border shadow-2xs">
            <button
              type="button"
              onClick={() => {
                setSlotMode('AUTO_PARTITION')
                setError(null)
              }}
              className={cn(
                'flex-1 flex items-center justify-center gap-2 py-2 px-3 rounded-lg text-xs font-semibold transition-all',
                slotMode === 'AUTO_PARTITION'
                  ? 'bg-brand text-white shadow-xs'
                  : 'text-muted-foreground hover:text-foreground'
              )}
            >
              <Sparkles className="size-3.5" />
              Mode B: Auto-Partition from Pool (Recommended)
            </button>
            <button
              type="button"
              onClick={() => {
                setSlotMode('MANUAL')
                setError(null)
              }}
              className={cn(
                'flex-1 flex items-center justify-center gap-2 py-2 px-3 rounded-lg text-xs font-semibold transition-all',
                slotMode === 'MANUAL'
                  ? 'bg-brand text-white shadow-xs'
                  : 'text-muted-foreground hover:text-foreground'
              )}
            >
              <Layers className="size-3.5" />
              Mode A: Manual Paper Assignment
            </button>
          </div>

          {/* Mode B: Clean Auto-Partition Control Box */}
          {slotMode === 'AUTO_PARTITION' && (
            <div className="rounded-xl border border-brand/25 bg-brand-soft/25 p-4 space-y-3.5">
              <div className="flex flex-col md:flex-row md:items-center justify-between gap-3">
                {/* Clean inline formula settings */}
                <div className="flex flex-wrap items-center gap-2.5">
                  <div className="flex items-center gap-2 rounded-xl bg-background border px-3 py-1.5 shadow-2xs">
                    <span className="text-xs font-semibold text-foreground">Questions per slot:</span>
                    <input
                      type="number"
                      min={1}
                      max={50}
                      value={questionsPerSlot}
                      onChange={(e) => {
                        setQuestionsPerSlot(Math.max(1, Number(e.target.value)))
                        if (partitionPreview) setPartitionPreview(null)
                      }}
                      className="h-7 w-14 rounded-lg border border-input bg-muted/20 px-2 text-center text-xs font-mono font-bold text-brand outline-none focus:border-brand"
                    />
                    <span className="text-xs text-muted-foreground">questions</span>
                  </div>

                  <div className="flex items-center gap-1.5 text-xs text-muted-foreground bg-background/80 rounded-xl px-3 py-1.5 border">
                    <span className="font-medium text-foreground">Formula:</span>
                    <span className="font-semibold text-foreground">{slotCount} slots</span>
                    <span>×</span>
                    <span className="font-semibold text-foreground">{questionsPerSlot} questions</span>
                    <span>=</span>
                    <span className="font-bold text-brand">{poolTarget} questions required</span>
                  </div>
                </div>

                {/* Auto-generate button */}
                <Button
                  type="button"
                  size="sm"
                  disabled={partitioning || !isPoolSufficient || slotCount === 0}
                  onClick={handleAutoPartition}
                  className="h-8.5 px-4 gap-2 text-xs shadow-xs shrink-0 self-start md:self-auto"
                >
                  {partitioning ? <Loader2 className="size-3.5 animate-spin" /> : <Sparkles className="size-3.5" />}
                  {partitioning ? 'Partitioning pool…' : '⚡ Auto-Generate Slot Papers'}
                </Button>
              </div>

              {/* Pool Size Status Banner */}
              {slotCount === 0 ? (
                <p className="text-xs text-muted-foreground italic">
                  Add at least 1 time slot below to enable question pool partitioning.
                </p>
              ) : !isPoolSufficient ? (
                <div className="flex items-center gap-2.5 rounded-xl bg-amber-500/10 border border-amber-500/30 px-3.5 py-2.5 text-xs text-amber-900 dark:text-amber-300">
                  <AlertCircle className="size-4 shrink-0 text-amber-600 dark:text-amber-400" />
                  <span>
                    Selected <strong>{poolSelected}</strong> / Required <strong>{poolTarget}</strong> questions ({slotCount} slots × {questionsPerSlot} questions).
                    Please select <strong>{poolTarget - poolSelected}</strong> more question(s) from the bank below.
                  </span>
                </div>
              ) : (
                <div className="flex items-center gap-2.5 rounded-xl bg-emerald-500/10 border border-emerald-500/30 px-3.5 py-2.5 text-xs text-emerald-900 dark:text-emerald-300">
                  <Check className="size-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
                  <span>
                    Selected <strong>{poolSelected}</strong> / Required <strong>{poolTarget}</strong> questions.
                    {poolSelected > poolTarget ? (
                      <> Surplus: <strong>{poolSelected - poolTarget}</strong> question(s) will be preserved in the reserve pool.</>
                    ) : (
                      ' Exact quota met. Zero surplus.'
                    )}
                  </span>
                </div>
              )}

              {/* Partition Preview Display */}
              {partitionPreview && (
                <div className="space-y-3 pt-3 border-t border-brand/20">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-bold text-brand flex items-center gap-1.5">
                      <Sparkles className="size-3.5" />
                      Generated Sub-Papers Preview ({partitionPreview.slots.length} papers):
                    </span>
                    <button
                      type="button"
                      onClick={handleAutoPartition}
                      className="text-[11px] text-brand hover:underline font-semibold flex items-center gap-1"
                    >
                      <RefreshCw className="size-3" /> Re-shuffle
                    </button>
                  </div>

                  <div className="grid gap-2.5 sm:grid-cols-2">
                    {partitionPreview.slots.map((s) => {
                      const isExpanded = expandedSlots[s.slotIndex] ?? false
                      return (
                        <div
                          key={s.slotIndex}
                          className="rounded-xl border bg-card p-3.5 text-xs space-y-2.5 shadow-2xs"
                        >
                          <div className="flex items-start justify-between gap-1">
                            <div>
                              <div className="font-semibold text-foreground flex items-center gap-1.5">
                                <span>{s.slotName}:</span>
                                <span className="font-mono text-brand">{s.subPaperCode}</span>
                              </div>
                              <p className="text-[11px] text-muted-foreground truncate max-w-[220px]">
                                {s.subPaperTitle}
                              </p>
                            </div>
                            <div className="flex items-center gap-1 shrink-0">
                              {s.easyCount > 0 && <span className="rounded bg-emerald-500/15 px-1.5 py-0.5 text-[10px] font-bold text-emerald-700 dark:text-emerald-400">{s.easyCount} Easy</span>}
                              {s.mediumCount > 0 && <span className="rounded bg-amber-500/15 px-1.5 py-0.5 text-[10px] font-bold text-amber-700 dark:text-amber-400">{s.mediumCount} Med</span>}
                              {s.hardCount > 0 && <span className="rounded bg-rose-500/15 px-1.5 py-0.5 text-[10px] font-bold text-rose-700 dark:text-rose-400">{s.hardCount} Hard</span>}
                            </div>
                          </div>

                          <div className="flex items-center justify-between text-[11px] text-muted-foreground pt-1.5 border-t">
                            <span className="font-medium">{s.questions.length} questions assigned</span>
                            <button
                              type="button"
                              onClick={() =>
                                setExpandedSlots((prev) => ({ ...prev, [s.slotIndex]: !isExpanded }))
                              }
                              className="text-brand hover:underline font-semibold flex items-center gap-1"
                            >
                              {isExpanded ? 'Hide questions' : 'View questions'}
                              {isExpanded ? <ChevronDown className="size-3" /> : <ChevronRight className="size-3" />}
                            </button>
                          </div>

                          {isExpanded && (
                            <ul className="space-y-1.5 pt-1.5 max-h-40 overflow-y-auto text-[11px]">
                              {s.questions.map((q, qIdx) => (
                                <li
                                  key={q.questionId}
                                  className="flex items-start justify-between gap-2 rounded-lg p-1.5 bg-muted/40 border border-border/40"
                                >
                                  <div className="flex items-start gap-1.5 min-w-0">
                                    <span className="font-mono text-muted-foreground font-bold shrink-0">
                                      Q{qIdx + 1}.
                                    </span>
                                    <span className="truncate text-foreground">{q.questionText}</span>
                                  </div>
                                  {getLevelBadge(q.level)}
                                </li>
                              ))}
                            </ul>
                          )}
                        </div>
                      )
                    })}
                  </div>

                  {/* Reserve questions indicator */}
                  {partitionPreview.reserveQuestions.length > 0 && (
                    <div className="rounded-xl border bg-muted/30 p-2.5 text-xs flex items-center justify-between">
                      <span className="text-muted-foreground font-medium">
                        Reserve Pool ({partitionPreview.reserveQuestions.length} surplus questions):
                      </span>
                      <span className="font-mono font-medium text-foreground">
                        {partitionPreview.reserveQuestions.map((q) => `#${q.questionId}`).join(', ')}
                      </span>
                    </div>
                  )}
                </div>
              )}
            </div>
          )}

          {/* ========================================================================= */}
          {/* POLISHED TIME SLOT TEMPLATES TABLE */}
          {/* ========================================================================= */}
          {slotTemplates.length === 0 ? (
            <div className="rounded-xl border border-dashed py-4 px-4 text-center text-xs text-muted-foreground">
              No default time slots configured yet. Click <strong>Add Time Slot</strong> to divide sessions of this exam into sequential slots (Slot 1, Slot 2, ...).
            </div>
          ) : (
            <div className="overflow-hidden rounded-xl border bg-background shadow-2xs">
              {/* Header columns */}
              <div className="grid grid-cols-[2.5rem_1fr_9.5rem_9.5rem_2.5rem] items-center gap-3 border-b bg-muted/40 px-4 py-2.5 text-[11px] font-semibold text-muted-foreground uppercase tracking-wider">
                <span>#</span>
                <span>Slot Name</span>
                <span className="flex items-center gap-1.5">
                  <Clock className="size-3 text-muted-foreground" />
                  Duration
                </span>
                <span className="flex items-center gap-1.5">
                  <Users className="size-3 text-muted-foreground" />
                  Max Capacity
                </span>
                <span className="text-right">Action</span>
              </div>

              {/* Table Rows */}
              <div className="divide-y">
                {slotTemplates.map((st, idx) => (
                  <div key={idx} className="transition-colors hover:bg-muted/10">
                    <div className="grid grid-cols-[2.5rem_1fr_9.5rem_9.5rem_2.5rem] items-center gap-3 px-4 py-3 text-xs">
                      <span className="font-mono font-bold text-muted-foreground text-xs">
                        #{idx + 1}
                      </span>
                      <input
                        type="text"
                        required
                        placeholder={`Slot ${idx + 1}`}
                        value={st.slotName}
                        onChange={(e) => updateSlotTemplate(idx, 'slotName', e.target.value)}
                        className="h-8 rounded-lg border border-input bg-background px-3 text-xs font-medium outline-none focus:border-brand"
                      />
                      <div className="flex items-center gap-1.5">
                        <input
                          type="number"
                          required
                          min={1}
                          max={480}
                          value={st.durationMinutes}
                          onChange={(e) => updateSlotTemplate(idx, 'durationMinutes', Number(e.target.value))}
                          className="h-8 w-16 rounded-lg border border-input bg-background px-2 text-center text-xs font-mono outline-none focus:border-brand"
                        />
                        <span className="text-muted-foreground text-[11px]">mins</span>
                      </div>
                      <div className="flex items-center gap-1.5">
                        <input
                          type="number"
                          required
                          min={1}
                          max={500}
                          value={st.maxCapacity}
                          onChange={(e) => updateSlotTemplate(idx, 'maxCapacity', Number(e.target.value))}
                          className="h-8 w-16 rounded-lg border border-input bg-background px-2 text-center text-xs font-mono outline-none focus:border-brand"
                        />
                        <span className="text-muted-foreground text-[11px]">students</span>
                      </div>
                      <div className="text-right">
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          onClick={() => removeSlotTemplate(idx)}
                          className="size-7 text-muted-foreground hover:text-destructive hover:bg-destructive/10"
                          title="Remove slot"
                        >
                          <Trash2 className="size-3.5" />
                        </Button>
                      </div>
                    </div>

                    {/* Mode A: Manual multi-paper selection row */}
                    {slotMode === 'MANUAL' && siblingExams.length > 0 && (
                      <div className="px-4 pb-2.5 pt-0.5 text-[11px] text-muted-foreground flex items-center gap-2.5">
                        <span className="font-medium text-foreground">Assigned Exam Paper:</span>
                        <select
                          aria-label="Assigned exam paper for slot"
                          value={st.examPaperIds?.[0] ?? ''}
                          onChange={(e) => {
                            const val = Number(e.target.value)
                            updateSlotTemplate(
                              idx,
                              'examPaperIds',
                              val > 0 ? [val] : []
                            )
                          }}
                          className="h-7 rounded-lg border border-input bg-background px-2.5 text-xs outline-none focus:border-brand"
                        >
                          <option value="">-- Default (All session papers / Primary) --</option>
                          {siblingExams.map((se) => (
                            <option key={se.examId} value={se.examId}>
                              {se.examCode} ({se.title})
                            </option>
                          ))}
                        </select>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* ========================================================================= */}
        {/* QUESTION BANK SELECTION POOL */}
        {/* ========================================================================= */}
        <div className="flex flex-wrap items-center justify-between gap-3 pt-3">
          <div className="space-y-0.5">
            <p className="text-sm font-semibold text-foreground">
              {slotMode === 'AUTO_PARTITION'
                ? `Master Question Pool (${selected.size} selected / ${poolTarget} target)`
                : `Selected ${selected.size}/${maxQuestions} questions`}
            </p>
            <p className="text-xs text-muted-foreground">
              {slotMode === 'AUTO_PARTITION'
                ? `Select at least ${poolTarget} questions to partition across ${slotCount} time slots.`
                : 'Select questions for this examination paper.'}
            </p>
          </div>

          <Button
            type="button"
            variant="outline"
            disabled={!loaded || !questions.length}
            onClick={() => void suggest()}
          >
            {suggesting ? 'Suggesting…' : 'Suggest questions'}
          </Button>
        </div>

        {rationale && <p className="text-xs text-muted-foreground">{rationale}</p>}

        <p className="text-xs text-muted-foreground">
          Only approved questions are listed.{' '}
          <Link
            to={`/staff/questions?courseId=${exam.courseId}`}
            className="font-medium text-brand hover:text-brand-hover"
          >
            Review pending questions
          </Link>
        </p>

        {loading ? (
          <p role="status">Loading question bank…</p>
        ) : !questions.length ? (
          <p className="rounded-xl border border-dashed p-4 text-sm">
            No approved questions for this course yet. Approve questions in the question bank first.
          </p>
        ) : (
          <div className="max-h-72 space-y-2 overflow-y-auto rounded-xl border p-3">
            {questions.map((question) => (
              <div key={question.questionId} className="space-y-2 rounded-xl border bg-card p-3 shadow-2xs">
                <div className="flex items-start justify-between gap-3">
                  <label className="flex items-start gap-2.5 text-sm cursor-pointer flex-1">
                    <input
                      type="checkbox"
                      className="mt-0.5 rounded border-gray-300 text-brand focus:ring-brand accent-brand size-4"
                      checked={selected.has(question.questionId)}
                      disabled={
                        slotMode === 'MANUAL' &&
                        !selected.has(question.questionId) &&
                        selected.size >= maxQuestions
                      }
                      onChange={() => toggle(question)}
                    />
                    <span className="leading-snug text-foreground">{question.questionText}</span>
                  </label>
                  {getLevelBadge(question.level)}
                </div>

                {selected.has(question.questionId) && (
                  <label className="block text-xs text-muted-foreground pt-1.5 border-t">
                    Score for #{question.questionId}
                    <input
                      required
                      type="number"
                      min="0.01"
                      max="100"
                      step="0.0001"
                      className={`${inputClass} max-w-32 font-mono`}
                      value={selected.get(question.questionId)}
                      onChange={(event) =>
                        setSelected((previous) =>
                          new Map(previous).set(
                            question.questionId,
                            Number(event.target.value)
                          )
                        )
                      }
                    />
                  </label>
                )}
              </div>
            ))}
          </div>
        )}
      </fieldset>

      <div className="flex justify-end gap-2 pt-3 border-t">
        <Button
          type="button"
          variant="outline"
          disabled={pending || suggesting}
          onClick={onCancel}
        >
          Cancel
        </Button>
        <Button
          disabled={
            pending ||
            suggesting ||
            !loaded ||
            (slotMode === 'MANUAL' && selected.size > maxQuestions) ||
            (slotMode === 'AUTO_PARTITION' && slotCount > 0 && selected.size < poolTarget)
          }
        >
          {pending ? 'Saving…' : submitLabel}
        </Button>
      </div>
    </form>
  )
}
