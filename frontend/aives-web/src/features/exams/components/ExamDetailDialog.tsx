import { CalendarPlus, LibraryBig } from 'lucide-react'
import { Link } from 'react-router-dom'
import { CopyButton } from '@/components/common/CopyButton'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { formatDateTime, parseApiDate } from '@/lib/format/date'
import { useApi } from '@/lib/http/useApi'
import { cn } from '@/lib/utils'
import { examsApi } from '../api/examsApi'
import { EXAM_STATUS_LABEL, EXAM_STATUS_TONE } from '../lib/examStatus'
import type { Exam } from '../types'

export function ExamDetailDialog({ exam, onClose }: { exam: Exam | null; onClose: () => void }) {
  return (
    <Dialog open={exam !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-2xl">{exam && <ExamDetail key={exam.examId} summary={exam} />}</DialogContent>
    </Dialog>
  )
}

function ExamDetail({ summary }: { summary: Exam }) {
  // The list response has no question texts, so load the full exam.
  const { state } = useApi(() => examsApi.get(summary.examId), summary.examId, 'Không tải được chi tiết đề thi.')
  const exam = state.status === 'ready' ? state.data : summary
  const status = exam.examStatus ?? 'DRAFT'

  const facts = [
    { label: 'Môn học', value: exam.courseName ? `${exam.courseName} (#${exam.courseId})` : `#${exam.courseId}` },
    { label: 'Thời lượng', value: `${exam.durationMinutes} phút` },
    { label: 'Số câu tối đa', value: String(exam.maxQuestions) },
    { label: 'Tạo lúc', value: exam.createdAt ? formatDateTime(parseApiDate(exam.createdAt)) : '—' },
  ]

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

      <section aria-labelledby="exam-questions">
        <h3 id="exam-questions" className="font-heading font-medium">
          Câu hỏi ({exam.questions.length})
        </h3>
        {state.status === 'loading' ? (
          <p className="mt-2 text-sm text-muted-foreground">Đang tải câu hỏi…</p>
        ) : state.status === 'error' ? (
          <p className="mt-2 text-sm text-destructive">{state.message}</p>
        ) : exam.questions.length === 0 ? (
          <p className="mt-2 flex items-start gap-2 rounded-[14px] border border-dashed p-4 text-sm text-muted-foreground">
            <LibraryBig className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            Đề chưa có câu hỏi. Chọn câu hỏi từ ngân hàng đang hoàn thiện (chờ AI sinh câu hỏi).
          </p>
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
