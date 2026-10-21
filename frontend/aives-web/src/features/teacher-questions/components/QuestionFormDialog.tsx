import { useEffect, useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { toApiError } from '@/lib/http/apiError'
import { cn } from '@/lib/utils'
import { teacherQuestionsApi } from '../api/teacherQuestionsApi'
import { APPROVAL_LABEL, APPROVAL_TONE } from '../lib/approvalStatus'
import type { QuestionItemDto, WriteQuestionInput } from '../types'

const fieldClass = 'mt-1 w-full rounded-xl border bg-background px-3 py-2 text-sm'
const emptyQuestion: WriteQuestionInput = {
  questionText: '', topic: '', sampleAnswer: '', learningOutcome: '', level: 1,
  maxScore: 1, isFollowUpAllowed: true, rubric: [{ description: '', maxScore: 1 }],
}

interface Props {
  courseId: number
  questionId?: number
  readOnly: boolean
  onClose: () => void
  onSaved: () => void
}

export function QuestionFormDialog({ courseId, questionId, readOnly, onClose, onSaved }: Props) {
  const [form, setForm] = useState<WriteQuestionInput>(emptyQuestion)
  const [loadFailed, setLoadFailed] = useState(false)
  const [loading, setLoading] = useState(questionId !== undefined)
  const [pending, setPending] = useState(false)
  const [inUse, setInUse] = useState(false)
  const [review, setReview] = useState<Pick<QuestionItemDto, 'approvalStatus' | 'rejectionReason'> | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (questionId === undefined) return
    let active = true
    teacherQuestionsApi.get(courseId, questionId).then((question) => {
      if (!active) return
      setForm({
        questionText: question.questionText, topic: question.topic ?? '',
        sampleAnswer: question.sampleAnswer ?? '', learningOutcome: question.learningOutcome ?? '',
        level: question.level, maxScore: question.maxScore ?? 1,
        isFollowUpAllowed: question.isFollowUpAllowed ?? true,
        rubric: question.rubric?.length ? question.rubric.map((criterion) => ({ ...criterion })) : [{ description: '', maxScore: question.maxScore ?? 1 }],
      })
      setInUse(Boolean(question.isInUse))
      setReview({ approvalStatus: question.approvalStatus, rejectionReason: question.rejectionReason })
      setLoading(false)
    }).catch((err) => {
      if (active) { setError(toApiError(err).message); setLoadFailed(true); setLoading(false) }
    })
    return () => { active = false }
  }, [courseId, questionId])

  const locked = readOnly || inUse || loadFailed
  const total = form.rubric.reduce((sum, criterion) => sum + Math.round(criterion.maxScore * 10000), 0) / 10000

  function set<K extends keyof WriteQuestionInput>(key: K, value: WriteQuestionInput[K]) {
    setForm((previous) => ({ ...previous, [key]: value }))
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (locked || loading || pending) return
    const normalized = form.rubric.map((criterion) => criterion.description.trim().toLocaleLowerCase().replace(/[^\p{L}\p{N}]+/gu, ' ').trim())
    if (!form.questionText.trim() || !form.sampleAnswer.trim() || !form.learningOutcome.trim()) {
      setError('Enter the question, model answer and learning outcome.'); return
    }
    if (form.rubric.some((criterion) => !criterion.description.trim() || !Number.isFinite(criterion.maxScore) || criterion.maxScore <= 0)
      || new Set(normalized).size !== normalized.length || !Number.isFinite(form.maxScore)
      || Math.abs(total - form.maxScore) > 0.000001) {
      setError('Criteria must be distinct, have positive points, and add up to the max score.'); return
    }
    setPending(true)
    setError(null)
    try {
      const input = { ...form, questionText: form.questionText.trim(), topic: form.topic.trim(), sampleAnswer: form.sampleAnswer.trim(), learningOutcome: form.learningOutcome.trim() }
      if (questionId === undefined) await teacherQuestionsApi.create(courseId, input)
      else await teacherQuestionsApi.update(courseId, questionId, input)
      onSaved()
    } catch (err) { setError(toApiError(err).message) }
    finally { setPending(false) }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && !pending && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{readOnly ? 'Question details' : questionId === undefined ? 'Add question' : 'Edit question'}</DialogTitle>
          <DialogDescription>{inUse ? 'This question is already used in an exam. Create a new question to change its content.' : 'The model answer, learning outcome and rubric are used for scoring.'}</DialogDescription>
        </DialogHeader>
        {loading ? <p role="status">Loading question…</p> : (
          <form onSubmit={submit} className="space-y-4">
            {review && (
              <div className="space-y-2">
                <span className={cn('inline-flex rounded-full px-2.5 py-1 text-xs font-medium', APPROVAL_TONE[review.approvalStatus])}>
                  {APPROVAL_LABEL[review.approvalStatus]}
                </span>
                {review.approvalStatus === 'REJECTED' && review.rejectionReason && (
                  <p className="rounded-xl bg-[#fef1f0] px-3.5 py-2.5 text-sm text-destructive">
                    <span className="font-medium">Rejection reason: </span>{review.rejectionReason}
                  </p>
                )}
                {/* The backend resets any edited question to PENDING_REVIEW; say so before the user saves. */}
                {!locked && review.approvalStatus !== 'PENDING_REVIEW' && (
                  <p role="note" className="rounded-xl bg-warning-soft px-3.5 py-2.5 text-sm text-warning">
                    Saving changes sends this question back to <span className="font-medium">Pending review</span>.
                    {review.approvalStatus === 'APPROVED' && ' It cannot be added to new exams until it is approved again.'}
                  </p>
                )}
              </div>
            )}
            {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
            <fieldset disabled={locked || pending} className="space-y-4">
              <label className="block text-sm">Question<textarea aria-label="Question" required maxLength={10000} rows={3} className={fieldClass} value={form.questionText} onChange={(event) => set('questionText', event.target.value)} /></label>
              <label className="block text-sm">Topic<input maxLength={1000} className={fieldClass} value={form.topic} onChange={(event) => set('topic', event.target.value)} /></label>
              <label className="block text-sm">Model answer<textarea aria-label="Model answer" required maxLength={10000} rows={3} className={fieldClass} value={form.sampleAnswer} onChange={(event) => set('sampleAnswer', event.target.value)} /></label>
              <label className="block text-sm">Learning outcome<textarea aria-label="Learning outcome" required maxLength={2000} rows={2} className={fieldClass} value={form.learningOutcome} onChange={(event) => set('learningOutcome', event.target.value)} /></label>
              <div className="grid grid-cols-2 gap-4">
                <label className="text-sm">Difficulty<select className={fieldClass} value={form.level} onChange={(event) => set('level', Number(event.target.value))}><option value={1}>Basic</option><option value={2}>Intermediate</option><option value={3}>Advanced</option></select></label>
                <label className="text-sm">Max score<input required type="number" min="0.01" max="100" step="0.0001" className={fieldClass} value={form.maxScore} onChange={(event) => set('maxScore', Number(event.target.value))} /></label>
              </div>
              <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isFollowUpAllowed} onChange={(event) => set('isFollowUpAllowed', event.target.checked)} />Allow follow-up questions</label>
              <div className="space-y-3 rounded-xl border p-3">
                <p className="text-sm font-medium">Rubric · Total: {total}</p>
                {form.rubric.map((criterion, index) => (
                  <div key={index} className="grid gap-2 sm:grid-cols-[1fr_6rem_auto]">
                    <label className="text-xs">Criterion {index + 1}<textarea aria-label={`Criterion ${index + 1}`} required maxLength={2000} className={fieldClass} value={criterion.description} onChange={(event) => set('rubric', form.rubric.map((item, position) => position === index ? { ...item, description: event.target.value } : item))} /></label>
                    <label className="text-xs">Points<input required type="number" min="0.0001" max="100" step="0.0001" className={fieldClass} value={criterion.maxScore} onChange={(event) => set('rubric', form.rubric.map((item, position) => position === index ? { ...item, maxScore: Number(event.target.value) } : item))} /></label>
                    {!locked && <Button type="button" variant="ghost" disabled={form.rubric.length === 1} onClick={() => set('rubric', form.rubric.filter((_, position) => position !== index))} aria-label={`Remove criterion ${index + 1}`}>Remove</Button>}
                  </div>
                ))}
                {!locked && <Button type="button" variant="outline" disabled={form.rubric.length >= 20} onClick={() => set('rubric', [...form.rubric, { description: '', maxScore: 1 }])}>Add criterion</Button>}
              </div>
            </fieldset>
            <DialogFooter>
              <Button type="button" variant="outline" disabled={pending} onClick={onClose}>Close</Button>
              {!locked && <Button type="submit" disabled={pending}>{pending ? 'Saving…' : 'Save question'}</Button>}
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}
