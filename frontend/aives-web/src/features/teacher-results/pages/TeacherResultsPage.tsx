import { AlertCircle, CalendarClock, CheckCircle2, Inbox, Loader2, RotateCw, Search } from 'lucide-react'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { periodsApi } from '@/features/exam-sessions/api/periodsApi'
import { formatDateTime, parseApiDate } from '@/lib/format/date'
import { toApiError } from '@/lib/http/apiError'
import { useApi } from '@/lib/http/useApi'
import { teacherResultsApi } from '../api/teacherResultsApi'
import { ResultReviewModal, type ReviewNavigation } from '../components/ResultReviewModal'
import type { ResultForReviewDto, ResultSearchResponse } from '../types'

const STATUS_LABEL: Record<string, string> = {
  PENDING_REVIEW: 'Pending review', SUBMITTED: 'Submitted', REVIEWED: 'Reviewed',
  PUBLISHED: 'Published', CANCELLED: 'Voided attempt',
}
const STATUS_OPTIONS = ['PENDING_REVIEW', 'PUBLISHED', 'CANCELLED']
const AI_OPTIONS = ['PENDING', 'SUCCEEDED', 'FAILED']
const SELECT = 'h-10 w-full rounded-xl border border-input bg-background px-3 text-sm'

export default function TeacherResultsPage() {
  const [params, setParams] = useSearchParams()
  const periods = useApi(() => periodsApi.list(), '', 'Could not load exam periods.')
  const periodId = params.get('periodId') ?? ''
  const sessionId = params.get('sessionId') ?? ''
  const periodSelection = params.get('unassigned') === 'true' ? 'UNASSIGNED' : periodId || (sessionId || params.get('all') ? 'ALL' : '')
  const enabled = Boolean(periodSelection)
  const rawStatus = params.get('status') ?? ''
  const status = STATUS_OPTIONS.includes(rawStatus) ? rawStatus : ''
  const request = new URLSearchParams()
  if (periodId) request.set('periodId', periodId)
  if (periodSelection === 'UNASSIGNED') request.set('unassigned', 'true')
  if (sessionId) request.set('sessionId', sessionId)
  if (status) request.set('status', status)
  for (const key of ['aiStatus', 'search']) if (params.get(key)) request.set(key, params.get(key)!)
  const rawPage = Number(params.get('page') || 1)
  const page = Number.isInteger(rawPage) && rawPage >= 1 && rawPage <= 1000000 ? rawPage : 1
  request.set('page', String(page))
  request.set('pageSize', '20')
  const query = request.toString()
  const [attempt, setAttempt] = useState(0)
  // Tag responses so changing filters cannot flash attempts from the previous period.
  const [response, setResponse] = useState<{ query: string; data?: ResultSearchResponse; error?: string } | null>(null)
  const [selected, setSelected] = useState<ResultForReviewDto | null>(null)
  const [opening, setOpening] = useState<number | null>(null)
  const [detailError, setDetailError] = useState<string | null>(null)
  const [flash, setFlash] = useState<string | null>(null)
  const detailRequest = useRef<AbortController | null>(null)
  useEffect(() => () => detailRequest.current?.abort(), [])

  useEffect(() => {
    if (!enabled) return
    const controller = new AbortController()
    teacherResultsApi.search(query, controller.signal).then(data => {
      if (!controller.signal.aborted) setResponse({ query, data })
    }).catch(err => {
      if (!controller.signal.aborted) setResponse({ query, error: toApiError(err).message })
    })
    return () => controller.abort()
  }, [query, enabled, attempt])

  const current = response?.query === query ? response : null
  const data = current?.data
  const loading = enabled && !current
  const aiStatus = params.get('aiStatus') ?? ''

  function changeFilter(key: string, value: string) {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    next.delete('page')
    if (key === 'sessionId' && periodSelection === 'ALL') next.set('all', 'true')
    if (key === 'period') {
      next.delete('period'); next.delete('periodId'); next.delete('unassigned'); next.delete('all'); next.delete('sessionId')
      if (value === 'ALL') next.set('all', 'true')
      else if (value === 'UNASSIGNED') next.set('unassigned', 'true')
      else if (value) next.set('periodId', value)
    }
    setParams(next)
    detailRequest.current?.abort()
    setOpening(null); setSelected(null); setDetailError(null)
  }

  function refresh() { setResponse(null); setAttempt(n => n + 1) }
  // Reloads the list but keeps showing the current rows until the new ones arrive,
  // so the review dialog keeps its place while the examiner works through students.
  function refreshQuietly() { setAttempt(n => n + 1) }

  async function openResult(resultId: number) {
    detailRequest.current?.abort()
    const controller = new AbortController()
    detailRequest.current = controller
    setOpening(resultId); setDetailError(null)
    try {
      const result = await teacherResultsApi.get(resultId, controller.signal)
      if (!controller.signal.aborted) setSelected(result)
    } catch (err) {
      if (!controller.signal.aborted) setDetailError(toApiError(err).message)
    } finally {
      if (!controller.signal.aborted) setOpening(null)
    }
  }

  const items = data?.items ?? []
  const selectedIndex = selected ? items.findIndex(r => r.resultId === selected.resultId) : -1
  const isDone = (status: string) => status === 'PUBLISHED' || status === 'CANCELLED'
  const navigation: ReviewNavigation | undefined = selectedIndex < 0 ? undefined : {
    position: selectedIndex + 1,
    total: items.length,
    hasPrevious: selectedIndex > 0,
    hasNext: selectedIndex < items.length - 1,
    loading: opening !== null,
    error: detailError,
    onPrevious: () => void openResult(items[selectedIndex - 1].resultId),
    onNext: () => void openResult(items[selectedIndex + 1].resultId),
  }

  /** After publishing, open the next attempt on this page that still needs review. */
  function openNextPending(published: ResultForReviewDto) {
    const from = items.findIndex(r => r.resultId === published.resultId)
    const next = items.slice(from + 1).find(r => !isDone(r.status))
      ?? items.slice(0, Math.max(from, 0)).find(r => !isDone(r.status))
    if (next) {
      void openResult(next.resultId)
      return
    }
    setSelected(null)
    setFlash(`Published ${published.studentName}. Every attempt on this page is reviewed.`)
  }

  function submitSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    changeFilter('search', String(new FormData(event.currentTarget).get('search') ?? '').trim())
  }

  return (
    <div className="mx-auto max-w-6xl py-6">
      <header className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-3xl font-medium">Review & publish results</h1>
          <p className="mt-2 text-muted-foreground">Choose a staff-created exam period to find students' attempts, review answers and publish scores.</p>
        </div>
        <Button variant="secondary" onClick={() => { periods.reload(); refresh() }}><RotateCw className="size-4" /> Refresh</Button>
      </header>

      {periods.state.status === 'error' && <Alert variant="destructive" className="mb-4"><AlertCircle /><AlertDescription>{periods.state.message}</AlertDescription></Alert>}
      <section aria-label="Result filters" className="mb-5 grid gap-4 rounded-2xl border p-4 sm:grid-cols-2 lg:grid-cols-4">
        <label className="space-y-1 text-sm font-medium">Exam period
          <select className={SELECT} value={periodSelection} onChange={e => changeFilter('period', e.target.value)} disabled={periods.state.status === 'loading'}>
            <option value="">Choose an exam period</option>
            <option value="ALL">All periods</option>
            <option value="UNASSIGNED">Unassigned / general sessions</option>
            {periods.state.status === 'ready' && periods.state.data.map(p => <option key={p.periodId} value={p.periodId}>{p.periodName} · {p.courseName} ({p.status})</option>)}
          </select>
        </label>
        <label className="space-y-1 text-sm font-medium">Session
          <select className={SELECT} value={sessionId} onChange={e => changeFilter('sessionId', e.target.value)} disabled={!enabled || loading}>
            <option value="">All sessions in this period</option>
            {sessionId && !data?.sessions.some(s => String(s.sessionId) === sessionId) && <option value={sessionId}>Session #{sessionId}</option>}
            {data?.sessions.map(s => <option key={s.sessionId} value={s.sessionId}>{s.sessionName} · #{s.sessionId}</option>)}
          </select>
        </label>
        <label className="space-y-1 text-sm font-medium">Review status
          <select className={SELECT} value={status} onChange={e => changeFilter('status', e.target.value)} disabled={!enabled}>
            <option value="">All review statuses</option>
            {STATUS_OPTIONS.map(s => <option key={s} value={s}>{STATUS_LABEL[s]}</option>)}
          </select>
        </label>
        <label className="space-y-1 text-sm font-medium">AI grading
          <select className={SELECT} value={aiStatus} onChange={e => changeFilter('aiStatus', e.target.value)} disabled={!enabled}>
            <option value="">All AI statuses</option>
            {AI_OPTIONS.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        </label>
        <form onSubmit={submitSearch} className="flex gap-2 sm:col-span-2 lg:col-span-4">
          <input key={params.get('search') ?? ''} name="search" aria-label="Student name, email or code" defaultValue={params.get('search') ?? ''} maxLength={150} placeholder="Student name, email or code" className={SELECT} disabled={!enabled} />
          <Button type="submit" variant="outline" disabled={!enabled}><Search className="size-4" /> Search</Button>
        </form>
      </section>

      {flash && <Alert className="mb-4"><CheckCircle2 /><AlertDescription className="flex items-center justify-between gap-3">{flash}<button type="button" className="text-xs underline" onClick={() => setFlash(null)}>Dismiss</button></AlertDescription></Alert>}
      {detailError && !selected && <Alert variant="destructive" className="mb-4"><AlertDescription>{detailError}</AlertDescription></Alert>}
      {!enabled && <div className="rounded-2xl border border-dashed p-12 text-center"><CalendarClock className="mx-auto mb-3 text-brand" /><p>Choose an exam period above to see students' attempts.</p></div>}
      {loading && <p role="status" className="p-8 text-center text-muted-foreground">Loading attempts…</p>}
      {current?.error && <Alert variant="destructive"><AlertDescription>{current.error} <button type="button" className="underline" onClick={refresh}>Try again</button></AlertDescription></Alert>}
      {enabled && data && <>
        <p className="mb-3 text-sm text-muted-foreground">{data.totalCount} attempt(s) found. AI scores marked “Not graded” have no score yet.</p>
        {data.items.length === 0 ? <div className="rounded-2xl border border-dashed p-10 text-center"><Inbox className="mx-auto mb-2 text-muted-foreground" />No attempts match these filters.</div> : (
          <div className="overflow-x-auto rounded-2xl border">
            <table className="w-full text-left text-sm">
              <thead className="bg-muted"><tr>{['Student', 'Period / session', 'Review / AI', 'AI score', 'Official score', ''].map((h, i) => <th key={i} className="p-4 font-medium">{h}</th>)}</tr></thead>
              <tbody className="divide-y">{data.items.map(r => <tr key={r.resultId}>
                <td className="p-4"><p className="font-medium">{r.studentName}</p><p className="text-xs text-muted-foreground">{r.studentCode} · {r.studentEmail}</p>{r.submittedAt && <p className="mt-1 text-xs text-muted-foreground">{formatDateTime(parseApiDate(r.submittedAt))}</p>}</td>
                <td className="p-4"><p>{r.periodName ?? 'Unassigned'}</p><p className="text-xs text-muted-foreground">{r.sessionName} · #{r.sessionId}</p><p className="text-xs text-muted-foreground">{r.examTitle}</p></td>
                <td className="p-4"><p>{STATUS_LABEL[r.status] ?? r.status}</p><p className="text-xs text-muted-foreground">AI: {r.aiEvaluationStatus ?? 'Not graded'}</p></td>
                <td className="p-4">{r.aiScore == null ? 'Not graded' : r.aiScore.toFixed(1)}</td>
                <td className="p-4">{r.finalScore == null ? 'Not finalized' : r.finalScore.toFixed(1)}</td>
                <td className="p-4"><Button variant="outline" onClick={() => void openResult(r.resultId)} disabled={opening === r.resultId}>{opening === r.resultId ? <Loader2 className="size-4 animate-spin" /> : r.status === 'PUBLISHED' || r.status === 'CANCELLED' ? 'View' : 'Review'}</Button></td>
              </tr>)}</tbody>
            </table>
          </div>
        )}
        <nav aria-label="Result pages" className="mt-4 flex items-center justify-end gap-3">
          <Button variant="outline" disabled={page <= 1} onClick={() => { const next = new URLSearchParams(params); next.set('page', String(page - 1)); setParams(next) }}>Previous</Button>
          <span className="text-sm">Page {page} / {Math.max(1, Math.ceil(data.totalCount / data.pageSize))}</span>
          <Button variant="outline" disabled={page * data.pageSize >= data.totalCount} onClick={() => { const next = new URLSearchParams(params); next.set('page', String(page + 1)); setParams(next) }}>Next</Button>
        </nav>
      </>}
      {selected && <ResultReviewModal
        key={selected.resultId}
        result={selected}
        navigation={navigation}
        onClose={() => { detailRequest.current?.abort(); setOpening(null); setDetailError(null); setSelected(null) }}
        onUpdated={updated => { setSelected(updated); refreshQuietly() }}
        onPublished={openNextPending}
      />}
    </div>
  )
}
