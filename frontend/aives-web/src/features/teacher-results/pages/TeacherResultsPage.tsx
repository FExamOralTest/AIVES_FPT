import { AlertCircle, Award, Clock, Edit3, Inbox, RotateCw, Search, Sparkles } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { formatDateTime, parseApiDate } from '@/lib/format/date'
import { useApi } from '@/lib/http/useApi'
import { teacherResultsApi } from '../api/teacherResultsApi'
import { ResultReviewModal } from '../components/ResultReviewModal'
import type { ResultForReviewDto } from '../types'

const STATUS_CONFIG: Record<string, { label: string; badgeClass: string }> = {
  PENDING_REVIEW: { label: 'Pending review', badgeClass: 'bg-warning-soft text-warning' },
  REVIEWED: { label: 'Reviewed', badgeClass: 'bg-brand-soft text-brand' },
  PUBLISHED: { label: 'Published', badgeClass: 'bg-success-soft text-success' },
}

interface SessionGroup {
  sessionId: number
  sessionName: string
  examTitle: string
  results: ResultForReviewDto[]
}

/** Keeps the API's newest-first order: groups appear in the order their newest submission arrived. */
function groupBySession(results: ResultForReviewDto[]): SessionGroup[] {
  const groups = new Map<number, SessionGroup>()
  for (const r of results) {
    const group = groups.get(r.sessionId) ?? { sessionId: r.sessionId, sessionName: r.sessionName, examTitle: r.examTitle, results: [] }
    group.results.push(r)
    groups.set(r.sessionId, group)
  }
  return [...groups.values()]
}

export default function TeacherResultsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const lookupId = Number(searchParams.get('sessionId')) || null

  const pending = useApi(teacherResultsApi.listPending, '', 'Could not load results waiting for review.')
  // Fallback lookup of one session (any status, including published), driven by ?sessionId=.
  const lookup = useApi(
    () => (lookupId ? teacherResultsApi.listBySession(lookupId) : Promise.resolve([])),
    lookupId ?? 0,
    'Could not load this session.',
  )
  const [lookupInput, setLookupInput] = useState(lookupId ? String(lookupId) : '')
  const [selectedResult, setSelectedResult] = useState<ResultForReviewDto | null>(null)

  const groups = useMemo(() => (pending.state.status === 'ready' ? groupBySession(pending.state.data) : []), [pending.state])
  const pendingCount = pending.state.status === 'ready' ? pending.state.data.length : 0

  function handleUpdatedResult(updated: ResultForReviewDto) {
    // Published results leave the review queue; reviewed ones stay until published.
    pending.update((list) =>
      updated.status === 'PUBLISHED' ? list.filter((r) => r.resultId !== updated.resultId) : list.map((r) => (r.resultId === updated.resultId ? updated : r)),
    )
    lookup.update((list) => list.map((r) => (r.resultId === updated.resultId ? updated : r)))
    if (selectedResult?.resultId === updated.resultId) setSelectedResult(updated)
  }

  function submitLookup(event: FormEvent) {
    event.preventDefault()
    const id = Number(lookupInput)
    if (Number.isInteger(id) && id > 0) setSearchParams({ sessionId: String(id) })
  }

  return (
    <div className="mx-auto max-w-5xl py-6">
      <header className="mb-8 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-3xl font-medium tracking-tight sm:text-4xl">Review & publish results</h1>
          <p className="mt-2 max-w-2xl text-muted-foreground">
            Review students' viva answers, listen to the audio, read the AI analysis, adjust scores and publish official results.
          </p>
        </div>
        <Button variant="secondary" onClick={pending.reload} disabled={pending.state.status === 'loading'}>
          <RotateCw aria-hidden="true" />
          Refresh
        </Button>
      </header>

      <section aria-labelledby="queue-heading" aria-busy={pending.state.status === 'loading'}>
        <h2 id="queue-heading" className="font-heading text-lg font-medium">
          Waiting for review{pending.state.status === 'ready' && ` (${pendingCount})`}
        </h2>

        {pending.state.status === 'loading' && (
          <div className="mt-4 space-y-3">
            {[0, 1].map((i) => <div key={i} className="h-24 animate-pulse rounded-3xl bg-muted motion-reduce:animate-none" />)}
          </div>
        )}

        {pending.state.status === 'error' && (
          <Alert variant="destructive" className="mt-4 rounded-2xl">
            <AlertCircle className="size-4" />
            <AlertDescription>{pending.state.message}</AlertDescription>
          </Alert>
        )}

        {pending.state.status === 'ready' && groups.length === 0 && (
          <div className="mt-4 flex flex-col items-center gap-2 rounded-3xl border border-dashed p-10 text-center text-muted-foreground">
            <Inbox className="size-9 opacity-50" aria-hidden="true" />
            <p className="font-medium text-foreground">Nothing is waiting for review</p>
            <p className="max-w-md text-sm">Results appear here after students finish a viva exam and the AI has scored their answers.</p>
          </div>
        )}

        <div className="mt-4 space-y-6">
          {groups.map((group) => (
            <SessionResults key={group.sessionId} group={group} onOpen={setSelectedResult} />
          ))}
        </div>
      </section>

      {/* Fallback while the queue is empty for examiners, and to reopen published results of a session. */}
      <details className="mt-10 rounded-3xl border p-5 sm:p-6" open={Boolean(lookupId)}>
        <summary className="cursor-pointer font-heading font-medium">Look up a session by ID</summary>
        <p className="mt-2 text-sm text-muted-foreground">
          Shows every submission in one session, including published ones.
        </p>
        <form onSubmit={submitLookup} className="mt-4 flex max-w-md gap-3">
          <div className="relative flex-1">
            <Search className="absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
            <input
              type="number"
              min={1}
              aria-label="Session ID"
              value={lookupInput}
              onChange={(e) => setLookupInput(e.target.value)}
              placeholder="Session ID"
              className="h-11 w-full rounded-xl border border-input bg-background pr-4 pl-10 text-sm outline-none focus:border-brand focus:ring-4 focus:ring-brand/15"
            />
          </div>
          <Button type="submit" className="h-11 px-5" disabled={!lookupInput}>
            Show
          </Button>
        </form>

        {lookupId && (
          <div className="mt-5">
            {lookup.state.status === 'loading' && <p role="status" className="text-sm text-muted-foreground">Loading session #{lookupId}…</p>}
            {lookup.state.status === 'error' && <p role="alert" className="text-sm text-destructive">{lookup.state.message}</p>}
            {lookup.state.status === 'ready' &&
              (lookup.state.data.length === 0 ? (
                <p className="text-sm text-muted-foreground">Session #{lookupId} has no submissions yet.</p>
              ) : (
                groupBySession(lookup.state.data).map((group) => (
                  <SessionResults key={group.sessionId} group={group} onOpen={setSelectedResult} />
                ))
              ))}
          </div>
        )}
      </details>

      {selectedResult && (
        <ResultReviewModal result={selectedResult} onClose={() => setSelectedResult(null)} onUpdated={handleUpdatedResult} />
      )}
    </div>
  )
}

function SessionResults({ group, onOpen }: { group: SessionGroup; onOpen: (result: ResultForReviewDto) => void }) {
  return (
    <div>
      <div className="mb-2 flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="font-heading font-medium">
          {group.examTitle || 'Exam'} <span className="text-muted-foreground">· {group.sessionName}</span>
        </h3>
        <span className="text-xs text-muted-foreground">
          Session #{group.sessionId} · {group.results.length} submission{group.results.length === 1 ? '' : 's'}
        </span>
      </div>
      <ul className="divide-y overflow-hidden rounded-3xl border bg-card shadow-sm">
        {group.results.map((res) => (
          <ResultRow key={res.resultId} result={res} onOpen={() => onOpen(res)} />
        ))}
      </ul>
    </div>
  )
}

function ResultRow({ result: res, onOpen }: { result: ResultForReviewDto; onOpen: () => void }) {
  const statusMeta = STATUS_CONFIG[res.status] ?? { label: res.status, badgeClass: 'bg-muted text-foreground' }
  return (
    <li className="flex flex-col gap-4 p-5 transition-colors hover:bg-muted/30 sm:flex-row sm:items-center sm:justify-between sm:p-6">
      <div className="space-y-1">
        <div className="flex flex-wrap items-center gap-3">
          <p className="font-heading text-base font-semibold text-foreground">{res.studentName}</p>
          <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${statusMeta.badgeClass}`}>
            {statusMeta.label}
          </span>
        </div>
        <p className="text-xs text-muted-foreground">{res.studentEmail}</p>
        {res.submittedAt && (
          <p className="flex items-center gap-1 text-xs text-muted-foreground">
            <Clock className="size-3" aria-hidden="true" />
            Submitted: {formatDateTime(parseApiDate(res.submittedAt))}
          </p>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-4 sm:justify-end">
        <div className="flex gap-4 text-right">
          <div>
            <span className="block text-[11px] font-semibold text-muted-foreground uppercase">AI suggested</span>
            <span className="flex items-center justify-end gap-1 font-heading text-sm font-semibold text-foreground">
              <Sparkles className="size-3 text-amber-500" aria-hidden="true" />
              {res.aiScore != null ? res.aiScore.toFixed(1) : '--'}
            </span>
          </div>
          <div>
            <span className="block text-[11px] font-semibold text-muted-foreground uppercase">Official score</span>
            <span className="flex items-center justify-end gap-1 font-heading text-base font-bold text-brand">
              <Award className="size-4" aria-hidden="true" />
              {res.finalScore != null ? res.finalScore.toFixed(1) : '--'}
            </span>
          </div>
        </div>
        <Button variant={res.status === 'PUBLISHED' ? 'outline' : 'default'} onClick={onOpen} className="gap-2 rounded-xl">
          <Edit3 className="size-4" aria-hidden="true" />
          {res.status === 'PUBLISHED' ? 'View' : 'Review & score'}
        </Button>
      </div>
    </li>
  )
}
