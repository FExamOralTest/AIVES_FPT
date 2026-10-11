import { CircleAlert, CircleCheck, Info, LoaderCircle, TriangleAlert } from 'lucide-react'
import { useState, type FormEvent } from 'react'
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
import { toApiError } from '@/lib/http/apiError'
import { cn } from '@/lib/utils'
import { examsApi } from '../api/examsApi'
import type { CourseCheck, Exam } from '../types'

interface KnownCourse {
  courseId: number
  courseName: string | null
}

interface CreateExamDialogProps {
  open: boolean
  /** Courses seen on the teacher's existing exams, offered as one-click picks. */
  knownCourses: KnownCourse[]
  onClose: () => void
  onCreated: (exam: Exam) => void
}

export function CreateExamDialog({ open, knownCourses, onClose, onCreated }: CreateExamDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>{open && <CreateExamForm knownCourses={knownCourses} onCreated={onCreated} />}</DialogContent>
    </Dialog>
  )
}

type Field = 'title' | 'courseId' | 'durationMinutes' | 'maxQuestions'
type CheckState = { status: 'idle' } | { status: 'checking'; courseId: number } | ({ courseId: number } & CourseCheck & { status: 'done' })

function CreateExamForm({ knownCourses, onCreated }: Pick<CreateExamDialogProps, 'knownCourses' | 'onCreated'>) {
  const [values, setValues] = useState<Record<Field, string>>({
    title: '',
    courseId: knownCourses.length === 1 ? String(knownCourses[0].courseId) : '',
    durationMinutes: '30',
    maxQuestions: '10',
  })
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [check, setCheck] = useState<CheckState>({ status: 'idle' })
  const [submitting, setSubmitting] = useState(false)

  const set = (field: Field, value: string) => {
    setValues((prev) => ({ ...prev, [field]: value }))
    if (errors[field]) setErrors((prev) => ({ ...prev, [field]: undefined }))
    if (field === 'courseId') setCheck({ status: 'idle' })
  }

  async function checkCourse(courseId: number): Promise<CourseCheck> {
    if (check.status === 'done' && check.courseId === courseId) return check
    setCheck({ status: 'checking', courseId })
    const result = await examsApi.checkCourse(courseId)
    setCheck({ status: 'done', courseId, ...result })
    return result
  }

  function validate() {
    const next: Partial<Record<Field, string>> = {}
    const title = values.title.trim()
    if (!title) next.title = 'Nhập tên đề thi.'
    else if (title.length > 255) next.title = 'Tên đề thi tối đa 255 ký tự.'
    if (!isPositiveInt(values.courseId)) next.courseId = 'Nhập mã số môn học (số nguyên dương).'
    const duration = Number(values.durationMinutes)
    if (!isPositiveInt(values.durationMinutes) || duration > 480) next.durationMinutes = 'Thời lượng từ 1 đến 480 phút.'
    const max = Number(values.maxQuestions)
    if (!isPositiveInt(values.maxQuestions) || max > 200) next.maxQuestions = 'Số câu hỏi từ 1 đến 200.'
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
      const courseId = Number(values.courseId)
      // An unknown course would hit a database constraint (500), so confirm it first.
      const course = await checkCourse(courseId)
      if (!course.ok) return // the failed check is shown under the course field
      onCreated(
        await examsApi.create({
          title: values.title.trim(),
          courseId,
          durationMinutes: Number(values.durationMinutes),
          maxQuestions: Number(values.maxQuestions),
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

  return (
    <form onSubmit={handleSubmit} noValidate className="contents">
      <DialogHeader>
        <DialogTitle>Tạo đề thi</DialogTitle>
        <DialogDescription>Hệ thống tự sinh mã đề dạng EX-XXXXXXXX. Đề mới ở trạng thái Nháp.</DialogDescription>
      </DialogHeader>

      {formError && (
        <Alert variant="destructive" className="rounded-[14px] border-0 bg-[#fef1f0] px-4 py-3.5">
          <CircleAlert />
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      )}

      <div className="flex flex-col gap-4">
        <FormField
          label="Tên đề thi"
          placeholder="Kỳ thi giữa kỳ JPD123 - Lần 1"
          autoFocus
          value={values.title}
          error={errors.title}
          onChange={(e) => set('title', e.target.value)}
        />

        <div className="flex flex-col gap-2">
          <FormField
            label="Mã số môn học"
            inputMode="numeric"
            placeholder="Ví dụ: 1"
            value={values.courseId}
            error={errors.courseId ?? (check.status === 'done' && !check.ok ? check.message : undefined)}
            hint="Chưa có API lấy danh sách môn của bạn. Hỏi quản trị viên mã số môn được phân công."
            onChange={(e) => set('courseId', e.target.value.replace(/\D/g, ''))}
            onBlur={() => isPositiveInt(values.courseId) && void checkCourse(Number(values.courseId))}
          />
          {knownCourses.length > 0 && (
            <div className="flex flex-wrap items-center gap-1.5 text-[13px]">
              <span className="text-muted-foreground">Môn đã dùng:</span>
              {knownCourses.map((c) => (
                <button
                  key={c.courseId}
                  type="button"
                  onClick={() => {
                    set('courseId', String(c.courseId))
                    void checkCourse(c.courseId)
                  }}
                  className={cn(
                    'rounded-full px-2.5 py-1 transition-colors',
                    values.courseId === String(c.courseId) ? 'bg-foreground text-background' : 'bg-muted hover:bg-[#eaebef]',
                  )}
                >
                  #{c.courseId} {c.courseName}
                </button>
              ))}
            </div>
          )}
          <CourseCheckNote check={check} />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <FormField
            label="Thời lượng (phút)"
            inputMode="numeric"
            value={values.durationMinutes}
            error={errors.durationMinutes}
            onChange={(e) => set('durationMinutes', e.target.value.replace(/\D/g, ''))}
          />
          <FormField
            label="Số câu hỏi tối đa"
            inputMode="numeric"
            value={values.maxQuestions}
            error={errors.maxQuestions}
            onChange={(e) => set('maxQuestions', e.target.value.replace(/\D/g, ''))}
          />
        </div>

        <p className="flex gap-2 rounded-[14px] bg-muted px-4 py-3 text-[13px] text-muted-foreground">
          <Info className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          Chọn câu hỏi cho đề đang hoàn thiện: ngân hàng câu hỏi chờ AI sinh câu hỏi. Đề tạo bây giờ chưa có câu hỏi.
        </p>
      </div>

      <DialogFooter>
        <DialogClose asChild>
          <Button type="button" variant="secondary">
            Huỷ
          </Button>
        </DialogClose>
        <Button type="submit" disabled={submitting}>
          {submitting ? 'Đang tạo…' : 'Tạo đề thi'}
        </Button>
      </DialogFooter>
    </form>
  )
}

function CourseCheckNote({ check }: { check: CheckState }) {
  if (check.status === 'idle') return null
  if (check.status === 'checking')
    return (
      <p className="flex items-center gap-1.5 text-[13px] text-muted-foreground" role="status">
        <LoaderCircle className="size-3.5 animate-spin motion-reduce:animate-none" aria-hidden="true" />
        Đang kiểm tra môn học…
      </p>
    )
  if (!check.ok) return null // shown as the course field's error
  const count = check.docTypes.length
  return count >= 2 ? (
    <p className="flex items-center gap-1.5 text-[13px] text-success" role="status">
      <CircleCheck className="size-3.5" aria-hidden="true" />
      Môn học hợp lệ, đã có đủ 2/2 tài liệu .md.
    </p>
  ) : (
    <p className="flex items-start gap-1.5 text-[13px] text-warning" role="status">
      <TriangleAlert className="mt-0.5 size-3.5 shrink-0" aria-hidden="true" />
      Môn học hợp lệ nhưng mới có {count}/2 tài liệu .md. Vẫn tạo được đề, nhưng AI cần đủ 2 file để hỏi thi.
    </p>
  )
}

function isPositiveInt(value: string) {
  return /^\d+$/.test(value) && Number(value) > 0
}
