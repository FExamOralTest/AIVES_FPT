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
  const maxTotalScore = result.aiAnalysis?.maxTotalScore
    ?? result.answers.reduce((total, answer) => total + (answer.maxScore ?? 10), 0)
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
  // Per-answer outcome of "Retry scoring", shown under that answer instead of a blocking alert().
  const [retryNotes, setRetryNotes] = useState<Record<number, { ok: boolean; text: string }>>({})

  function validateReviewScores() {
    if (!Number.isFinite(overallScore) || overallScore < 0 || overallScore > maxTotalScore) {
      setErrorMessage(`The overall score must be between 0 and ${maxTotalScore}.`)
      return false
    }

    const invalidAnswer = result.answers.find((answer) => {
      const score = answerScores[answer.answerId]
      const maxScore = answer.maxScore ?? 10
      return !Number.isFinite(score) || score < 0 || score > maxScore
    })
    if (invalidAnswer) {
      setErrorMessage(
        `The score for question ${result.answers.indexOf(invalidAnswer) + 1} must be between 0 and ${invalidAnswer.maxScore ?? 10}.`,
      )
      return false
    }

    return true
  }

  async function handleRetry(answerId: number, position: number) {
    try {
      await teacherResultsApi.retryAnswer(answerId, 'Examiner requested retry')
      setRetryNotes((prev) => ({ ...prev, [answerId]: { ok: true, text: `Question ${position} was sent for re-scoring. Refresh in a few seconds.` } }))
    } catch (err) {
      setRetryNotes((prev) => ({ ...prev, [answerId]: { ok: false, text: `Could not retry: ${toApiError(err).message}` } }))
    }
  }

  async function handleSaveReview() {
    if (!validateReviewScores()) return null

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
      // Save the edited scores first; publishing after a failed save would publish the old scores.
      if (!(await handleSaveReview())) return
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
      <div role="dialog" aria-modal="true" aria-labelledby="result-review-title" className="relative my-8 flex max-h-[90vh] w-full max-w-4xl flex-col rounded-3xl border bg-card shadow-2xl overflow-hidden">
        {/* Header */}
        <header className="flex items-center justify-between border-b px-6 py-5 sm:px-8">
          <div>
            <span className="text-xs font-semibold uppercase tracking-wider text-brand">
              Review & score
            </span>
            <h2 id="result-review-title" className="mt-0.5 font-heading text-xl font-medium sm:text-2xl">
              {result.studentName} ({result.studentEmail})
            </h2>
            <p className="text-xs text-muted-foreground">{result.sessionName}</p>
          </div>

          <Button variant="ghost" size="icon" onClick={onClose} className="rounded-full" aria-label="Close">
            <X className="size-5" aria-hidden="true" />
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
              Final score & overall feedback
            </h3>

            <div className="mt-4 grid gap-4 sm:grid-cols-[10rem_1fr]">
              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                  Score (0 - {maxTotalScore})
                </label>
                <input
                  type="number"
                  step="0.1"
                  min="0"
                  max={maxTotalScore}
                  value={overallScore}
                  onChange={(e) => setOverallScore(parseFloat(e.target.value) || 0)}
                  className="mt-1.5 h-12 w-full rounded-xl border border-input bg-background px-4 font-heading text-2xl font-bold text-brand outline-none focus:border-brand focus:ring-4 focus:ring-brand/15"
                />
                {result.aiScore != null && (
                  <p className="mt-1.5 text-xs text-muted-foreground">
                    AI-suggested score: <span className="font-semibold">{result.aiScore.toFixed(1)}</span>
                  </p>
                )}
              </div>

              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                  Examiner's overall feedback
                </label>
                <textarea
                  rows={3}
                  value={overallFeedback}
                  onChange={(e) => setOverallFeedback(e.target.value)}
                  placeholder="Write overall feedback for the student..."
                  className="mt-1.5 w-full rounded-xl border border-input bg-background p-3 text-sm outline-none focus:border-brand focus:ring-4 focus:ring-brand/15 resize-none"
                />
              </div>
            </div>
          </section>

          {/* Individual Questions List */}
          <section className="space-y-4">
            <h3 className="font-heading text-base font-semibold">
              Answers ({result.answers.length})
            </h3>

            {result.answers.map((answer, index) => (
              <div
                key={answer.answerId}
                className="rounded-2xl border bg-card p-5 space-y-4 shadow-sm"
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="font-semibold text-sm text-brand">
                    Question {index + 1}
                    {answer.isFollowUp && (
                      <Badge variant="secondary" className="ml-2 text-xs bg-amber-100 text-amber-800">
                        Follow-up
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
                          ? 'AI scored'
                          : answer.status === 'ERROR'
                          ? 'Processing error'
                          : 'Processing'}
                      </Badge>
                    )}
                  </span>

                  {answer.aiScore != null && (
                    <span className="flex items-center gap-1 text-xs text-muted-foreground">
                      <Sparkles className="size-3.5 text-amber-500" />
                      AI suggests: <b>{answer.aiScore.toFixed(1)} pts</b>
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
                      <p className="font-semibold">The answer could not be analysed:</p>
                      <p className="opacity-90">{answer.errorMessage || 'Speech recognition or answer analysis failed.'}</p>
                    </div>
                    <Button
                      size="sm"
                      variant="outline"
                      className="border-destructive/40 text-destructive hover:bg-destructive hover:text-white shrink-0 text-xs h-8"
                      onClick={() => void handleRetry(answer.answerId, index + 1)}
                    >
                      Retry scoring
                    </Button>
                  </div>
                )}
                {retryNotes[answer.answerId] && (
                  <p role="status" className={retryNotes[answer.answerId].ok ? 'text-xs text-success' : 'text-xs text-destructive'}>
                    {retryNotes[answer.answerId].text}
                  </p>
                )}

                {/* Student Answer & Audio Player */}
                <div className="rounded-xl bg-muted/60 p-4 text-xs space-y-2">
                  <span className="flex items-center gap-1.5 font-semibold text-muted-foreground">
                    <Volume2 className="size-3.5 text-brand" />
                    Student's answer:
                  </span>
                  <p className="text-foreground text-sm italic">
                    "{answer.studentAnswer || answer.transcript || '(No content)'}"
                  </p>

                  {answer.audioUrl && (
                    <div className="pt-2">
                      <audio controls src={answer.audioUrl} className="h-9 w-full max-w-sm" />
                    </div>
                  )}

                  {answer.aiFeedback && (
                    <p className="border-t pt-2 text-muted-foreground">
                      <span className="font-semibold text-foreground">AI analysis: </span>
                      {answer.aiFeedback}
                    </p>
                  )}
                </div>

                {/* Score & Teacher Note per Question */}
                <div className="grid gap-3 sm:grid-cols-[8rem_1fr] border-t pt-3">
                  <div>
                    <label className="block text-xs font-semibold text-muted-foreground">
                      Score for this question (0 - {answer.maxScore ?? 10})
                    </label>
                    <input
                      type="number"
                      step="0.1"
                      min="0"
                      max={answer.maxScore ?? 10}
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
                      Examiner's note for this question
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
                      placeholder="Add a note..."
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
            Close
          </Button>

          <div className="flex gap-3">
            <Button
              variant="outline"
              onClick={handleSaveReview}
              disabled={isSaving || isPublishing}
              className="gap-2"
            >
              <Save className="size-4" />
              {isSaving ? 'Saving…' : 'Save draft'}
            </Button>

            <Button
              onClick={handlePublish}
              disabled={isSaving || isPublishing}
              className="gap-2 bg-emerald-600 hover:bg-emerald-700 text-white"
            >
              <CheckCircle2 className="size-4" />
              {isPublishing ? 'Publishing…' : 'Approve & publish result'}
            </Button>
          </div>
        </footer>
      </div>
    </div>
  )
}
