import {
  AlertCircle,
  ArrowRight,
  BookOpen,
  CheckCircle2,
  Filter,
  Layers,
  LibraryBig,
  RefreshCw,
  Sparkles,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { toApiError } from '@/lib/http/apiError'
import { teacherCoursesApi } from '@/features/teacher-courses/api/teacherCoursesApi'
import type { TeacherCourse } from '@/features/teacher-courses/types'
import { teacherQuestionsApi } from '../api/teacherQuestionsApi'
import type { QuestionItemDto } from '../types'

const LEVEL_LABELS: Record<number, { label: string; variant: 'secondary' | 'default' | 'destructive' }> = {
  1: { label: 'Cơ bản', variant: 'secondary' },
  2: { label: 'Trung bình', variant: 'default' },
  3: { label: 'Nâng cao', variant: 'destructive' },
}

export default function TeacherQuestionsPage() {
  const [searchParams] = useSearchParams()
  const initialCourseId = searchParams.get('courseId')

  const [courses, setCourses] = useState<TeacherCourse[]>([])
  const [selectedCourseId, setSelectedCourseId] = useState<number | null>(
    initialCourseId ? Number(initialCourseId) : null,
  )
  const [questions, setQuestions] = useState<QuestionItemDto[]>([])
  const [filterLevel, setFilterLevel] = useState<number | 'ALL'>('ALL')

  // Generate form state
  const [generateCount, setGenerateCount] = useState(10)
  const [focusTopics, setFocusTopics] = useState('')
  const [isGenerating, setIsGenerating] = useState(false)
  const [isLoadingQuestions, setIsLoadingQuestions] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  useEffect(() => {
    async function init() {
      try {
        const list = await teacherCoursesApi.listMine()
        setCourses(list)
        if (list.length > 0 && !selectedCourseId) {
          setSelectedCourseId(list[0].courseId)
        }
      } catch (err) {
        const apiErr = toApiError(err)
        setErrorMessage(apiErr.message)
      }
    }
    init()
  }, [])

  useEffect(() => {
    if (selectedCourseId) {
      loadQuestions(selectedCourseId)
    }
  }, [selectedCourseId])

  async function loadQuestions(courseId: number) {
    setIsLoadingQuestions(true)
    setErrorMessage(null)
    try {
      const data = await teacherQuestionsApi.suggestQuestions(courseId, 50)
      setQuestions(data.suggestedQuestions)
    } catch {
      // If none yet or error, keep empty
      setQuestions([])
    } finally {
      setIsLoadingQuestions(false)
    }
  }

  async function handleGenerate(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedCourseId) return

    setIsGenerating(true)
    setErrorMessage(null)
    setSuccessMessage(null)

    try {
      const res = await teacherQuestionsApi.generateQuestions(selectedCourseId, {
        questionCount: generateCount,
        focusTopics: focusTopics.trim(),
      })

      const generated = res.questions.map((q) => ({
        questionId: q.questionId,
        questionText: q.questionText,
        level: q.level ?? 2,
        maxScore: q.maxScore ?? 1.0,
        isFollowUpAllowed: q.isFollowUpAllowed ?? true,
        courseId: selectedCourseId,
      }))

      setQuestions((prev) => [...generated, ...prev])
      setSuccessMessage(`Đã sinh thành công ${generated.length} câu hỏi mới từ tài liệu Markdown!`)
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
    } finally {
      setIsGenerating(false)
    }
  }

  const filteredQuestions = questions.filter((q) =>
    filterLevel === 'ALL' ? true : q.level === filterLevel,
  )

  return (
    <div className="mx-auto max-w-5xl py-6">
      <header className="mb-8">
        <h1 className="font-heading text-3xl font-medium tracking-tight sm:text-4xl">
          Ngân hàng câu hỏi
        </h1>
        <p className="mt-2 text-muted-foreground">
          Sử dụng trí tuệ nhân tạo (AI) để tự động sinh ngân hàng câu hỏi đa cấp độ từ tài liệu Markdown môn học.
        </p>

        {/* Course Selector */}
        {courses.length > 0 && (
          <div className="mt-6 flex flex-wrap items-center gap-3">
            <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Môn học:
            </span>
            <div className="flex flex-wrap gap-2">
              {courses.map((c) => (
                <button
                  key={c.courseId}
                  type="button"
                  onClick={() => setSelectedCourseId(c.courseId)}
                  className={`rounded-xl px-4 py-2 text-xs font-medium transition-colors border ${
                    selectedCourseId === c.courseId
                      ? 'border-brand bg-brand text-white shadow-sm'
                      : 'border-input bg-card text-foreground hover:bg-muted'
                  }`}
                >
                  {c.courseCode} — {c.courseName}
                </button>
              ))}
            </div>
          </div>
        )}
      </header>

      {errorMessage && (
        <Alert variant="destructive" className="mb-6 rounded-2xl">
          <AlertCircle className="size-4" />
          <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
      )}

      {successMessage && (
        <Alert className="mb-6 rounded-2xl border-emerald-300 bg-emerald-50 text-emerald-900 dark:border-emerald-900/60 dark:bg-emerald-950/40 dark:text-emerald-200">
          <CheckCircle2 className="size-4 text-emerald-600" />
          <AlertDescription className="font-medium">{successMessage}</AlertDescription>
        </Alert>
      )}

      {/* AI Question Generation Panel */}
      <section className="mb-8 overflow-hidden rounded-3xl border bg-gradient-to-br from-brand-soft/30 via-background to-card p-6 shadow-sm sm:p-8">
        <div className="flex items-center gap-3">
          <span className="flex size-10 items-center justify-center rounded-2xl bg-brand text-white shadow-md">
            <Sparkles className="size-5" />
          </span>
          <div>
            <h2 className="font-heading text-lg font-semibold text-foreground">
              Sinh câu hỏi tự động với AI
            </h2>
            <p className="text-xs text-muted-foreground">
              AI sẽ phân tích tài liệu bài giảng và chuẩn đầu ra để tạo các câu hỏi chất lượng cao kèm thang điểm.
            </p>
          </div>
        </div>

        <form onSubmit={handleGenerate} className="mt-6 grid gap-4 sm:grid-cols-[10rem_1fr_auto] items-end">
          <div>
            <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-1.5">
              Số lượng câu
            </label>
            <input
              type="number"
              min="5"
              max="50"
              value={generateCount}
              onChange={(e) => setGenerateCount(parseInt(e.target.value) || 10)}
              className="h-11 w-full rounded-xl border border-input bg-background px-3 font-semibold text-sm outline-none focus:border-brand"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-1.5">
              Chủ đề trọng tâm (tùy chọn)
            </label>
            <input
              type="text"
              value={focusTopics}
              onChange={(e) => setFocusTopics(e.target.value)}
              placeholder="Ví dụ: Đa hình, Tính kế thừa, Clean Architecture..."
              className="h-11 w-full rounded-xl border border-input bg-background px-4 text-sm outline-none focus:border-brand"
            />
          </div>

          <Button
            type="submit"
            disabled={isGenerating || !selectedCourseId}
            className="h-11 gap-2 rounded-xl px-6 bg-brand hover:bg-brand-hover shadow-sm"
          >
            {isGenerating ? (
              <>
                <RefreshCw className="size-4 animate-spin" />
                AI đang sinh…
              </>
            ) : (
              <>
                <Sparkles className="size-4" />
                Bắt đầu sinh câu hỏi
              </>
            )}
          </Button>
        </form>
      </section>

      {/* Filter and Questions List */}
      <section className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <LibraryBig className="size-5 text-brand" />
            <h2 className="font-heading text-lg font-medium">
              Danh sách câu hỏi ({questions.length})
            </h2>
          </div>

          {/* Level Filter */}
          <div className="flex items-center gap-1.5">
            <Filter className="size-4 text-muted-foreground mr-1" />
            {(['ALL', 1, 2, 3] as const).map((lvl) => (
              <button
                key={lvl}
                type="button"
                onClick={() => setFilterLevel(lvl)}
                className={`rounded-lg px-3 py-1 text-xs font-medium transition-colors ${
                  filterLevel === lvl
                    ? 'bg-foreground text-background font-semibold'
                    : 'bg-muted text-muted-foreground hover:bg-muted/80'
                }`}
              >
                {lvl === 'ALL' ? 'Tất cả' : LEVEL_LABELS[lvl].label}
              </button>
            ))}
          </div>
        </div>

        {isLoadingQuestions ? (
          <div className="py-12 text-center text-sm text-muted-foreground">Đang tải câu hỏi…</div>
        ) : filteredQuestions.length === 0 ? (
          <div className="rounded-3xl border border-dashed p-12 text-center text-muted-foreground">
            <BookOpen className="mx-auto size-10 opacity-40 mb-3" />
            <p>Chưa có câu hỏi nào trong ngân hàng của môn này.</p>
            <p className="mt-1 text-xs">Hãy sử dụng bảng điều khiển phía trên để AI sinh câu hỏi.</p>
          </div>
        ) : (
          <div className="space-y-3">
            {filteredQuestions.map((q, idx) => {
              const meta = LEVEL_LABELS[q.level] ?? { label: `Level ${q.level}`, variant: 'default' }
              return (
                <article
                  key={q.questionId || idx}
                  className="flex flex-col gap-3 rounded-2xl border bg-card p-5 shadow-sm transition-all hover:border-brand/40 sm:flex-row sm:items-center sm:justify-between"
                >
                  <div className="flex-1 space-y-1.5">
                    <div className="flex items-center gap-2">
                      <span className="font-mono text-xs font-semibold text-brand">
                        #{idx + 1}
                      </span>
                      <Badge variant={meta.variant} className="text-xs">
                        <Layers className="size-3 mr-1" />
                        {meta.label}
                      </Badge>
                      {q.isFollowUpAllowed && (
                        <span className="text-[11px] text-muted-foreground font-medium">
                          • Hỗ trợ follow-up
                        </span>
                      )}
                    </div>
                    <p className="font-heading text-sm font-medium text-foreground">
                      {q.questionText}
                    </p>
                  </div>

                  <div className="shrink-0 text-right sm:pl-4">
                    <span className="font-mono text-xs font-semibold text-muted-foreground">
                      Điểm tối đa: {q.maxScore != null ? q.maxScore : '1.0'}đ
                    </span>
                  </div>
                </article>
              )
            })}
          </div>
        )}

        {/* Quick link to Exams */}
        {questions.length > 0 && (
          <div className="mt-8 flex justify-end">
            <Button asChild className="gap-2 rounded-xl">
              <Link to="/teacher/exams">
                Tạo đề thi từ ngân hàng câu hỏi
                <ArrowRight className="size-4" />
              </Link>
            </Button>
          </div>
        )}
      </section>
    </div>
  )
}
