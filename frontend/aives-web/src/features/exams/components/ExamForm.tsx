import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { AlertCircle, Loader2, Sparkles } from 'lucide-react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { teacherQuestionsApi } from '@/features/teacher-questions/api/teacherQuestionsApi'
import type { QuestionItemDto } from '@/features/teacher-questions/types'
import { toApiError } from '@/lib/http/apiError'
import type { Exam, UpdateExamInput } from '../types'

const inputClass = 'mt-1 block w-full rounded-xl border bg-background px-3 py-2 text-sm'

function getLevelBadge(level?: number | null) {
  if (level === 1) {
    return (
      <span className="rounded-md bg-emerald-500/15 border border-emerald-500/20 px-2 py-0.5 text-[10px] font-semibold text-emerald-700 dark:text-emerald-400 shrink-0">
        Easy
      </span>
    )
  }
  if (level === 2) {
    return (
      <span className="rounded-md bg-amber-500/15 border border-amber-500/20 px-2 py-0.5 text-[10px] font-semibold text-amber-700 dark:text-amber-400 shrink-0">
        Medium
      </span>
    )
  }
  return (
    <span className="rounded-md bg-rose-500/15 border border-rose-500/20 px-2 py-0.5 text-[10px] font-semibold text-rose-700 dark:text-rose-400 shrink-0">
      Hard
    </span>
  )
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

    return () => {
      active = false
    }
  }, [exam.courseId])

  function toggle(question: QuestionItemDto) {
    setSelected((previous) => {
      const next = new Map(previous)
      if (next.has(question.questionId)) next.delete(question.questionId)
      else next.set(question.questionId, question.maxScore ?? 1)
      return next
    })
  }

  async function suggest() {
    if (suggesting || pending) return
    setSuggesting(true)
    onPending(true)
    setError(null)
    try {
      const result = await teacherQuestionsApi.suggestQuestions(exam.courseId, maxQuestions)
      setSelected(
        new Map(
          result.suggestedQuestions.map((question) => [
            question.questionId,
            selected.get(question.questionId) ?? question.maxScore ?? 1,
          ])
        )
      )
      setRationale(result.rationale)
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

    if (selected.size > maxQuestions) {
      setError(`Selected ${selected.size} questions, exceeding Max Questions (${maxQuestions}).`)
      return
    }

    setPending(true)
    onPending(true)
    setError(null)

    try {
      const payload: UpdateExamInput = {
        title: title.trim(),
        durationMinutes: Number(duration),
        maxQuestions: Number(maxQuestions),
        questions: Array.from(selected.entries()).map(([questionId, questionScore]) => ({
          questionId,
          questionScore,
        })),
      }

      const updated = await onSubmit(payload)
      onSaved(updated)
    } catch (err) {
      setError(toApiError(err).message)
    } finally {
      setPending(false)
      onPending(false)
    }
  }

  return (
    <form onSubmit={save} className="space-y-4">
      {error && (
        <Alert variant="destructive">
          <AlertCircle className="size-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <div>
        <label className="block text-sm font-medium">Course</label>
        <input
          disabled
          type="text"
          className={`${inputClass} bg-muted text-muted-foreground`}
          value={exam.courseName ?? `#${exam.courseId}`}
        />
      </div>

      <div>
        <label className="block text-sm font-medium">Exam title</label>
        <input
          required
          maxLength={100}
          type="text"
          className={inputClass}
          value={title}
          placeholder="e.g. Midterm Oral Examination"
          onChange={(event) => setTitle(event.target.value)}
        />
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <label className="block text-sm font-medium">Standard Duration (minutes)</label>
          <input
            required
            type="number"
            min={1}
            max={480}
            className={inputClass}
            value={duration}
            onChange={(event) => setDuration(Number(event.target.value))}
          />
          <p className="text-xs text-muted-foreground mt-1">
            Standard examination duration for each student.
          </p>
        </div>

        <div>
          <label className="block text-sm font-medium">Max Questions</label>
          <input
            required
            type="number"
            min={1}
            max={200}
            className={inputClass}
            value={maxQuestions}
            onChange={(event) => setMaxQuestions(Number(event.target.value))}
          />
          <p className="text-xs text-muted-foreground mt-1">
            Total number of questions evaluated in this exam paper.
          </p>
        </div>
      </div>

      {/* Question Selection Section */}
      <fieldset className="space-y-3 rounded-2xl border p-4 bg-muted/5">
        <legend className="px-1 text-sm font-semibold text-foreground">
          Questions ({selected.size} / {maxQuestions} selected)
        </legend>

        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="text-xs text-muted-foreground">
            Select questions from the approved question bank and specify point weights.
          </p>

          <Button
            type="button"
            variant="outline"
            size="sm"
            className="gap-1.5"
            disabled={!loaded || !questions.length || maxQuestions < 1 || maxQuestions > 200 || suggesting}
            onClick={() => void suggest()}
          >
            {suggesting ? (
              <Loader2 className="size-3.5 animate-spin" />
            ) : (
              <Sparkles className="size-3.5 text-brand" />
            )}
            {suggesting ? 'Suggesting…' : 'Suggest questions'}
          </Button>
        </div>

        {rationale && (
          <p className="rounded-lg bg-brand-soft p-2.5 text-xs text-brand">{rationale}</p>
        )}

        <p className="text-xs text-muted-foreground">
          Only approved questions are listed.{' '}
          <Link
            to={`/staff/questions?courseId=${exam.courseId}`}
            className="font-medium text-brand hover:underline"
          >
            Review pending questions
          </Link>
        </p>

        {loading ? (
          <div className="flex items-center gap-2 py-6 justify-center text-sm text-muted-foreground">
            <Loader2 className="size-4 animate-spin" />
            Loading question bank…
          </div>
        ) : !questions.length ? (
          <p className="rounded-xl border border-dashed p-4 text-center text-sm text-muted-foreground">
            No approved questions for this course yet. Approve questions in the question bank first.
          </p>
        ) : (
          <div className="max-h-80 space-y-2 overflow-y-auto rounded-xl border p-2 bg-card">
            {questions.map((question) => (
              <div
                key={question.questionId}
                className="space-y-2 rounded-xl border bg-card p-3 shadow-2xs hover:border-border transition-colors"
              >
                <div className="flex items-start justify-between gap-3">
                  <label className="flex items-start gap-2.5 text-sm cursor-pointer flex-1">
                    <input
                      type="checkbox"
                      className="mt-0.5 rounded border-gray-300 text-brand focus:ring-brand accent-brand size-4"
                      checked={selected.has(question.questionId)}
                      disabled={!selected.has(question.questionId) && selected.size >= maxQuestions}
                      onChange={() => toggle(question)}
                    />
                    <span className="leading-snug text-foreground">{question.questionText}</span>
                  </label>
                  {getLevelBadge(question.level)}
                </div>

                {selected.has(question.questionId) && (
                  <div className="flex items-center gap-3 pt-2 border-t text-xs text-muted-foreground">
                    <span>Score weight for #{question.questionId}:</span>
                    <input
                      required
                      type="number"
                      min="0.01"
                      max="100"
                      step="0.0001"
                      className={`${inputClass} max-w-28 font-mono !mt-0 py-1`}
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
                    <span>points</span>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </fieldset>

      <div className="flex justify-end gap-2 pt-2 border-t">
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
            selected.size > maxQuestions ||
            selected.size === 0
          }
        >
          {pending ? 'Saving…' : submitLabel}
        </Button>
      </div>
    </form>
  )
}
