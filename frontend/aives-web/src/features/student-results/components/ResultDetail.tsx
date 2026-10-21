import { Award, Calendar, CheckCircle2, Clock, MessageSquare, Volume2 } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { formatDateTime, parseApiDate } from '@/lib/format/date'
import type { StudentResultDto } from '../types'

/** One exam result: score and feedback once published, a waiting notice before that. */
export function ResultDetail({ result }: { result: StudentResultDto }) {
  return (
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
  )
}
