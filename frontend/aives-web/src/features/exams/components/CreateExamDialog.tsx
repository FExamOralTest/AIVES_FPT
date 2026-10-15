import {
  BookOpen,
  CircleAlert,
  CircleCheck,
  LoaderCircle,
  Sparkles,
  TriangleAlert,
} from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
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
import { teacherCoursesApi } from '@/features/teacher-courses/api/teacherCoursesApi'
import type { TeacherCourse } from '@/features/teacher-courses/types'
import { teacherQuestionsApi } from '@/features/teacher-questions/api/teacherQuestionsApi'
import type { QuestionItemDto } from '@/features/teacher-questions/types'
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
  knownCourses: KnownCourse[]
  onClose: () => void
  onCreated: (exam: Exam) => void
}

export function CreateExamDialog({ open, knownCourses, onClose, onCreated }: CreateExamDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        {open && <CreateExamForm knownCourses={knownCourses} onCreated={onCreated} />}
      </DialogContent>
    </Dialog>
  )
}

type Field = 'title' | 'courseId' | 'durationMinutes' | 'maxQuestions'
type CheckState =
  | { status: 'idle' }
  | { status: 'checking'; courseId: number }
  | ({ courseId: number } & CourseCheck & { status: 'done' })

function CreateExamForm({ knownCourses, onCreated }: Pick<CreateExamDialogProps, 'knownCourses' | 'onCreated'>) {
  const [courses, setCourses] = useState<TeacherCourse[]>([])
  const [loadingCourses, setLoadingCourses] = useState(true)

  const [values, setValues] = useState<Record<Field, string>>({
    title: '',
    courseId: '',
    durationMinutes: '30',
    maxQuestions: '10',
  })
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [check, setCheck] = useState<CheckState>({ status: 'idle' })
  const [submitting, setSubmitting] = useState(false)

  // Question bank state
  const [bankQuestions, setBankQuestions] = useState<QuestionItemDto[]>([])
  const [loadingQuestions, setLoadingQuestions] = useState(false)
  const [selectedQuestionIds, setSelectedQuestionIds] = useState<Set<number>>(new Set())
  const [aiSuggesting, setAiSuggesting] = useState(false)
  const [aiRationale, setAiRationale] = useState<string | null>(null)

  // Fetch teacher assigned courses on mount
  useEffect(() => {
    let cancelled = false
    async function loadTeacherCourses() {
      setLoadingCourses(true)
      try {
        const data = await teacherCoursesApi.listMine()
        if (cancelled) return
        setCourses(data)
        if (data.length > 0) {
          const initialCourseId = String(data[0].courseId)
          setValues((prev) => ({ ...prev, courseId: prev.courseId || initialCourseId }))
          void checkCourse(data[0].courseId)
          void loadQuestionsForCourse(data[0].courseId)
        } else if (knownCourses.length > 0) {
          const initialCourseId = String(knownCourses[0].courseId)
          setValues((prev) => ({ ...prev, courseId: prev.courseId || initialCourseId }))
          void checkCourse(knownCourses[0].courseId)
          void loadQuestionsForCourse(knownCourses[0].courseId)
        }
      } catch {
        if (!cancelled && knownCourses.length > 0) {
          const initialCourseId = String(knownCourses[0].courseId)
          setValues((prev) => ({ ...prev, courseId: prev.courseId || initialCourseId }))
          void checkCourse(knownCourses[0].courseId)
          void loadQuestionsForCourse(knownCourses[0].courseId)
        }
      } finally {
        if (!cancelled) setLoadingCourses(false)
      }
    }
    void loadTeacherCourses()
    return () => {
      cancelled = true
    }
  }, [])

  const set = (field: Field, value: string) => {
    setValues((prev) => ({ ...prev, [field]: value }))
    if (errors[field]) setErrors((prev) => ({ ...prev, [field]: undefined }))
    if (field === 'courseId') {
      setCheck({ status: 'idle' })
      setSelectedQuestionIds(new Set())
      setAiRationale(null)
      const num = Number(value)
      if (num > 0) {
        void checkCourse(num)
        void loadQuestionsForCourse(num)
      } else {
        setBankQuestions([])
      }
    }
  }

  async function loadQuestionsForCourse(courseId: number) {
    setLoadingQuestions(true)
    setSelectedQuestionIds(new Set())
    setAiRationale(null)
    try {
      const res = await teacherQuestionsApi.getQuestions(courseId, { page: 1, pageSize: 100 })
      setBankQuestions(res.items)
    } catch {
      setBankQuestions([])
    } finally {
      setLoadingQuestions(false)
    }
  }

  async function checkCourse(courseId: number): Promise<CourseCheck> {
    if (check.status === 'done' && check.courseId === courseId) return check
    setCheck({ status: 'checking', courseId })
    const result = await examsApi.checkCourse(courseId)
    setCheck({ status: 'done', courseId, ...result })
    return result
  }

  // Handle AI question suggestions
  async function handleAiSuggest() {
    const courseId = Number(values.courseId)
    if (!courseId) return
    const max = Number(values.maxQuestions) || 10
    setAiSuggesting(true)
    setAiRationale(null)
    try {
      const res = await teacherQuestionsApi.suggestQuestions(courseId, max)
      const suggestedIds = new Set(res.suggestedQuestions.map((q) => q.questionId))
      setSelectedQuestionIds(suggestedIds)
      setAiRationale(
        res.aiRationale ||
          `AI đã chọn ${res.suggestedQuestions.length} câu hỏi phù hợp nhất theo chuẩn đầu ra và nội dung môn học.`,
      )
    } catch (err) {
      const apiErr = toApiError(err)
      setFormError(`Không thể lấy gợi ý câu hỏi từ AI: ${apiErr.message}`)
    } finally {
      setAiSuggesting(false)
    }
  }

  function toggleQuestion(questionId: number) {
    setSelectedQuestionIds((prev) => {
      const next = new Set(prev)
      if (next.has(questionId)) next.delete(questionId)
      else next.add(questionId)
      return next
    })
  }

  function toggleSelectAll() {
    const max = Number(values.maxQuestions) || 10
    if (selectedQuestionIds.size === bankQuestions.length) {
      setSelectedQuestionIds(new Set())
    } else {
      const idsToSelect = bankQuestions.slice(0, max).map((q) => q.questionId)
      setSelectedQuestionIds(new Set(idsToSelect))
    }
  }

  function validate() {
    const next: Partial<Record<Field, string>> = {}
    const title = values.title.trim()
    if (!title) next.title = 'Nhập tên đề thi.'
    else if (title.length > 255) next.title = 'Tên đề thi tối đa 255 ký tự.'
    if (!isPositiveInt(values.courseId)) next.courseId = 'Vui lòng chọn môn học.'
    const duration = Number(values.durationMinutes)
    if (!isPositiveInt(values.durationMinutes) || duration > 480) next.durationMinutes = 'Thời lượng từ 1 đến 480 phút.'
    const max = Number(values.maxQuestions)
    if (!isPositiveInt(values.maxQuestions) || max > 200) next.maxQuestions = 'Số câu hỏi từ 1 đến 200.'
    if (selectedQuestionIds.size > max) {
      next.maxQuestions = `Số câu hỏi đã chọn (${selectedQuestionIds.size}) vượt quá số lượng tối đa (${max}).`
    }
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
      const course = await checkCourse(courseId)
      if (!course.ok) return

      const questionsPayload =
        selectedQuestionIds.size > 0
          ? Array.from(selectedQuestionIds).map((id) => ({
              questionId: id,
              questionScore: 1.0,
            }))
          : undefined

      const created = await examsApi.create({
        title: values.title.trim(),
        courseId,
        durationMinutes: Number(values.durationMinutes),
        maxQuestions: Number(values.maxQuestions),
        questions: questionsPayload,
      })
      onCreated(created)
    } catch (err) {
      const apiError = toApiError(err)
      setErrors(apiError.fieldErrors)
      setFormError(apiError.message)
    } finally {
      setSubmitting(false)
    }
  }

  const maxQuestionsNum = Number(values.maxQuestions) || 10

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

        {/* Môn học phụ trách */}
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium text-foreground">
            Môn học <span className="text-destructive">*</span>
          </label>

          {loadingCourses ? (
            <div className="flex items-center gap-2 rounded-xl border px-3.5 py-2.5 text-sm text-muted-foreground">
              <LoaderCircle className="size-4 animate-spin motion-reduce:animate-none" aria-hidden="true" />
              Đang tải danh sách môn học của bạn…
            </div>
          ) : courses.length > 0 ? (
            <select
              value={values.courseId}
              onChange={(e) => set('courseId', e.target.value)}
              className="w-full rounded-xl border border-input bg-background px-3.5 py-2.5 text-sm font-normal shadow-sm transition-colors focus:outline-none focus:ring-2 focus:ring-primary/20"
            >
              <option value="">-- Chọn môn học --</option>
              {courses.map((c) => (
                <option key={c.courseId} value={String(c.courseId)}>
                  {c.courseCode} - {c.courseName} (#{c.courseId})
                </option>
              ))}
            </select>
          ) : (
            <div className="flex flex-col gap-2">
              <FormField
                label=""
                inputMode="numeric"
                placeholder="Nhập mã số môn học (ví dụ: 1)"
                value={values.courseId}
                error={errors.courseId ?? (check.status === 'done' && !check.ok ? check.message : undefined)}
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
                      onClick={() => set('courseId', String(c.courseId))}
                      className={cn(
                        'rounded-full px-2.5 py-1 transition-colors',
                        values.courseId === String(c.courseId)
                          ? 'bg-foreground text-background'
                          : 'bg-muted hover:bg-[#eaebef]',
                      )}
                    >
                      #{c.courseId} {c.courseName}
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}

          {errors.courseId && <p className="text-xs text-destructive">{errors.courseId}</p>}
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

        {/* Khối chọn câu hỏi cho đề thi */}
        <div className="flex flex-col gap-2.5 rounded-2xl border p-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <h3 className="font-heading text-sm font-medium">
                Chọn câu hỏi cho đề thi{' '}
                <span className="font-normal text-muted-foreground">
                  (Đã chọn {selectedQuestionIds.size}/{maxQuestionsNum})
                </span>
              </h3>
              <p className="text-xs text-muted-foreground">
                Chọn câu hỏi từ ngân hàng hoặc để AI tự động chọn câu hỏi tối ưu.
              </p>
            </div>
            <div className="flex items-center gap-2">
              {bankQuestions.length > 0 && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={toggleSelectAll}
                  className="h-8 text-xs"
                >
                  {selectedQuestionIds.size === bankQuestions.length ? 'Bỏ chọn hết' : 'Chọn nhanh'}
                </Button>
              )}
              <Button
                type="button"
                variant="default"
                size="sm"
                disabled={aiSuggesting || !values.courseId || bankQuestions.length === 0}
                onClick={handleAiSuggest}
                className="h-8 gap-1.5 text-xs bg-indigo-600 hover:bg-indigo-700 text-white"
              >
                {aiSuggesting ? (
                  <LoaderCircle className="size-3.5 animate-spin" />
                ) : (
                  <Sparkles className="size-3.5 text-yellow-300" />
                )}
                {aiSuggesting ? 'AI đang chọn…' : 'AI gợi ý chọn câu'}
              </Button>
            </div>
          </div>

          {aiRationale && (
            <div className="flex items-start gap-2 rounded-xl bg-indigo-50 border border-indigo-100 p-2.5 text-xs text-indigo-900">
              <Sparkles className="mt-0.5 size-3.5 shrink-0 text-indigo-600" />
              <span>{aiRationale}</span>
            </div>
          )}

          {loadingQuestions ? (
            <div className="flex items-center justify-center gap-2 py-6 text-xs text-muted-foreground">
              <LoaderCircle className="size-4 animate-spin" /> Đang tải ngân hàng câu hỏi…
            </div>
          ) : bankQuestions.length === 0 ? (
            <div className="flex flex-col items-center justify-center gap-1.5 rounded-xl border border-dashed py-6 text-center text-xs text-muted-foreground">
              <BookOpen className="size-6 text-muted-foreground/60" />
              <span>Ngân hàng câu hỏi của môn này hiện chưa có câu hỏi.</span>
              <span>Bạn có thể tạo đề nháp trước, hoặc vào mục "Ngân hàng câu hỏi" để AI sinh câu hỏi từ tài liệu.</span>
            </div>
          ) : (
            <div className="max-h-52 overflow-y-auto rounded-xl border divide-y text-xs">
              {bankQuestions.map((q) => {
                const selected = selectedQuestionIds.has(q.questionId)
                return (
                  <label
                    key={q.questionId}
                    className={cn(
                      'flex items-start gap-3 p-2.5 cursor-pointer transition-colors hover:bg-muted/50',
                      selected && 'bg-primary/5',
                    )}
                  >
                    <input
                      type="checkbox"
                      checked={selected}
                      onChange={() => toggleQuestion(q.questionId)}
                      className="mt-0.5 size-4 rounded border-gray-300 text-primary focus:ring-primary"
                    />
                    <div className="flex-1 space-y-1">
                      <p className="font-normal text-foreground leading-snug">{q.questionText}</p>
                      <div className="flex flex-wrap items-center gap-2 text-[11px] text-muted-foreground">
                        {q.topic && <span className="rounded bg-muted px-1.5 py-0.5">Chủ đề: {q.topic}</span>}
                        <span
                          className={cn(
                            'rounded px-1.5 py-0.5 font-medium',
                            q.level === 1
                              ? 'bg-emerald-100 text-emerald-800'
                              : q.level === 3
                                ? 'bg-amber-100 text-amber-800'
                                : 'bg-blue-100 text-blue-800',
                          )}
                        >
                          {q.level === 1 ? 'Dễ' : q.level === 3 ? 'Khó' : 'Trung bình'}
                        </span>
                        <span>Điểm: {q.maxScore ?? 1.0}</span>
                      </div>
                    </div>
                  </label>
                )
              })}
            </div>
          )}
        </div>
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
  if (!check.ok) return null
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

