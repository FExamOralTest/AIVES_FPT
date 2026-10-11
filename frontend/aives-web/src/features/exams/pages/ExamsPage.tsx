import { CircleAlert, CircleCheck, FileText, Plus, RotateCw } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Button } from '@/components/ui/button'
import { formatDateTime, parseApiDate } from '@/lib/format/date'
import { useApi } from '@/lib/http/useApi'
import { cn } from '@/lib/utils'
import { examsApi } from '../api/examsApi'
import { CreateExamDialog } from '../components/CreateExamDialog'
import { ExamDetailDialog } from '../components/ExamDetailDialog'
import { EXAM_STATUS_LABEL, EXAM_STATUS_TONE } from '../lib/examStatus'
import type { Exam } from '../types'

export default function ExamsPage() {
  const { state, reload, update } = useApi(examsApi.listMine, '', 'Không tải được danh sách đề thi.')
  const [creating, setCreating] = useState(false)
  const [viewing, setViewing] = useState<Exam | null>(null)
  const [flash, setFlash] = useState<string | null>(null)

  const exams = useMemo(() => (state.status === 'ready' ? state.data : []), [state])
  const knownCourses = useMemo(() => {
    const byId = new Map<number, string | null>()
    for (const e of exams) if (!byId.has(e.courseId)) byId.set(e.courseId, e.courseName)
    return [...byId].map(([courseId, courseName]) => ({ courseId, courseName }))
  }, [exams])

  function handleCreated(exam: Exam) {
    update((list) => [exam, ...list])
    setCreating(false)
    setFlash(`Đã tạo đề "${exam.title}" (mã ${exam.examCode}).`)
    setViewing(exam)
  }

  return (
    <>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-3xl font-medium tracking-tight">Đề thi</h1>
          <p className="mt-1.5 max-w-2xl text-muted-foreground">
            Tạo đề thi cho môn được phân công, sau đó lên lịch ca thi để sinh viên tham gia.
          </p>
        </div>
        <Button
          onClick={() => {
            setFlash(null)
            setCreating(true)
          }}
        >
          <Plus aria-hidden="true" />
          Tạo đề thi
        </Button>
      </header>

      <div role="status" aria-live="polite">
        {flash && (
          <p className="mt-6 flex items-center gap-2 rounded-[14px] bg-success-soft px-4 py-3 text-sm text-success">
            <CircleCheck className="size-4 shrink-0" aria-hidden="true" />
            {flash}
          </p>
        )}
      </div>

      <section aria-label="Danh sách đề thi" className="mt-6">
        {state.status === 'loading' && (
          <ul aria-busy="true" className="grid gap-4 sm:grid-cols-2">
            {[0, 1].map((i) => (
              <li key={i} className="h-40 animate-pulse rounded-2xl bg-muted motion-reduce:animate-none" />
            ))}
          </ul>
        )}

        {state.status === 'error' && (
          <div className="flex flex-col items-center gap-3 rounded-2xl border px-6 py-14 text-center">
            <CircleAlert className="size-7 text-destructive" aria-hidden="true" />
            <p className="max-w-sm text-muted-foreground">{state.message}</p>
            <Button variant="secondary" size="sm" onClick={reload}>
              <RotateCw aria-hidden="true" />
              Thử lại
            </Button>
          </div>
        )}

        {state.status === 'ready' && exams.length === 0 && (
          <div className="flex flex-col items-center gap-3 rounded-2xl border-2 border-dashed px-6 py-14 text-center">
            <FileText className="size-8 text-muted-foreground" aria-hidden="true" />
            <p className="font-heading text-lg font-medium">Chưa có đề thi nào</p>
            <p className="max-w-sm text-sm text-muted-foreground">Tạo đề thi đầu tiên để có thể lên lịch ca thi.</p>
            <Button onClick={() => setCreating(true)}>
              <Plus aria-hidden="true" />
              Tạo đề thi
            </Button>
          </div>
        )}

        {state.status === 'ready' && exams.length > 0 && (
          <ul className="grid gap-4 sm:grid-cols-2">
            {exams.map((exam) => (
              <li key={exam.examId}>
                <ExamCard exam={exam} onOpen={() => setViewing(exam)} />
              </li>
            ))}
          </ul>
        )}
      </section>

      <CreateExamDialog
        open={creating}
        knownCourses={knownCourses}
        onClose={() => setCreating(false)}
        onCreated={handleCreated}
      />
      <ExamDetailDialog exam={viewing} onClose={() => setViewing(null)} />
    </>
  )
}

function ExamCard({ exam, onOpen }: { exam: Exam; onOpen: () => void }) {
  const status = exam.examStatus ?? 'DRAFT'
  return (
    <button
      type="button"
      onClick={onOpen}
      className="flex h-full w-full flex-col gap-3 rounded-2xl border p-5 text-left transition-colors hover:border-brand/40 hover:bg-brand-soft/30"
    >
      <div className="flex w-full items-start justify-between gap-3">
        <code className="font-mono text-[13px] text-muted-foreground">{exam.examCode}</code>
        <span className={cn('shrink-0 rounded-full px-2.5 py-1 text-xs font-medium', EXAM_STATUS_TONE[status])}>
          {EXAM_STATUS_LABEL[status]}
        </span>
      </div>
      <div className="flex-1">
        <h2 className="font-heading font-medium">{exam.title}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{exam.courseName ?? `Môn #${exam.courseId}`}</p>
      </div>
      <p className="text-sm text-muted-foreground">
        {exam.durationMinutes} phút · tối đa {exam.maxQuestions} câu · {exam.questions.length} câu đã chọn
        {exam.createdAt && <> · tạo {formatDateTime(parseApiDate(exam.createdAt))}</>}
      </p>
    </button>
  )
}
