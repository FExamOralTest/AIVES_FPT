import {
  AlertCircle,
  Award,
  Clock,
  Edit3,
  HelpCircle,
  Search,
  Sparkles,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { formatDateTime, parseApiDate } from '@/lib/format/date'
import { toApiError } from '@/lib/http/apiError'
import { teacherResultsApi } from '../api/teacherResultsApi'
import { ResultReviewModal } from '../components/ResultReviewModal'
import type { ResultForReviewDto } from '../types'

const STATUS_CONFIG: Record<string, { label: string; badgeClass: string }> = {
  PENDING_REVIEW: { label: 'Pending review', badgeClass: 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300' },
  REVIEWED: { label: 'Reviewed', badgeClass: 'bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300' },
  PUBLISHED: { label: 'Published', badgeClass: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300' },
}

export default function TeacherResultsPage() {
  const [searchParams] = useSearchParams()
  const initialSession = searchParams.get('sessionId') ?? ''

  const [sessionIdInput, setSessionIdInput] = useState(initialSession)
  const [results, setResults] = useState<ResultForReviewDto[]>([])
  const [selectedResult, setSelectedResult] = useState<ResultForReviewDto | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    if (initialSession) {
      loadResults(Number(initialSession))
    }
  }, [initialSession])

  async function loadResults(id: number) {
    if (!id || isNaN(id)) {
      setErrorMessage('Please enter a valid session ID.')
      return
    }
    setIsLoading(true)
    setErrorMessage(null)
    try {
      const data = await teacherResultsApi.listBySession(id)
      setResults(data)
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
      setResults([])
    } finally {
      setIsLoading(false)
    }
  }

  function handleUpdatedResult(updated: ResultForReviewDto) {
    setResults((prev) =>
      prev.map((r) => (r.resultId === updated.resultId ? updated : r)),
    )
    if (selectedResult?.resultId === updated.resultId) {
      setSelectedResult(updated)
    }
  }

  return (
    <div className="mx-auto max-w-5xl py-6">
      <header className="mb-8">
        <h1 className="font-heading text-3xl font-medium tracking-tight sm:text-4xl">
          Review & publish results
        </h1>
        <p className="mt-2 text-muted-foreground">
          Review students' viva answers, listen to the audio, read the AI analysis, adjust scores and publish official results.
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
            onClick={() => loadResults(Number(sessionIdInput))}
            disabled={isLoading || !sessionIdInput}
            className="h-11 px-5"
          >
            {isLoading ? 'Loading…' : 'Show results'}
          </Button>
        </div>
      </header>

      {errorMessage && (
        <Alert variant="destructive" className="mb-6 rounded-2xl">
          <AlertCircle className="size-4" />
          <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
      )}

      {/* Submissions Table / Cards */}
      {results.length > 0 ? (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="font-heading text-lg font-medium">
              Submissions ({results.length})
            </h2>
            <span className="text-xs text-muted-foreground">
              Ca thi: {results[0]?.sessionName}
            </span>
          </div>

          <div className="overflow-hidden rounded-3xl border bg-card shadow-sm">
            <div className="divide-y divide-border">
              {results.map((res) => {
                const statusMeta = STATUS_CONFIG[res.status] ?? {
                  label: res.status,
                  badgeClass: 'bg-muted text-foreground',
                }

                return (
                  <div
                    key={res.resultId}
                    className="flex flex-col gap-4 p-5 transition-colors hover:bg-muted/30 sm:flex-row sm:items-center sm:justify-between sm:p-6"
                  >
                    <div className="space-y-1">
                      <div className="flex items-center gap-3">
                        <h3 className="font-heading text-base font-semibold text-foreground">
                          {res.studentName}
                        </h3>
                        <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${statusMeta.badgeClass}`}>
                          {statusMeta.label}
                        </span>
                      </div>
                      <p className="text-xs text-muted-foreground">{res.studentEmail}</p>
                      {res.submittedAt && (
                        <p className="text-xs text-muted-foreground flex items-center gap-1">
                          <Clock className="size-3" />
                          Submitted: {formatDateTime(parseApiDate(res.submittedAt))}
                        </p>
                      )}
                    </div>

                    <div className="flex flex-wrap items-center gap-4 sm:justify-end">
                      {/* Proposed & Final Scores */}
                      <div className="flex gap-4 text-right">
                        <div>
                          <span className="block text-[11px] uppercase font-semibold text-muted-foreground">
                            AI suggested
                          </span>
                          <span className="font-heading text-sm font-semibold text-foreground flex items-center gap-1 justify-end">
                            <Sparkles className="size-3 text-amber-500" />
                            {res.aiScore != null ? res.aiScore.toFixed(1) : '--'}
                          </span>
                        </div>

                        <div>
                          <span className="block text-[11px] uppercase font-semibold text-muted-foreground">
                            Official score
                          </span>
                          <span className="font-heading text-base font-bold text-brand flex items-center gap-1 justify-end">
                            <Award className="size-4" />
                            {res.finalScore != null ? res.finalScore.toFixed(1) : '--'}
                          </span>
                        </div>
                      </div>

                      {/* Action */}
                      <Button
                        variant={res.status === 'PUBLISHED' ? 'outline' : 'default'}
                        onClick={() => setSelectedResult(res)}
                        className="gap-2 rounded-xl"
                      >
                        <Edit3 className="size-4" />
                        {res.status === 'PUBLISHED' ? 'View' : 'Review & score'}
                      </Button>
                    </div>
                  </div>
                )
              })}
            </div>
          </div>
        </div>
      ) : (
        !isLoading &&
        !errorMessage && (
          <div className="rounded-3xl border border-dashed p-12 text-center text-muted-foreground">
            <HelpCircle className="mx-auto size-10 opacity-40 mb-3" />
            <p>Enter a session ID to see the student submissions to review.</p>
          </div>
        )
      )}

      {/* Review Modal */}
      {selectedResult && (
        <ResultReviewModal
          result={selectedResult}
          onClose={() => setSelectedResult(null)}
          onUpdated={handleUpdatedResult}
        />
      )}
    </div>
  )
}
