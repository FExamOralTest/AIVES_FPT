import { useId, useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { toApiError } from '@/lib/http/apiError'
import { teacherQuestionsApi } from '../api/teacherQuestionsApi'
import { REJECTION_REASON_MAX } from '../lib/approvalStatus'
import type { QuestionItemDto } from '../types'

interface RejectQuestionDialogProps {
  courseId: number
  question: QuestionItemDto
  onClose: () => void
  onRejected: (question: QuestionItemDto) => void
}

/** The backend requires a reason; it is shown to whoever edits the question next. */
export function RejectQuestionDialog({ courseId, question, onClose, onRejected }: RejectQuestionDialogProps) {
  const id = useId()
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (pending) return
    if (!reason.trim()) {
      setError('Enter a reason for rejecting this question.')
      return
    }
    setPending(true)
    setError(null)
    try {
      onRejected(await teacherQuestionsApi.reject(courseId, question.questionId, reason.trim()))
    } catch (err) {
      setError(toApiError(err).message)
    } finally {
      setPending(false)
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && !pending && onClose()}>
      <DialogContent>
        <form onSubmit={submit} noValidate className="contents">
          <DialogHeader>
            <DialogTitle>Reject question?</DialogTitle>
            <DialogDescription className="line-clamp-3">{question.questionText}</DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-2">
            <label htmlFor={id} className="text-sm font-semibold">
              Reason
            </label>
            <textarea
              id={id}
              autoFocus
              rows={4}
              maxLength={REJECTION_REASON_MAX}
              value={reason}
              onChange={(event) => {
                setReason(event.target.value)
                if (error) setError(null)
              }}
              aria-invalid={Boolean(error)}
              aria-describedby={`${id}-note`}
              placeholder="For example: the model answer is incomplete, or the question is outside the learning outcomes."
              className="w-full rounded-[14px] border border-input bg-background px-4 py-3 text-[15px] outline-none focus-visible:border-brand focus-visible:ring-4 focus-visible:ring-brand/15 aria-invalid:border-destructive"
            />
            <p id={`${id}-note`} className={error ? 'text-[13px] text-destructive' : 'text-[13px] text-muted-foreground'}>
              {error ?? 'Rejected questions cannot be used in exams. Editing a rejected question sends it back for review.'}
            </p>
          </div>

          <DialogFooter>
            <Button type="button" variant="secondary" disabled={pending} onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" variant="destructive" disabled={pending}>
              {pending ? 'Rejecting…' : 'Reject question'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
