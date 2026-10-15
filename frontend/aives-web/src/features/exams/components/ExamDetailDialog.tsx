import {
  BookOpen,
  CalendarPlus,
  Edit3,
  LibraryBig,
  LoaderCircle,
  Sparkles,
} from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { CopyButton } from '@/components/common/CopyButton'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { teacherQuestionsApi } from '@/features/teacher-questions/api/teacherQuestionsApi'
import type { QuestionItemDto } from '@/features/teacher-questions/types'
import { formatDateTime, parseApiDate } from '@/lib/format/date'
import { toApiError } from '@/lib/http/apiError'
import { useApi } from '@/lib/http/useApi'
import { cn } from '@/lib/utils'
import { examsApi } from '../api/examsApi'
import { EXAM_STATUS_LABEL, EXAM_STATUS_TONE } from '../lib/examStatus'
import type { Exam } from '../types'

export function ExamDetailDialog({ exam, onClose }: { exam: Exam | null; onClose: () => void }) {
  return (
    <Dialog open={exam !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        {exam && <ExamDetail key={exam.examId} summary={exam} />}
      </DialogContent>
    </Dialog>
  )
}

function ExamDetail({ summary }: { summary: Exam }) {
  const { state, reload } = useApi(() => examsApi.get(summary.examId), summary.examId, 'Không tải được chi tiết đề thi.')
  const exam = state.status === 'ready' ? state.data : summary
  const status = exam.examStatus ?? 'DRAFT'

  // Question editing state
  const [editingQuestions, setEditingQuestions] = useState(false)
  const [bankQuestions, setBankQuestions] = useState<QuestionItemDto[]>([])
  const [loadingBank, setLoadingBank] = useState(false)
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set())
  const [savingQuestions, setSavingQuestions] = useState(false)
  const [aiSuggesting, setAiSuggesting] = useState(false)
  const [aiRationale, setAiRationale] = useState<string | null>(null)
  const [saveError, setSaveError] = useState<string | null>(null)

  const facts = [
    { label: 'Môn học', value: exam.courseName ? `${exam.courseName} (#${exam.courseId})` : `#${exam.courseId}` },
    { label: 'Thời lượng', value: `${exam.durationMinutes} phút` },
    { label: 'Số câu tối đa', value: String(exam.maxQuestions) },
    { label: 'Tạo lúc', value: exam.createdAt ? formatDateTime(parseApiDate(exam.createdAt)) : '—' },
  ]

  async function openQuestionEditor() {
    setEditingQuestions(true)
    setSaveError(null)
    setAiRationale(null)
    setSelectedIds(new Set(exam.questions.map((q) => q.questionId)))
    setLoadingBank(true)
    try {
      const res = await teacherQuestionsApi.getQuestions(exam.courseId, { page: 1, pageSize: 100 })
      setBankQuestions(res.items)
    } catch {
      setBankQuestions([])
    } finally {
      setLoadingBank(false)
    }
  }

  async function handleAiSuggest() {
    setAiSuggesting(true)
    setSaveError(null)
    try {
      const res = await teacherQuestionsApi.suggestQuestions(exam.courseId, exam.maxQuestions)
      setSelectedIds(new Set(res.suggestedQuestions.map((q) => q.questionId)))
      setAiRationale(
        res.aiRationale ||
          `AI đã tự động chọn ${res.suggestedQuestions.length} câu hỏi phù hợp nhất cho đề thi.`,
      )
    } catch (err) {
      setSaveError(toApiError(err).message)
    } finally {
      setAiSuggesting(false)
    }
  }

  async function handleSaveQuestions() {
    setSavingQuestions(true)
    setSaveError(null)
    try {
      const payload = Array.from(selectedIds).map((id) => ({
        questionId: id,
        questionScore: 1.0,
      }))
      await examsApi.updateQuestions(exam.examId, payload)
      setEditingQuestions(false)
      reload()
    } catch (err) {
      setSaveError(toApiError(err).message)
    } finally {
      setSavingQuestions(false)
    }
  }

  function toggleQuestion(questionId: number) {
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (next.has(questionId)) next.delete(questionId)
      else next.add(questionId)
      return next
    })
  }

  return (
    <>
      <DialogHeader>
        <span className={cn('w-fit rounded-full px-2.5 py-1 text-xs font-medium', EXAM_STATUS_TONE[status])}>
          {EXAM_STATUS_LABEL[status]}
        </span>
        <DialogTitle>{exam.title}</DialogTitle>
        <DialogDescription className="flex flex-wrap items-center gap-2">
          Mã đề <code className="font-mono text-foreground">{exam.examCode}</code>
          <CopyButton value={exam.examCode} label="Sao chép mã đề" />
        </DialogDescription>
      </DialogHeader>

      <dl className="grid grid-cols-2 gap-x-6 gap-y-3 rounded-2xl bg-muted p-5 text-sm">
        {facts.map((f) => (
          <div key={f.label}>
            <dt className="text-muted-foreground">{f.label}</dt>
            <dd className="mt-0.5 font-medium">{f.value}</dd>
          </div>
        ))}
      </dl>

      <section aria-labelledby="exam-questions" className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 id="exam-questions" className="font-heading font-medium">
            Câu hỏi ({exam.questions.length}/{exam.maxQuestions})
          </h3>
          {status === 'DRAFT' && !editingQuestions && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={openQuestionEditor}
              className="h-8 gap-1.5 text-xs"
            >
              <Edit3 className="size-3.5" />
              {exam.questions.length === 0 ? 'Chọn / AI gợi ý câu hỏi' : 'Chỉnh sửa câu hỏi'}
            </Button>
          )}
        </div>

        {editingQuestions ? (
          <div className="space-y-3 rounded-2xl border p-4 bg-muted/20">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="text-xs font-medium">
                Đã chọn {selectedIds.size}/{exam.maxQuestions} câu hỏi
              </span>
              <div className="flex items-center gap-2">
                <Button
                  type="button"
                  variant="default"
                  size="sm"
                  disabled={aiSuggesting || bankQuestions.length === 0}
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

            {saveError && <p className="text-xs text-destructive">{saveError}</p>}

            {loadingBank ? (
              <div className="flex items-center justify-center gap-2 py-6 text-xs text-muted-foreground">
                <LoaderCircle className="size-4 animate-spin" /> Đang tải ngân hàng câu hỏi…
              </div>
            ) : bankQuestions.length === 0 ? (
              <div className="flex flex-col items-center justify-center gap-1.5 rounded-xl border border-dashed py-6 text-center text-xs text-muted-foreground">
                <BookOpen className="size-6 text-muted-foreground/60" />
                <span>Ngân hàng câu hỏi của môn này hiện đang trống.</span>
              </div>
            ) : (
              <div className="max-h-56 overflow-y-auto rounded-xl border divide-y text-xs bg-background">
                {bankQuestions.map((q) => {
                  const selected = selectedIds.has(q.questionId)
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
                        </div>
                      </div>
                    </label>
                  )
                })}
              </div>
            )}

            <div className="flex justify-end gap-2 pt-1">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => setEditingQuestions(false)}
                disabled={savingQuestions}
              >
                Hủy
              </Button>
              <Button
                type="button"
                size="sm"
                onClick={handleSaveQuestions}
                disabled={savingQuestions || selectedIds.size > exam.maxQuestions}
              >
                {savingQuestions ? 'Đang lưu…' : 'Lưu danh sách câu hỏi'}
              </Button>
            </div>
          </div>
        ) : state.status === 'loading' ? (
          <p className="mt-2 text-sm text-muted-foreground">Đang tải câu hỏi…</p>
        ) : state.status === 'error' ? (
          <p className="mt-2 text-sm text-destructive">{state.message}</p>
        ) : exam.questions.length === 0 ? (
          <div className="flex flex-col items-center justify-center gap-3 rounded-[14px] border border-dashed p-6 text-center text-sm text-muted-foreground">
            <LibraryBig className="size-6 text-muted-foreground" aria-hidden="true" />
            <p>Đề chưa có câu hỏi nào.</p>
            {status === 'DRAFT' && (
              <Button size="sm" onClick={openQuestionEditor} className="gap-1.5">
                <Sparkles className="size-3.5" />
                Chọn / AI gợi ý câu hỏi ngay
              </Button>
            )}
          </div>
        ) : (
          <ol className="mt-3 flex flex-col gap-2">
            {exam.questions.map((q, i) => (
              <li key={q.questionId} className="flex gap-3 rounded-[14px] border p-3.5 text-sm">
                <span className="font-mono text-muted-foreground">{i + 1}.</span>
                <span className="flex-1">{q.questionText}</span>
                <span className="shrink-0 text-muted-foreground">{q.questionScore} điểm</span>
              </li>
            ))}
          </ol>
        )}
      </section>

      <div className="flex justify-end">
        <Button asChild>
          <Link to={`/teacher/sessions?examId=${exam.examId}`}>
            <CalendarPlus aria-hidden="true" />
            Tạo ca thi cho đề này
          </Link>
        </Button>
      </div>
    </>
  )
}
