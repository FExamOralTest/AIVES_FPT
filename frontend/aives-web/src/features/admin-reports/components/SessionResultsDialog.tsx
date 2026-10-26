import { CircleAlert, Inbox, RotateCw, Volume2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { formatDateTime, parseApiDate } from '@/lib/format/date'
import { useApi } from '@/lib/http/useApi'
import { cn } from '@/lib/utils'
import { adminReportsApi } from '../api/adminReportsApi'
import { RESULT_STATUS } from '../lib/resultStatus'
import type { ExamSessionSummary, ResultForReviewDto } from '../types'

interface SessionResultsDialogProps {
  session: ExamSessionSummary | null
  onClose: () => void
}

/** Read-only list of one session's results; reviewing and publishing stay with the examiner. */
export function SessionResultsDialog({ session, onClose }: SessionResultsDialogProps) {
  return (
    <Dialog open={session !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>{session?.sessionName}</DialogTitle>
          <DialogDescription>
            {session?.examTitle} · {session?.enrolledStudents} student{session?.enrolledStudents === 1 ? '' : 's'} enrolled
          </DialogDescription>
        </DialogHeader>
        {session && <ResultList key={session.sessionId} sessionId={session.sessionId} />}
      </DialogContent>
    </Dialog>
  )
}

function ResultList({ sessionId }: { sessionId: number }) {
  const { state, reload } = useApi(() => adminReportsApi.listResults(sessionId), sessionId, 'Could not load the results of this session.')

  if (state.status === 'loading') {
    return (
      <ul aria-busy="true" className="flex flex-col gap-2">
        {[0, 1, 2].map((i) => (
          <li key={i} className="h-16 animate-pulse rounded-2xl bg-muted motion-reduce:animate-none" />
        ))}
      </ul>
    )
  }

  if (state.status === 'error') {
    return (
      <div className="flex flex-col items-center gap-3 rounded-2xl border px-6 py-10 text-center">
        <CircleAlert className="size-7 text-destructive" aria-hidden="true" />
        <p className="max-w-sm text-muted-foreground">{state.message}</p>
        <Button variant="secondary" size="sm" onClick={reload}>
          <RotateCw aria-hidden="true" />
          Try again
        </Button>
      </div>
    )
  }

  if (state.data.length === 0) {
    return (
      <div className="flex flex-col items-center gap-2 rounded-2xl border-2 border-dashed px-6 py-10 text-center">
        <Inbox className="size-7 text-muted-foreground" aria-hidden="true" />
        <p className="text-muted-foreground">No student has submitted in this session yet.</p>
      </div>
    )
  }

  return (
    <ul className="flex flex-col gap-2">
      {state.data.map((result) => (
        <li key={result.resultId}>
          <ResultItem result={result} />
        </li>
      ))}
    </ul>
  )
}

function ResultItem({ result }: { result: ResultForReviewDto }) {
  const status = RESULT_STATUS[result.status] ?? { label: result.status, tone: 'bg-muted text-foreground' }
  return (
    <details className="group rounded-2xl border">
      <summary className="flex cursor-pointer list-none flex-wrap items-center gap-x-4 gap-y-2 p-4 hover:bg-muted/30 [&::-webkit-details-marker]:hidden">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-medium">{result.studentName}</span>
            <span className={cn('rounded-full px-2.5 py-0.5 text-xs font-medium', status.tone)}>{status.label}</span>
          </div>
          <p className="truncate text-xs text-muted-foreground">
            {result.studentEmail}
            {result.submittedAt && ` · submitted ${formatDateTime(parseApiDate(result.submittedAt))}`}
          </p>
        </div>
        <dl className="flex gap-5 text-right text-sm">
          <div>
            <dt className="text-[11px] text-muted-foreground uppercase">AI</dt>
            <dd className="tabular-nums">{formatScore(result.aiScore)}</dd>
          </div>
          <div>
            <dt className="text-[11px] text-muted-foreground uppercase">Official</dt>
            <dd className="font-semibold text-brand tabular-nums">{formatScore(result.finalScore)}</dd>
          </div>
        </dl>
      </summary>

      <div className="space-y-4 border-t p-4 text-sm">
        {(result.finalFeedback || result.aiFeedback) && (
          <p className="whitespace-pre-line text-muted-foreground">{result.finalFeedback || result.aiFeedback}</p>
        )}
        {result.answers.length === 0 ? (
          <p className="text-muted-foreground">No answers recorded.</p>
        ) : (
          <ol className="space-y-4">
            {result.answers.map((answer, i) => (
              <li key={answer.answerId} className="space-y-1.5">
                <p className="font-medium">
                  {answer.isFollowUp ? 'Follow-up' : `Question ${i + 1}`}: {answer.questionText}
                </p>
                <p className="whitespace-pre-line text-muted-foreground">
                  {answer.transcript || answer.studentAnswer || 'No answer.'}
                </p>
                {answer.audioUrl && (
                  <div className="flex items-center gap-2">
                    <Volume2 className="size-4 text-muted-foreground" aria-hidden="true" />
                    <audio controls preload="none" src={answer.audioUrl} className="h-9 w-full max-w-sm" />
                  </div>
                )}
                <p className="text-xs text-muted-foreground">
                  AI {formatScore(answer.aiScore)} · Official {formatScore(answer.finalScore)}
                  {answer.status === 'ERROR' && <span className="text-destructive"> · AI scoring failed</span>}
                  {answer.teacherComment && ` · “${answer.teacherComment}”`}
                </p>
              </li>
            ))}
          </ol>
        )}
      </div>
    </details>
  )
}

function formatScore(value: number | null | undefined): string {
  return value == null ? '—' : value.toFixed(1)
}
