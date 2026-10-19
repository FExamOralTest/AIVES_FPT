import {
  Award,
  CheckCircle2,
  Save,
  Sparkles,
  Volume2,
  X,
} from 'lucide-react'
import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { toApiError } from '@/lib/http/apiError'
import { teacherResultsApi } from '../api/teacherResultsApi'
import type { ResultForReviewDto } from '../types'

interface ResultReviewModalProps {
  result: ResultForReviewDto
  onClose: () => void
  onUpdated: (updated: ResultForReviewDto) => void
}

export function ResultReviewModal({ result, onClose, onUpdated }: ResultReviewModalProps) {
  const [overallScore, setOverallScore] = useState<number>(
    result.finalScore ?? result.aiScore ?? 0,
  )
  const [overallFeedback, setOverallFeedback] = useState<string>(
    result.finalFeedback ?? result.aiFeedback ?? '',
  )

  const [answerScores, setAnswerScores] = useState<Record<number, number>>(
    () => {
      const init: Record<number, number> = {}
      result.answers.forEach((a) => {
        init[a.answerId] = a.finalScore ?? a.aiScore ?? 0
      })
      return init
    },
  )

  const [answerComments, setAnswerComments] = useState<Record<number, string>>(
    () => {
      const init: Record<number, string> = {}
      result.answers.forEach((a) => {
        init[a.answerId] = a.teacherComment ?? ''
      })
      return init
    },
  )

  const [isSaving, setIsSaving] = useState(false)
  const [isPublishing, setIsPublishing] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  async function handleSaveReview() {
    setIsSaving(true)
    setErrorMessage(null)
    try {
      const payload = {
        finalScore: overallScore,
        finalFeedback: overallFeedback,
        answerReviews: result.answers.map((a) => ({
          answerId: a.answerId,
          finalScore: answerScores[a.answerId],
          teacherComment: answerComments[a.answerId],
        })),
      }
      const updated = await teacherResultsApi.review(result.resultId, payload)
      onUpdated(updated)
      return updated
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
      return null
    } finally {
      setIsSaving(false)
    }
  }

  async function handlePublish() {
    setIsPublishing(true)
    setErrorMessage(null)
    try {
      // First ensure reviews are saved
      await handleSaveReview()
      const published = await teacherResultsApi.publish(result.resultId)
      onUpdated(published)
      onClose()
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
    } finally {
      setIsPublishing(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm overflow-y-auto">
      <div className="relative my-8 flex max-h-[90vh] w-full max-w-4xl flex-col rounded-3xl border bg-card shadow-2xl overflow-hidden">
        {/* Header */}
        <header className="flex items-center justify-between border-b px-6 py-5 sm:px-8">
          <div>
            <span className="text-xs font-semibold uppercase tracking-wider text-brand">
              Chấm thi & Đánh giá
            </span>
            <h2 className="mt-0.5 font-heading text-xl font-medium sm:text-2xl">
              {result.studentName} ({result.studentEmail})
            </h2>
            <p className="text-xs text-muted-foreground">{result.sessionName}</p>
          </div>

          <Button variant="ghost" size="icon" onClick={onClose} className="rounded-full">
            <X className="size-5" />
          </Button>
        </header>

        {/* Scrollable Body */}
        <div className="flex-1 overflow-y-auto p-6 sm:p-8 space-y-6">
          {errorMessage && (
            <div className="rounded-2xl bg-destructive/10 p-4 text-sm text-destructive font-medium">
              {errorMessage}
            </div>
          )}

          {/* Overall Review Summary */}
          <section className="rounded-2xl border bg-gradient-to-br from-brand-soft/30 via-background to-card p-6">
            <h3 className="font-heading text-base font-semibold text-foreground flex items-center gap-2">
              <Award className="size-5 text-brand" />
              Điểm tổng kết & Nhận xét chung
            </h3>

            <div className="mt-4 grid gap-4 sm:grid-cols-[10rem_1fr]">
              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                  Điểm số (0 - 10)
                </label>
                <input
                  type="number"
                  step="0.1"
                  min="0"
                  max="10"
                  value={overallScore}
                  onChange={(e) => setOverallScore(parseFloat(e.target.value) || 0)}
                  className="mt-1.5 h-12 w-full rounded-xl border border-input bg-background px-4 font-heading text-2xl font-bold text-brand outline-none focus:border-brand focus:ring-4 focus:ring-brand/15"
                />
                {result.aiScore != null && (
                  <p className="mt-1.5 text-xs text-muted-foreground">
                    Điểm AI đề xuất: <span className="font-semibold">{result.aiScore.toFixed(1)}</span>
                  </p>
                )}
              </div>

              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                  Nhận xét tổng thể của giảng viên
                </label>
                <textarea
                  rows={3}
                  value={overallFeedback}
                  onChange={(e) => setOverallFeedback(e.target.value)}
                  placeholder="Nhập nhận xét tổng quan cho sinh viên..."
                  className="mt-1.5 w-full rounded-xl border border-input bg-background p-3 text-sm outline-none focus:border-brand focus:ring-4 focus:ring-brand/15 resize-none"
                />
              </div>
            </div>
          </section>

          {/* Individual Questions List */}
          <section className="space-y-4">
            <h3 className="font-heading text-base font-semibold">
              Chi tiết các câu hỏi ({result.answers.length})
            </h3>

            {result.answers.map((answer, index) => (
              <div
                key={answer.answerId}
                className="rounded-2xl border bg-card p-5 space-y-4 shadow-sm"
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="font-semibold text-sm text-brand">
                    Câu {index + 1}
                    {answer.isFollowUp && (
                      <Badge variant="secondary" className="ml-2 text-xs bg-amber-100 text-amber-800">
                        Câu hỏi phụ
                      </Badge>
                    )}
                    {answer.status && (
                      <Badge
                        variant={answer.status === 'ERROR' ? 'destructive' : 'secondary'}
                        className={`text-[10px] uppercase font-bold ${
                          answer.status === 'EVALUATED'
                            ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300'
                            : ''
                        }`}
                      >
                        {answer.status === 'EVALUATED'
                          ? 'Đã chấm AI'
                          : answer.status === 'ERROR'
                          ? 'Lỗi xử lý'
                          : 'Đang xử lý'}
                      </Badge>
                    )}
                  </span>

                  {answer.aiScore != null && (
                    <span className="flex items-center gap-1 text-xs text-muted-foreground">
                      <Sparkles className="size-3.5 text-amber-500" />
                      AI đề xuất: <b>{answer.aiScore.toFixed(1)} điểm</b>
                    </span>
                  )}
                </div>

                <p className="font-heading text-sm font-medium text-foreground">
                  {answer.questionText}
                </p>

                {/* Error Banner & Retry Button if ERROR */}
                {answer.status === 'ERROR' && (
                  <div className="flex items-center justify-between rounded-xl bg-destructive/10 border border-destructive/20 p-3 text-xs text-destructive">
                    <div>
                      <p className="font-semibold">Xảy ra lỗi khi phân tích câu trả lời:</p>
                      <p className="opacity-90">{answer.errorMessage || 'Không thể nhận diện giọng nói hoặc phân tích câu trả lời.'}</p>
                    </div>
                    <Button
                      size="sm"
                      variant="outline"
                      className="border-destructive/40 text-destructive hover:bg-destructive hover:text-white shrink-0 text-xs h-8"
                      onClick={async () => {
                        try {
                          await teacherResultsApi.retryAnswer(answer.answerId, 'Examiner requested retry')
                          alert(`Đã gửi yêu cầu xử lý lại câu ${index + 1}. Vui lòng làm mới trang sau vài giây.`)
                        } catch (e) {
                          alert('Không thể thử lại: ' + (e as Error).message)
                        }
                      }}
                    >
                      Thử chấm lại
                    </Button>
                  </div>
                )}

                {/* Student Answer & Audio Player */}
                <div className="rounded-xl bg-muted/60 p-4 text-xs space-y-2">
                  <span className="flex items-center gap-1.5 font-semibold text-muted-foreground">
                    <Volume2 className="size-3.5 text-brand" />
                    Bản ghi câu trả lời của sinh viên:
                  </span>
                  <p className="text-foreground text-sm italic">
                    "{answer.studentAnswer || answer.transcript || '(Không có nội dung)'}"
                  </p>

                  {answer.audioUrl && (
                    <div className="pt-2">
                      <audio controls src={answer.audioUrl} className="h-9 w-full max-w-sm" />
                    </div>
                  )}

                  {answer.aiFeedback && (
                    <p className="border-t pt-2 text-muted-foreground">
                      <span className="font-semibold text-foreground">AI Phân tích: </span>
                      {answer.aiFeedback}
                    </p>
                  )}
                </div>

                {/* Score & Teacher Note per Question */}
                <div className="grid gap-3 sm:grid-cols-[8rem_1fr] border-t pt-3">
                  <div>
                    <label className="block text-xs font-semibold text-muted-foreground">
                      Điểm câu này
                    </label>
                    <input
                      type="number"
                      step="0.1"
                      value={answerScores[answer.answerId] ?? 0}
                      onChange={(e) =>
                        setAnswerScores((prev) => ({
                          ...prev,
                          [answer.answerId]: parseFloat(e.target.value) || 0,
                        }))
                      }
                      className="mt-1 h-10 w-full rounded-lg border border-input bg-background px-3 font-semibold text-sm outline-none focus:border-brand"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-muted-foreground">
                      Ghi chú của giảng viên cho câu này
                    </label>
                    <input
                      type="text"
                      value={answerComments[answer.answerId] ?? ''}
                      onChange={(e) =>
                        setAnswerComments((prev) => ({
                          ...prev,
                          [answer.answerId]: e.target.value,
                        }))
                      }
                      placeholder="Ghi chú nhận xét..."
                      className="mt-1 h-10 w-full rounded-lg border border-input bg-background px-3 text-sm outline-none focus:border-brand"
                    />
                  </div>
                </div>
              </div>
            ))}
          </section>
        </div>

        {/* Footer Actions */}
        <footer className="flex flex-wrap items-center justify-between gap-3 border-t bg-muted/30 px-6 py-4 sm:px-8">
          <Button variant="ghost" onClick={onClose}>
            Đóng
          </Button>

          <div className="flex gap-3">
            <Button
              variant="outline"
              onClick={handleSaveReview}
              disabled={isSaving || isPublishing}
              className="gap-2"
            >
              <Save className="size-4" />
              {isSaving ? 'Đang lưu…' : 'Lưu bản nháp'}
            </Button>

            <Button
              onClick={handlePublish}
              disabled={isSaving || isPublishing}
              className="gap-2 bg-emerald-600 hover:bg-emerald-700 text-white"
            >
              <CheckCircle2 className="size-4" />
              {isPublishing ? 'Đang công bố…' : 'Phê duyệt & Công bố kết quả'}
            </Button>
          </div>
        </footer>
      </div>
    </div>
  )
}
