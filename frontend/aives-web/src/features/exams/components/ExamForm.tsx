import { useEffect, useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { teacherQuestionsApi } from '@/features/teacher-questions/api/teacherQuestionsApi'
import type { QuestionItemDto } from '@/features/teacher-questions/types'
import { toApiError } from '@/lib/http/apiError'
import type { Exam, UpdateExamInput } from '../types'

const inputClass = 'mt-1 block w-full rounded-xl border bg-background px-3 py-2 text-sm'

export function ExamForm({ exam, onSubmit, onSaved, onCancel, onPending, submitLabel = 'Save exam' }: { exam: Exam; onSubmit: (input: UpdateExamInput) => Promise<Exam>; onSaved: (exam: Exam) => void; onCancel: () => void; onPending: (pending: boolean) => void; submitLabel?: string }) {
  const [title, setTitle] = useState(exam.title)
  const [duration, setDuration] = useState(exam.durationMinutes)
  const [maxQuestions, setMaxQuestions] = useState(exam.maxQuestions)
  const [selected, setSelected] = useState(() => new Map(exam.questions.map((question) => [question.questionId, question.questionScore])))
  const [questions, setQuestions] = useState<QuestionItemDto[]>([])
  const [loading, setLoading] = useState(true)
  const [loaded, setLoaded] = useState(false)
  const [pending, setPending] = useState(false)
  const [suggesting, setSuggesting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [rationale, setRationale] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    teacherQuestionsApi.getAllQuestions(exam.courseId).then((items) => {
      if (active) { setQuestions(items); setLoaded(true); setLoading(false) }
    }).catch((err) => { if (active) { setError(toApiError(err).message); setLoading(false) } })
    return () => { active = false }
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
    setSuggesting(true); onPending(true); setError(null)
    try {
      const result = await teacherQuestionsApi.suggestQuestions(exam.courseId, maxQuestions)
      setSelected(new Map(result.suggestedQuestions.map((question) => [question.questionId, selected.get(question.questionId) ?? question.maxScore ?? 1])))
      setRationale(result.rationale)
    } catch (err) { setError(toApiError(err).message) }
    finally { setSuggesting(false); onPending(false) }
  }

  async function save(event: FormEvent) {
    event.preventDefault()
    if (pending || suggesting || !loaded) return
    if (!title.trim() || selected.size > maxQuestions) { setError('Enter an exam title and select no more questions than the limit.'); return }
    if ([...selected.values()].some((score) => !Number.isFinite(score) || score < 0.01 || score > 100)) {
      setError('Each question score must be between 0.01 and 100.'); return
    }
    setPending(true); onPending(true); setError(null)
    try {
      const saved = await onSubmit({
        title: title.trim(), durationMinutes: duration, maxQuestions,
        questions: [...selected].map(([questionId, questionScore]) => ({ questionId, questionScore })),
      })
      onSaved(saved)
    } catch (err) { setError(toApiError(err).message) }
    finally { setPending(false); onPending(false) }
  }

  return <form onSubmit={save} className="space-y-4">
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    <fieldset disabled={pending || suggesting} className="space-y-4">
      <label className="block text-sm">Exam title<input required maxLength={255} className={inputClass} value={title} onChange={(event) => setTitle(event.target.value)} /></label>
      <div className="grid grid-cols-2 gap-4">
        <label className="text-sm">Duration (minutes)<input required type="number" min={1} max={480} className={inputClass} value={duration} onChange={(event) => setDuration(Number(event.target.value))} /></label>
        <label className="text-sm">Max questions<input required type="number" min={1} max={200} className={inputClass} value={maxQuestions} onChange={(event) => setMaxQuestions(Number(event.target.value))} /></label>
      </div>
      <div className="flex flex-wrap items-center justify-between gap-3"><p className="text-sm">Selected {selected.size}/{maxQuestions} questions</p><Button type="button" variant="outline" disabled={!loaded || !questions.length || maxQuestions < 1 || maxQuestions > 200} onClick={() => void suggest()}>{suggesting ? 'Suggesting…' : 'Suggest questions'}</Button></div>
      {rationale && <p className="text-xs text-muted-foreground">{rationale}</p>}
      {loading ? <p role="status">Loading question bank…</p> : !questions.length ? <p className="text-sm">The question bank is empty.</p> : <div className="max-h-72 space-y-2 overflow-y-auto rounded-xl border p-3">
        {questions.map((question) => <div key={question.questionId} className="space-y-2 rounded-xl border p-3">
          <label className="flex items-start gap-2 text-sm"><input type="checkbox" className="mt-1" checked={selected.has(question.questionId)} disabled={!selected.has(question.questionId) && selected.size >= maxQuestions} onChange={() => toggle(question)} /><span>{question.questionText}</span></label>
          {selected.has(question.questionId) && <label className="block text-xs">Score for #{question.questionId}<input required type="number" min="0.01" max="100" step="0.0001" className={`${inputClass} max-w-32`} value={selected.get(question.questionId)} onChange={(event) => setSelected((previous) => new Map(previous).set(question.questionId, Number(event.target.value)))} /></label>}
        </div>)}
      </div>}
    </fieldset>
    <div className="flex justify-end gap-2"><Button type="button" variant="outline" disabled={pending || suggesting} onClick={onCancel}>Cancel</Button><Button disabled={pending || suggesting || !loaded || selected.size > maxQuestions}>{pending ? 'Saving…' : submitLabel}</Button></div>
  </form>
}
