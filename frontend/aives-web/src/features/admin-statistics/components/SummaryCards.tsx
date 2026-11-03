import { AlertTriangle, Award, CalendarClock, ClipboardCheck, Sparkles, Users } from 'lucide-react'
import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'
import type { ReportSummary } from '../types'

function score(value: number | null): string {
  return value == null ? '—' : value.toFixed(1)
}

/** The summary as four cards: sessions, students, scores and the review pipeline. */
export function SummaryCards({ summary }: { summary: ReportSummary }) {
  const s = summary
  return (
    <div className="space-y-3">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Card icon={<CalendarClock className="size-4" />} label="Sessions" value={s.totalSessions}>
          <Breakdown
            items={[
              ['In progress', s.activeSessions],
              ['Upcoming', s.upcomingSessions],
              ['Completed', s.completedSessions],
              ['Cancelled', s.cancelledSessions],
            ]}
          />
        </Card>

        <Card icon={<Users className="size-4" />} label="Students enrolled" value={s.totalEnrollments}>
          <Breakdown
            items={[
              ['Attempts', s.totalResults],
              ['Finished', s.completedAttempts],
            ]}
          />
        </Card>

        <Card icon={<Award className="size-4" />} label="Average official score" value={score(s.averageFinalScore)}>
          <Breakdown
            items={[
              ['Highest', score(s.highestFinalScore)],
              ['Lowest', score(s.lowestFinalScore)],
              ['AI average', score(s.averageAiScore)],
            ]}
          />
        </Card>

        <Card icon={<ClipboardCheck className="size-4" />} label="Waiting for review" value={s.pendingReviewCount}>
          <Breakdown
            items={[
              ['Submitted', s.submittedCount],
              ['Reviewed', s.reviewedCount],
              ['Published', s.publishedCount],
              ['Cancelled', s.cancelledResultCount],
            ]}
          />
        </Card>
      </div>

      <div
        className={cn(
          'flex flex-wrap items-center gap-x-6 gap-y-1 rounded-2xl border px-5 py-3 text-sm',
          s.errorAnswerCount > 0 && 'border-destructive/40 bg-[#fef1f0]',
        )}
      >
        <span className="flex items-center gap-1.5 font-medium">
          {s.errorAnswerCount > 0 ? (
            <AlertTriangle className="size-4 text-destructive" aria-hidden="true" />
          ) : (
            <Sparkles className="size-4 text-brand" aria-hidden="true" />
          )}
          AI scoring
        </span>
        <span className="text-muted-foreground">
          <strong className="font-medium text-foreground tabular-nums">{s.evaluatedAnswerCount}</strong> answers scored
        </span>
        <span className="text-muted-foreground">
          <strong className="font-medium text-foreground tabular-nums">{s.processingAnswerCount}</strong> still processing
        </span>
        <span className={s.errorAnswerCount > 0 ? 'text-destructive' : 'text-muted-foreground'}>
          <strong className="font-medium tabular-nums">{s.errorAnswerCount}</strong> failed
          {s.errorAnswerCount > 0 && ' · examiners can retry them from the review page'}
        </span>
      </div>
    </div>
  )
}

function Card({ icon, label, value, children }: { icon: ReactNode; label: string; value: ReactNode; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-3 rounded-2xl border bg-card p-5">
      <h3 className="flex items-center gap-2 text-sm text-muted-foreground">
        <span aria-hidden="true">{icon}</span>
        {label}
      </h3>
      <p className="font-heading text-3xl font-medium tabular-nums">{value}</p>
      {children}
    </section>
  )
}

function Breakdown({ items }: { items: [string, ReactNode][] }) {
  return (
    <dl className="mt-auto grid grid-cols-2 gap-x-3 gap-y-1 text-xs">
      {items.map(([label, value]) => (
        <div key={label} className="flex justify-between gap-2">
          <dt className="text-muted-foreground">{label}</dt>
          <dd className="font-medium tabular-nums">{value}</dd>
        </div>
      ))}
    </dl>
  )
}
