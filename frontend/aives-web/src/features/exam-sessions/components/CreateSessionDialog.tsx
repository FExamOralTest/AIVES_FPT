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
  /** Preselect an exam, e.g. when coming from the exam detail's "Tạo ca thi" button. */
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
  const { state: exams } = useApi(examsApi.listMine, '', 'Không tải được danh sách đề thi.')

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
    if (!values.examId) next.examId = 'Chọn đề thi.'
    const name = values.sessionName.trim()
    if (!name) next.sessionName = 'Nhập tên ca thi.'
    else if (name.length > 150) next.sessionName = 'Tên ca thi tối đa 150 ký tự.'
    if (values.room.trim().length > 150) next.room = 'Phòng thi tối đa 150 ký tự.'
    const start = new Date(values.start)
    const end = new Date(values.end)
    if (Number.isNaN(start.getTime())) next.start = 'Chọn giờ bắt đầu.'
    else if (start.getTime() < Date.now() - 5 * 60_000) next.start = 'Giờ bắt đầu không được ở quá khứ.'
    if (Number.isNaN(end.getTime())) next.end = 'Chọn giờ kết thúc.'
    else if (!next.start && end <= start) next.end = 'Giờ kết thúc phải sau giờ bắt đầu.'
    const cap = Number(values.maxCapacity)
    if (!/^\d+$/.test(values.maxCapacity) || cap < 1 || cap > 500) next.maxCapacity = 'Sức chứa từ 1 đến 500.'
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
        <DialogTitle>Tạo ca thi</DialogTitle>
        <DialogDescription>Hệ thống tự sinh mã tham gia ESS-XXXXXXXX để gửi cho sinh viên.</DialogDescription>
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
            Đề thi
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
              {exams.status === 'loading' ? 'Đang tải đề thi…' : examList.length ? 'Chọn đề thi' : 'Chưa có đề thi'}
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
                  Bạn chưa có đề thi.{' '}
                  <Link to="/teacher/exams" className="font-semibold text-brand">
                    Tạo đề thi trước
                  </Link>
                  .
                </>
              ) : (
                'Chọn đề sẽ tự điền giờ kết thúc theo thời lượng của đề.'
              ))}
          </p>
        </div>

        <FormField
          label="Tên ca thi"
          placeholder="Ca sáng - Nhóm 1"
          value={values.sessionName}
          error={errors.sessionName}
          onChange={(e) => set('sessionName', e.target.value)}
        />
        <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
          <FormField
            label="Phòng thi"
            placeholder="Phòng 301, tòa E (không bắt buộc)"
            value={values.room}
            error={errors.room}
            onChange={(e) => set('room', e.target.value)}
          />
          <FormField
            label="Sức chứa"
            inputMode="numeric"
            value={values.maxCapacity}
            error={errors.maxCapacity}
            onChange={(e) => set('maxCapacity', e.target.value.replace(/\D/g, ''))}
          />
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField
            label="Bắt đầu"
            type="datetime-local"
            value={values.start}
            error={errors.start}
            onChange={(e) => set('start', e.target.value)}
          />
          <FormField
            label="Kết thúc"
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
            Huỷ
          </Button>
        </DialogClose>
        <Button type="submit" disabled={submitting}>
          {submitting ? 'Đang tạo…' : 'Tạo ca thi'}
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
