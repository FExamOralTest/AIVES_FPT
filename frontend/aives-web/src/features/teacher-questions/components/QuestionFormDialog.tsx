import { useEffect, useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { toApiError } from '@/lib/http/apiError'
import { teacherQuestionsApi } from '../api/teacherQuestionsApi'
import type { WriteQuestionInput } from '../types'

const fieldClass = 'mt-1 w-full rounded-xl border bg-background px-3 py-2 text-sm'
const emptyQuestion: WriteQuestionInput = {
  questionText: '', topic: '', sampleAnswer: '', learningOutcome: '', level: 1,
  maxScore: 1, isFollowUpAllowed: true, rubric: [{ description: '', maxScore: 1 }],
}

interface Props {
  courseId: number
  questionId?: number
  readOnly: boolean
  onClose: () => void
  onSaved: () => void
}

export function QuestionFormDialog({ courseId, questionId, readOnly, onClose, onSaved }: Props) {
  const [form, setForm] = useState<WriteQuestionInput>(emptyQuestion)
  const [loadFailed, setLoadFailed] = useState(false)
  const [loading, setLoading] = useState(questionId !== undefined)
  const [pending, setPending] = useState(false)
  const [inUse, setInUse] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (questionId === undefined) return
    let active = true
    teacherQuestionsApi.get(courseId, questionId).then((question) => {
      if (!active) return
      setForm({
        questionText: question.questionText, topic: question.topic ?? '',
        sampleAnswer: question.sampleAnswer ?? '', learningOutcome: question.learningOutcome ?? '',
        level: question.level, maxScore: question.maxScore ?? 1,
        isFollowUpAllowed: question.isFollowUpAllowed ?? true,
        rubric: question.rubric?.length ? question.rubric.map((criterion) => ({ ...criterion })) : [{ description: '', maxScore: question.maxScore ?? 1 }],
      })
      setInUse(Boolean(question.isInUse))
      setLoading(false)
    }).catch((err) => {
      if (active) { setError(toApiError(err).message); setLoadFailed(true); setLoading(false) }
    })
    return () => { active = false }
  }, [courseId, questionId])

  const locked = readOnly || inUse || loadFailed
  const total = form.rubric.reduce((sum, criterion) => sum + Math.round(criterion.maxScore * 10000), 0) / 10000

  function set<K extends keyof WriteQuestionInput>(key: K, value: WriteQuestionInput[K]) {
    setForm((previous) => ({ ...previous, [key]: value }))
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (locked || loading || pending) return
    const normalized = form.rubric.map((criterion) => criterion.description.trim().toLocaleLowerCase().replace(/[^\p{L}\p{N}]+/gu, ' ').trim())
    if (!form.questionText.trim() || !form.sampleAnswer.trim() || !form.learningOutcome.trim()) {
      setError('Nhập câu hỏi, đáp án mẫu và chuẩn đầu ra.'); return
    }
    if (form.rubric.some((criterion) => !criterion.description.trim() || !Number.isFinite(criterion.maxScore) || criterion.maxScore <= 0)
      || new Set(normalized).size !== normalized.length || !Number.isFinite(form.maxScore)
      || Math.abs(total - form.maxScore) > 0.000001) {
      setError('Tiêu chí phải khác nhau, có điểm dương và tổng điểm bằng điểm tối đa.'); return
    }
    setPending(true)
    setError(null)
    try {
      const input = { ...form, questionText: form.questionText.trim(), topic: form.topic.trim(), sampleAnswer: form.sampleAnswer.trim(), learningOutcome: form.learningOutcome.trim() }
      if (questionId === undefined) await teacherQuestionsApi.create(courseId, input)
      else await teacherQuestionsApi.update(courseId, questionId, input)
      onSaved()
    } catch (err) { setError(toApiError(err).message) }
    finally { setPending(false) }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && !pending && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{readOnly ? 'Chi tiết câu hỏi' : questionId === undefined ? 'Thêm câu hỏi' : 'Sửa câu hỏi'}</DialogTitle>
          <DialogDescription>{inUse ? 'Câu hỏi đã được sử dụng. Tạo câu hỏi mới để thay đổi nội dung.' : 'Đáp án mẫu, chuẩn đầu ra và rubric được dùng để chấm bài.'}</DialogDescription>
        </DialogHeader>
        {loading ? <p role="status">Đang tải câu hỏi…</p> : (
          <form onSubmit={submit} className="space-y-4">
            {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
            <fieldset disabled={locked || pending} className="space-y-4">
              <label className="block text-sm">Câu hỏi<textarea aria-label="Câu hỏi" required maxLength={10000} rows={3} className={fieldClass} value={form.questionText} onChange={(event) => set('questionText', event.target.value)} /></label>
              <label className="block text-sm">Chủ đề<input maxLength={1000} className={fieldClass} value={form.topic} onChange={(event) => set('topic', event.target.value)} /></label>
              <label className="block text-sm">Đáp án mẫu<textarea aria-label="Đáp án mẫu" required maxLength={10000} rows={3} className={fieldClass} value={form.sampleAnswer} onChange={(event) => set('sampleAnswer', event.target.value)} /></label>
              <label className="block text-sm">Chuẩn đầu ra<textarea aria-label="Chuẩn đầu ra" required maxLength={2000} rows={2} className={fieldClass} value={form.learningOutcome} onChange={(event) => set('learningOutcome', event.target.value)} /></label>
              <div className="grid grid-cols-2 gap-4">
                <label className="text-sm">Độ khó<select className={fieldClass} value={form.level} onChange={(event) => set('level', Number(event.target.value))}><option value={1}>Cơ bản</option><option value={2}>Trung bình</option><option value={3}>Nâng cao</option></select></label>
                <label className="text-sm">Điểm tối đa<input required type="number" min="0.01" max="100" step="0.0001" className={fieldClass} value={form.maxScore} onChange={(event) => set('maxScore', Number(event.target.value))} /></label>
              </div>
              <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isFollowUpAllowed} onChange={(event) => set('isFollowUpAllowed', event.target.checked)} />Cho phép câu hỏi phụ</label>
              <div className="space-y-3 rounded-xl border p-3">
                <p className="text-sm font-medium">Rubric · Tổng điểm: {total}</p>
                {form.rubric.map((criterion, index) => (
                  <div key={index} className="grid gap-2 sm:grid-cols-[1fr_6rem_auto]">
                    <label className="text-xs">Tiêu chí {index + 1}<textarea aria-label={`Tiêu chí ${index + 1}`} required maxLength={2000} className={fieldClass} value={criterion.description} onChange={(event) => set('rubric', form.rubric.map((item, position) => position === index ? { ...item, description: event.target.value } : item))} /></label>
                    <label className="text-xs">Điểm<input required type="number" min="0.0001" max="100" step="0.0001" className={fieldClass} value={criterion.maxScore} onChange={(event) => set('rubric', form.rubric.map((item, position) => position === index ? { ...item, maxScore: Number(event.target.value) } : item))} /></label>
                    {!locked && <Button type="button" variant="ghost" disabled={form.rubric.length === 1} onClick={() => set('rubric', form.rubric.filter((_, position) => position !== index))} aria-label={`Xóa tiêu chí ${index + 1}`}>Xóa</Button>}
                  </div>
                ))}
                {!locked && <Button type="button" variant="outline" disabled={form.rubric.length >= 20} onClick={() => set('rubric', [...form.rubric, { description: '', maxScore: 1 }])}>Thêm tiêu chí</Button>}
              </div>
            </fieldset>
            <DialogFooter>
              <Button type="button" variant="outline" disabled={pending} onClick={onClose}>Đóng</Button>
              {!locked && <Button type="submit" disabled={pending}>{pending ? 'Đang lưu…' : 'Lưu câu hỏi'}</Button>}
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}
