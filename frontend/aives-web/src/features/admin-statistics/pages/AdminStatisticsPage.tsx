import { CircleAlert, Info, RotateCw } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { adminCoursesApi } from '@/features/admin-courses/api/adminCoursesApi'
import { ROLE_HOME } from '@/features/auth'
import { SESSION_STATUS_LABEL } from '@/lib/domain/sessionStatus'
import { formatDateTime, parseApiDate } from '@/lib/format/date'
import { useApi } from '@/lib/http/useApi'
import { statisticsApi } from '../api/statisticsApi'
import { SummaryCards } from '../components/SummaryCards'
import type { SessionStatus, StatisticsFilters } from '../types'

const inputClass = 'mt-1 block w-full rounded-xl border bg-background px-3 py-2 text-sm'
const NO_FILTERS: StatisticsFilters = { courseId: null, fromDate: '', toDate: '', sessionStatus: null }

export default function AdminStatisticsPage() {
  const [filters, setFilters] = useState<StatisticsFilters>(NO_FILTERS)
  const courses = useApi(adminCoursesApi.list, '', 'Could not load courses.')
  const summary = useApi(() => statisticsApi.summary(filters), JSON.stringify(filters), 'Could not load the statistics.')

  function patch(next: Partial<StatisticsFilters>) {
    setFilters((current) => ({ ...current, ...next }))
  }

  const hasFilters = JSON.stringify(filters) !== JSON.stringify(NO_FILTERS)

  return (
    <>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-3xl font-medium tracking-tight">Statistics</h1>
          <p className="mt-1.5 max-w-2xl text-muted-foreground">
            Sessions, attempts, scores and the review pipeline, for the whole system or one course.
          </p>
        </div>
        <Button variant="ghost" size="sm" onClick={summary.reload} disabled={summary.state.status === 'loading'}>
          <RotateCw aria-hidden="true" />
          Refresh
        </Button>
      </header>

      <section aria-label="Filters" className="mt-6 grid gap-3 rounded-2xl border p-4 sm:grid-cols-2 lg:grid-cols-[2fr_1fr_1fr_1fr_auto] lg:items-end">
        <label className="block text-sm sm:col-span-2 lg:col-span-1">
          Course
          <select
            className={inputClass}
            value={filters.courseId ?? ''}
            onChange={(e) => patch({ courseId: e.target.value ? Number(e.target.value) : null })}
          >
            <option value="">All courses</option>
            {courses.state.status === 'ready' &&
              courses.state.data.map((c) => (
                <option key={c.courseId} value={c.courseId}>
                  {c.courseCode} · {c.courseName}
                </option>
              ))}
          </select>
        </label>
        <label className="block text-sm">
          From
          <input type="date" className={inputClass} value={filters.fromDate} max={filters.toDate || undefined} onChange={(e) => patch({ fromDate: e.target.value })} />
        </label>
        <label className="block text-sm">
          To
          <input type="date" className={inputClass} value={filters.toDate} min={filters.fromDate || undefined} onChange={(e) => patch({ toDate: e.target.value })} />
        </label>
        <label className="block text-sm">
          Session status
          <select
            className={inputClass}
            value={filters.sessionStatus ?? ''}
            onChange={(e) => patch({ sessionStatus: (e.target.value || null) as SessionStatus | null })}
          >
            <option value="">All statuses</option>
            {(Object.keys(SESSION_STATUS_LABEL) as SessionStatus[]).map((status) => (
              <option key={status} value={status}>
                {SESSION_STATUS_LABEL[status]}
              </option>
            ))}
          </select>
        </label>
        <Button variant="outline" disabled={!hasFilters} onClick={() => setFilters(NO_FILTERS)}>
          Clear
        </Button>
      </section>

      <section aria-label="Statistics" aria-busy={summary.state.status === 'loading'} className="mt-6">
        {summary.state.status === 'loading' && (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            {[0, 1, 2, 3].map((i) => (
              <div key={i} className="h-40 animate-pulse rounded-2xl bg-muted motion-reduce:animate-none" />
            ))}
          </div>
        )}

        {summary.state.status === 'error' && (
          <div className="flex flex-col items-center gap-3 rounded-2xl border px-6 py-10 text-center">
            <CircleAlert className="size-7 text-destructive" aria-hidden="true" />
            <p className="max-w-sm text-muted-foreground">{summary.state.message}</p>
            <Button variant="secondary" size="sm" onClick={summary.reload}>
              <RotateCw aria-hidden="true" />
              Try again
            </Button>
          </div>
        )}

        {summary.state.status === 'ready' && summary.state.data && (
          <>
            <SummaryCards summary={summary.state.data} />
            <p className="mt-3 text-xs text-muted-foreground">
              Updated {formatDateTime(parseApiDate(summary.state.data.generatedAt))}
            </p>
          </>
        )}

        {summary.state.status === 'ready' && !summary.state.data && (
          <p className="flex items-start gap-2 rounded-2xl border-2 border-dashed px-5 py-6 text-sm text-muted-foreground">
            <Info className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            <span>
              Statistics are not available on this server yet. You can still browse sessions and results in{' '}
              <Link to={`${ROLE_HOME.ADMIN}/reports`} className="font-medium text-foreground underline">
                Exams &amp; results
              </Link>
              .
            </span>
          </p>
        )}
      </section>
    </>
  )
}
