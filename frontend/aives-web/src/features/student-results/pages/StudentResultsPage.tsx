import {
  AlertCircle,
  Award,
  Calendar,
  CheckCircle2,
  Clock,
  HelpCircle,
  MessageSquare,
  Search,
  Volume2,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { formatDateTime, parseApiDate } from '@/lib/format/date'
import { toApiError } from '@/lib/http/apiError'
import { studentResultsApi } from '../api/studentResultsApi'
import type { StudentResultDto } from '../types'

export default function StudentResultsPage() {
  const [searchParams] = useSearchParams()
  const initialSession = searchParams.get('sessionId') ?? ''

  const [sessionIdInput, setSessionIdInput] = useState(initialSession)
  const [result, setResult] = useState<StudentResultDto | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    if (initialSession) {
      fetchResult(Number(initialSession))
    }
  }, [initialSession])

  async function fetchResult(id: number) {
    if (!id || isNaN(id)) {
      setErrorMessage('Please enter a valid session ID.')
      return
    }
    setIsLoading(true)
    setErrorMessage(null)
    try {
      const data = await studentResultsApi.get(id)
      setResult(data)
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
      setResult(null)
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="mx-auto max-w-4xl py-6">
      <header className="mb-8">
        <h1 className="font-heading text-3xl font-medium tracking-tight sm:text-4xl">
          Viva exam results
        </h1>
        <p className="mt-2 text-muted-foreground">
          See your official score, the examiner's feedback and exam details once they are published.
        </p>

        {/* Search Session Bar */}
        <div className="mt-6 flex max-w-md gap-3">
          <div className="relative flex-1">
            <Search className="absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <input
              type="number"
              value={sessionIdInput}
              onChange={(e) => setSessionIdInput(e.target.value)}
              placeholder="Enter the session ID..."
              className="h-11 w-full rounded-xl border border-input bg-background pl-10 pr-4 text-sm outline-none transition-colors focus:border-brand focus:ring-4 focus:ring-brand/15"
            />
          </div>
          <Button
            onClick={() => fetchResult(Number(sessionIdInput))}
            disabled={isLoading || !sessionIdInput}
            className="h-11 px-5"
          >
            {isLoading ? 'Loading…' : 'Look up'}
          </Button>
        </div>
      </header>

      {errorMessage && (
        <Alert variant="destructive" className="mb-6 rounded-2xl">
          <AlertCircle className="size-4" />
          <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
      )}

      {result && (
        <div className="space-y-6">
          {/* Top Session Overview Card */}
          <section className="overflow-hidden rounded-3xl border bg-card p-6 shadow-sm sm:p-8">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <span className="text-xs font-semibold uppercase tracking-wider text-brand">
                  {result.examTitle}
                </span>
                <h2 className="mt-1 font-heading text-2xl font-medium text-foreground sm:text-3xl">
                  {result.sessionName}
                </h2>
              </div>

              <div>
                {result.isPublished ? (
                  <Badge variant="default" className="bg-emerald-600 hover:bg-emerald-700 text-white gap-1 px-3 py-1">
                    <CheckCircle2 className="size-3.5" />
                    Officially published
                  </Badge>
                ) : (
                  <Badge variant="secondary" className="gap-1 px-3 py-1 bg-amber-100 text-amber-900 dark:bg-amber-950/60 dark:text-amber-300">
                    <Clock className="size-3.5" />
                    Awaiting examiner review
                  </Badge>
                )}
              </div>
            </div>

            {/* Score Banner if published */}
            {result.isPublished ? (
              <div className="mt-8 grid gap-6 rounded-2xl border bg-gradient-to-br from-brand-soft/40 via-card to-background p-6 sm:grid-cols-[auto_1fr] sm:items-center">
                <div className="flex flex-col items-center justify-center rounded-2xl border bg-card px-8 py-6 shadow-sm">
                  <Award className="size-8 text-brand mb-1" />
                  <span className="font-heading text-5xl font-bold tracking-tight text-brand">
                    {result.finalScore != null ? Number(result.finalScore).toFixed(1) : '--'}
                  </span>
                  <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mt-1">
                    Final score
                  </span>
                </div>

                <div>
                  <h3 className="flex items-center gap-2 font-heading font-medium text-foreground">
                    <MessageSquare className="size-4 text-brand" />
                    Examiner feedback
                  </h3>
                  <p className="mt-2 text-sm text-foreground/90 leading-relaxed italic bg-muted/50 p-4 rounded-xl border">
                    "{result.finalFeedback || 'No overall feedback yet.'}"
                  </p>

                  <div className="mt-4 flex flex-wrap gap-4 text-xs text-muted-foreground">
                    {result.submittedAt && (
                      <span className="flex items-center gap-1.5">
                        <Calendar className="size-3.5" />
                        Submitted: {formatDateTime(parseApiDate(result.submittedAt))}
                      </span>
                    )}
                    {result.publishedAt && (
                      <span className="flex items-center gap-1.5">
                        <CheckCircle2 className="size-3.5 text-emerald-600" />
                        Published: {formatDateTime(parseApiDate(result.publishedAt))}
                      </span>
                    )}
                  </div>
                </div>
              </div>
            ) : (
              <div className="mt-6 rounded-2xl border border-amber-200 bg-amber-50/70 p-6 dark:border-amber-900/50 dark:bg-amber-950/20">
                <h3 className="font-heading font-medium text-amber-900 dark:text-amber-200">
                  Your exam is being reviewed
                </h3>
                <p className="mt-1 text-sm text-amber-800 dark:text-amber-300">
                  {result.message || 'The AI has made a first assessment. The examiner is listening to your answers and reviewing the scores before publishing.'}
                </p>
                {result.submittedAt && (
                  <p className="mt-3 text-xs text-amber-700 dark:text-amber-400">
                    Submitted: {formatDateTime(parseApiDate(result.submittedAt))}
                  </p>
                )}
              </div>
            )}
          </section>

          {/* Breakdown Per Question (Only when published) */}
          {result.isPublished && result.answers?.length > 0 && (
            <section className="space-y-4">
              <h3 className="font-heading text-xl font-medium">Question by question</h3>

              {result.answers.map((answer, index) => (
                <article
                  key={answer.answerId}
                  className="rounded-3xl border bg-card p-6 shadow-sm transition-all sm:p-7"
                >
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <span className="flex items-center gap-2 text-sm font-semibold text-brand">
                      Question {index + 1}
                      {answer.isFollowUp && (
                        <Badge variant="secondary" className="text-xs bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300">
                          Follow-up
                        </Badge>
                      )}
                    </span>

                    {answer.finalScore != null && (
                      <span className="rounded-full bg-brand-soft px-3 py-1 font-mono text-sm font-bold text-brand">
                        {Number(answer.finalScore).toFixed(1)} pts
                      </span>
                    )}
                  </div>

                  <h4 className="mt-3 font-heading text-base font-medium text-foreground">
                    {answer.questionText}
                  </h4>

                  {/* Transcript / Student Answer */}
                  <div className="mt-4 rounded-2xl bg-muted/60 p-4 text-sm">
                    <span className="flex items-center gap-1.5 text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-1">
                      <Volume2 className="size-3.5 text-brand" />
                      Your answer:
                    </span>
                    <p className="text-foreground leading-relaxed">
                      {answer.studentAnswer || answer.transcript || '(No transcript)'}
                    </p>

                    {answer.audioUrl && (
                      <div className="mt-3">
                        <audio controls src={answer.audioUrl} className="h-9 w-full max-w-sm" />
                      </div>
                    )}
                  </div>

                  {/* Teacher's comment on question */}
                  {answer.teacherComment && (
                    <div className="mt-3 rounded-xl border border-dashed border-brand/30 bg-brand-soft/20 p-3 text-xs text-foreground">
                      <span className="font-semibold text-brand">Examiner's comment: </span>
                      {answer.teacherComment}
                    </div>
                  )}
                </article>
              ))}
            </section>
          )}
        </div>
      )}

      {!result && !isLoading && !errorMessage && (
        <div className="rounded-3xl border border-dashed p-12 text-center text-muted-foreground">
          <HelpCircle className="mx-auto size-10 opacity-40 mb-3" />
          <p>Enter a session ID above to check your exam result.</p>
        </div>
      )}
    </div>
  )
}
