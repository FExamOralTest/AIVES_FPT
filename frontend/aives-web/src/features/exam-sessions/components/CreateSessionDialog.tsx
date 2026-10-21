import { CircleAlert } from 'lucide-react'
import { useId, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
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
      <DialogContent>{open && <CreateSessionForm initialExamId={initialExamId} onCreated={onCreated} />}</DialogContent>
    </Dialog>
  )
}

type Field = 'examId' | 'sessionName' | 'room' | 'start' | 'end' | 'maxCapacity'

const SELECT_CLASS =
  'h-12 w-full rounded-[14px] border border-input bg-background px-4 text-[15px] outline-none focus-visible:border-brand focus-visible:ring-4 focus-visible:ring-brand/15'

function CreateSessionForm({ initialExamId, onCreated }: Pick<CreateSessionDialogProps, 'initialExamId' | 'onCreated'>) {
  const examSelectId = useId()
  const { state: exams } = useApi(examsApi.listMine, '', 'Could not load exams.')

  const [values, setValues] = useState<Record<Field, string>>(() => {
    const start = nextHalfHour()
    return {
      examId: initialExamId ? String(initialExamId) : '',
      sessionName: '',
      room: '',
      start: toLocalInput(start),
      end: toLocalInput(new Date(start.getTime() + 60 * 60_000)),
      maxCapacity: '50',
    }
  })
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const set = (field: Field, value: string) => {
    setValues((prev) => ({ ...prev, [field]: value }))
    if (errors[field]) setErrors((prev) => ({ ...prev, [field]: undefined }))
  }

  // Picking an exam fills the end time from its duration, unless the teacher already changed it.
  function pickExam(examId: string, list: Exam[]) {
    const exam = list.find((e) => String(e.examId) === examId)
    setValues((prev) => {
      const start = new Date(prev.start)
      const end = exam && !Number.isNaN(start.getTime()) ? toLocalInput(new Date(start.getTime() + exam.durationMinutes * 60_000)) : prev.end
      return { ...prev, examId, end }
    })
    if (errors.examId) setErrors((prev) => ({ ...prev, examId: undefined }))
  }

  function validate() {
    const next: Partial<Record<Field, string>> = {}
    if (!values.examId) next.examId = 'Choose an exam.'
    const name = values.sessionName.trim()
    if (!name) next.sessionName = 'Enter a session name.'
    else if (name.length > 150) next.sessionName = 'Session name can be at most 150 characters.'
    if (values.room.trim().length > 150) next.room = 'Room can be at most 150 characters.'
    const start = new Date(values.start)
    const end = new Date(values.end)
    if (Number.isNaN(start.getTime())) next.start = 'Choose a start time.'
    else if (start.getTime() < Date.now() - 5 * 60_000) next.start = 'The start time cannot be in the past.'
    if (Number.isNaN(end.getTime())) next.end = 'Choose an end time.'
    else if (!next.start && end <= start) next.end = 'The end time must be after the start time.'
    const cap = Number(values.maxCapacity)
    if (!/^\d+$/.test(values.maxCapacity) || cap < 1 || cap > 500) next.maxCapacity = 'Capacity must be between 1 and 500.'
    return next
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const next = validate()
    setErrors(next)
    setFormError(null)
    if (Object.keys(next).length) return

    setSubmitting(true)
    try {
      onCreated(
        await sessionsApi.create({
          examId: Number(values.examId),
          sessionName: values.sessionName.trim(),
          room: values.room.trim() || undefined,
          // datetime-local is local time; the backend compares against UTC.
          startTime: new Date(values.start).toISOString(),
          endTime: new Date(values.end).toISOString(),
          maxCapacity: Number(values.maxCapacity),
        }),
      )
    } catch (err) {
      const apiError = toApiError(err)
      setErrors(apiError.fieldErrors)
      setFormError(apiError.message)
    } finally {
      setSubmitting(false)
    }
  }

  const examList = exams.status === 'ready' ? exams.data : []

  return (
    <form onSubmit={handleSubmit} noValidate className="contents">
      <DialogHeader>
        <DialogTitle>Create exam session</DialogTitle>
        <DialogDescription>The system generates an ESS-XXXXXXXX join code to send to students.</DialogDescription>
      </DialogHeader>

      {formError && (
        <Alert variant="destructive" className="rounded-[14px] border-0 bg-[#fef1f0] px-4 py-3.5">
          <CircleAlert />
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      )}

      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-2">
          <Label htmlFor={examSelectId} className="text-sm font-semibold">
            Exam
          </Label>
          <select
            id={examSelectId}
            value={values.examId}
            onChange={(e) => pickExam(e.target.value, examList)}
            disabled={exams.status !== 'ready' || examList.length === 0}
            aria-invalid={Boolean(errors.examId)}
            aria-describedby={`${examSelectId}-note`}
            className={cn(SELECT_CLASS, errors.examId && 'border-destructive ring-3 ring-destructive/20')}
          >
            <option value="">
              {exams.status === 'loading' ? 'Loading exams…' : examList.length ? 'Choose an exam' : 'No exams yet'}
            </option>
            {examList.map((exam) => (
              <option key={exam.examId} value={exam.examId}>
                {exam.title} ({exam.examCode})
              </option>
            ))}
          </select>
          <p id={`${examSelectId}-note`} className={cn('text-[13px]', errors.examId || exams.status === 'error' ? 'text-destructive' : 'text-muted-foreground')}>
            {errors.examId ??
              (exams.status === 'error' ? (
                exams.message
              ) : exams.status === 'ready' && examList.length === 0 ? (
                <>
                  You have no exams yet.{' '}
                  <Link to="/staff/exams" className="font-semibold text-brand">
                    Create an exam first
                  </Link>
                  .
                </>
              ) : (
                'Choosing an exam fills in the end time from its duration.'
              ))}
          </p>
        </div>

        <FormField
          label="Session name"
          placeholder="Morning session - Group 1"
          value={values.sessionName}
          error={errors.sessionName}
          onChange={(e) => set('sessionName', e.target.value)}
        />
        <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
          <FormField
            label="Room"
            placeholder="Room 301, Building E (optional)"
            value={values.room}
            error={errors.room}
            onChange={(e) => set('room', e.target.value)}
          />
          <FormField
            label="Capacity"
            inputMode="numeric"
            value={values.maxCapacity}
            error={errors.maxCapacity}
            onChange={(e) => set('maxCapacity', e.target.value.replace(/\D/g, ''))}
          />
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField
            label="Start"
            type="datetime-local"
            value={values.start}
            error={errors.start}
            onChange={(e) => set('start', e.target.value)}
          />
          <FormField
            label="End"
            type="datetime-local"
            value={values.end}
            error={errors.end}
            onChange={(e) => set('end', e.target.value)}
          />
        </div>
      </div>

      <DialogFooter>
        <DialogClose asChild>
          <Button type="button" variant="secondary">
            Cancel
          </Button>
        </DialogClose>
        <Button type="submit" disabled={submitting}>
          {submitting ? 'Creating…' : 'Create session'}
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
