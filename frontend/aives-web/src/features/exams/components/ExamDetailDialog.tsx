import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { CopyButton } from '@/components/common/CopyButton'
import { useApi } from '@/lib/http/useApi'
import { toApiError } from '@/lib/http/apiError'
import { examsApi } from '../api/examsApi'
import { ExamForm } from './ExamForm'
import { EXAM_STATUS_LABEL } from '../lib/examStatus'
import type { Exam } from '../types'

interface Props {
  exam: Exam | null
  onClose: () => void
  onChanged: (exam: Exam) => void
  onDeleted: (examId: number) => void
}

export function ExamDetailDialog({ exam, onClose, onChanged, onDeleted }: Props) {
  const [pending, setPending] = useState(false)
  return <Dialog open={exam !== null} onOpenChange={(value) => !value && !pending && onClose()}>
    <DialogContent className="max-w-2xl">
      {exam && <ExamDetail key={exam.examId} examId={exam.examId} onChanged={onChanged} onDeleted={onDeleted} onPending={setPending} />}
    </DialogContent>
  </Dialog>
}

function ExamDetail({ examId, onChanged, onDeleted, onPending }: { examId: number; onChanged: Props['onChanged']; onDeleted: Props['onDeleted']; onPending: (pending: boolean) => void }) {
  const { state, update, reload } = useApi(() => examsApi.get(examId), examId)
  const [editing, setEditing] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function remove() {
    if (pending) return
    setPending(true); onPending(true); setError(null)
    try { await examsApi.delete(examId); onDeleted(examId) }
    catch (err) { setError(toApiError(err).message) }
    finally { setPending(false); onPending(false) }
  }

  const exam = state.status === 'ready' ? state.data : null
  return <>
    <DialogHeader><DialogTitle>{editing ? 'Sửa đề thi nháp' : exam?.title ?? 'Chi tiết đề thi'}</DialogTitle><DialogDescription>{exam ? `${exam.courseName ?? `Môn #${exam.courseId}`} · ${EXAM_STATUS_LABEL[exam.examStatus ?? 'DRAFT']}` : 'Đang tải thông tin đề thi.'}</DialogDescription></DialogHeader>
    {state.status === 'loading' ? <p role="status">Đang tải đề thi…</p> : state.status === 'error' ? <p role="alert">{state.message} <Button onClick={reload}>Thử lại</Button></p> : exam && (editing ? <ExamForm exam={exam} onSubmit={(input) => examsApi.update(examId, input)} onPending={onPending} onCancel={() => setEditing(false)} onSaved={(saved) => { update(() => saved); onChanged(saved); setEditing(false) }} /> : <>
      <div className="flex items-center gap-2 text-sm"><code>{exam.examCode}</code><CopyButton value={exam.examCode} label="Sao chép mã đề" /></div>
      <p className="text-sm">{exam.durationMinutes} phút · {exam.questions.length}/{exam.maxQuestions} câu hỏi</p>
      {exam.canEdit ? <div className="flex gap-2"><Button variant="outline" onClick={() => setEditing(true)}>Sửa đề thi</Button><Button variant="destructive" onClick={() => { setDeleting(true); setError(null) }}>Xóa đề nháp</Button></div> : <p className="text-sm text-muted-foreground">Chỉ có thể sửa hoặc xóa đề nháp chưa có ca thi.</p>}
      {!exam.questions.length ? <p className="rounded-xl border border-dashed p-5 text-sm">Đề chưa có câu hỏi. Thêm câu hỏi trước khi tạo ca thi.</p> : <ol className="space-y-2">{exam.questions.map((question, index) => <li key={question.questionId} className="rounded-xl border p-3 text-sm"><span>{index + 1}. {question.questionText}</span><p className="mt-1 text-muted-foreground">{question.questionScore} điểm</p></li>)}</ol>}
      {exam.questions.length > 0 && <Button asChild><Link to={`/teacher/sessions?examId=${exam.examId}`}>Tạo ca thi cho đề này</Link></Button>}
    </>)}
    <ConfirmDialog open={deleting} title="Xóa đề thi nháp?" description={`Xóa đề ${exam?.title ?? ''} và các liên kết câu hỏi của đề? Ngân hàng câu hỏi vẫn được giữ lại.`} pending={pending} error={error} onClose={() => setDeleting(false)} onConfirm={() => void remove()} />
  </>
}
