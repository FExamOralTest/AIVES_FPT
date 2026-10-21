import { CalendarClock, CircleAlert, CircleCheck, Info, KeyRound } from 'lucide-react'
import { useEffect, useId, useRef, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { formatRelative, formatTimeRange, parseApiDate } from '@/lib/format/date'
import { toApiError } from '@/lib/http/apiError'
import { cn } from '@/lib/utils'
import { enrollApi } from '../api/enrollApi'
import {
  normalizeCodeBody,
  SESSION_CODE_LENGTH,
  SESSION_CODE_PREFIX,
  toSessionCode,
  validateCodeBody,
} from '../lib/sessionCode'
import { SESSION_STATUS_LABEL } from '@/lib/domain/sessionStatus'
import type { EnrollSessionResponse } from '../types'

const ALREADY_ENROLLED = 'You are already enrolled in this session.'

type Notice = { tone: 'error' | 'info'; text: string }

export default function EnrollPage() {
  const [enrolled, setEnrolled] = useState<EnrollSessionResponse | null>(null)

  return (
    <>
      <header>
        <h1 className="font-heading text-3xl font-medium tracking-tight">Join a session</h1>
        <p className="mt-1.5 max-w-2xl text-muted-foreground">
          Enter the join code from your lecturer to register for a viva exam session.
        </p>
      </header>

      <div className="mt-8 max-w-xl">
        {enrolled ? (
          <EnrolledCard session={enrolled} onEnrollAnother={() => setEnrolled(null)} />
        ) : (
          <EnrollForm onEnrolled={setEnrolled} />
        )}
      </div>
    </>
  )
}

function EnrollForm({ onEnrolled }: { onEnrolled: (session: EnrollSessionResponse) => void }) {
  const id = useId()
  const [body, setBody] = useState('')
  const [fieldError, setFieldError] = useState<string>()
  const [notice, setNotice] = useState<Notice | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const error = validateCodeBody(body)
    setFieldError(error)
    setNotice(null)
    if (error) return

    setSubmitting(true)
    try {
      onEnrolled(await enrollApi.enroll({ sessionCode: toSessionCode(body) }))
    } catch (err) {
      const apiError = toApiError(err)
      if (apiError.status === 404) setFieldError(apiError.message)
      else setNotice({ tone: apiError.rawMessage === ALREADY_ENROLLED ? 'info' : 'error', text: apiError.message })
    } finally {
      setSubmitting(false)
    }
  }

  const describedBy = fieldError ? `${id}-error` : `${id}-hint`

  return (
    <>
      <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-6 rounded-3xl border p-6 sm:p-8">
        {notice && (
          <Alert
            variant={notice.tone === 'error' ? 'destructive' : 'default'}
            className={cn(
              'rounded-[14px] border-0 px-4 py-3.5',
              notice.tone === 'error' ? 'bg-[#fef1f0]' : 'bg-brand-soft text-foreground',
            )}
          >
            {notice.tone === 'error' ? <CircleAlert /> : <Info className="text-brand" />}
            <AlertDescription className={notice.tone === 'info' ? 'text-foreground' : undefined}>
              {notice.text}
              {notice.tone === 'info' && (
                <Link to="/student/sessions" className="ml-1 font-medium underline underline-offset-4">See it in My sessions.</Link>
              )}
            </AlertDescription>
          </Alert>
        )}

        <div className="flex flex-col gap-2">
          <Label htmlFor={id} className="text-sm font-semibold">
            Join code
          </Label>
          <div
            className={cn(
              'flex h-14 items-center rounded-[14px] border border-input bg-background pl-4 transition-colors',
              'focus-within:border-brand focus-within:ring-4 focus-within:ring-brand/15',
              fieldError && 'border-destructive ring-3 ring-destructive/20',
            )}
          >
            <span aria-hidden="true" className="font-mono text-lg tracking-[0.2em] text-muted-foreground select-none">
              {SESSION_CODE_PREFIX}
            </span>
            <input
              id={id}
              value={body}
              onChange={(e) => {
                setBody(normalizeCodeBody(e.target.value))
                if (fieldError) setFieldError(undefined)
              }}
              aria-label={`Join code, ${SESSION_CODE_LENGTH} characters after ${SESSION_CODE_PREFIX}`}
              aria-invalid={Boolean(fieldError)}
              aria-describedby={describedBy}
              placeholder="1A2B3C4D"
              autoComplete="off"
              autoCapitalize="characters"
              spellCheck={false}
              autoFocus
              className="h-full w-full min-w-0 bg-transparent pr-4 font-mono text-lg tracking-[0.2em] uppercase outline-none placeholder:text-muted-foreground/50"
            />
          </div>
          {fieldError ? (
            <p id={`${id}-error`} className="text-[13px] text-destructive">
              {fieldError}
            </p>
          ) : (
            <p id={`${id}-hint`} className="text-[13px] text-muted-foreground">
              Enter the {SESSION_CODE_LENGTH} characters after "{SESSION_CODE_PREFIX}". You can also paste the whole code, e.g. ESS-1A2B3C4D.
            </p>
          )}
        </div>

        <Button type="submit" disabled={submitting}>
          {submitting ? 'Checking code…' : 'Join session'}
        </Button>
      </form>

      <section aria-labelledby={`${id}-help`} className="mt-6 rounded-2xl bg-muted p-5 text-sm">
        <h2 id={`${id}-help`} className="flex items-center gap-2 font-medium">
          <KeyRound className="size-4" aria-hidden="true" />
          Where do I find the join code?
        </h2>
        <ul className="mt-2 list-disc space-y-1 pl-5 text-muted-foreground">
          <li>Your lecturer sends the code before the exam. Each code is for one session.</li>
          <li>You can only join a session that has not started yet or is in progress.</li>
          <li>The code is not case-sensitive when you type it.</li>
        </ul>
      </section>
    </>
  )
}

function EnrolledCard({
  session,
  onEnrollAnother,
}: {
  session: EnrollSessionResponse
  onEnrollAnother: () => void
}) {
  const headingRef = useRef<HTMLHeadingElement>(null)
  // Move focus to the result so screen readers announce it after the form disappears.
  useEffect(() => headingRef.current?.focus(), [])
  // Captured once: the card is a confirmation snapshot, not a live countdown.
  const [now] = useState(() => new Date())

  const start = parseApiDate(session.startTime)
  const end = parseApiDate(session.endTime)
  const started = session.status === 'IN_PROGRESS' || start <= now

  return (
    <section aria-labelledby="enrolled-heading" className="rounded-3xl border p-6 sm:p-8">
      <p className="flex items-center gap-2 text-sm font-medium text-success">
        <CircleCheck className="size-5" aria-hidden="true" />
        You joined the session
      </p>
      <h2 id="enrolled-heading" ref={headingRef} tabIndex={-1} className="mt-3 font-heading text-2xl font-medium outline-none">
        {session.examTitle || 'Viva exam session'}
      </h2>
      <p className="mt-1 text-muted-foreground">Session: {session.sessionName}</p>

      <dl className="mt-6 divide-y rounded-2xl bg-muted px-5">
        <div className="flex flex-col gap-1 py-3.5 sm:flex-row sm:gap-4">
          <dt className="w-28 shrink-0 text-muted-foreground">ID ca thi</dt>
          <dd className="font-mono font-medium">{session.sessionId}</dd>
        </div>
        <div className="flex flex-col gap-1 py-3.5 sm:flex-row sm:gap-4">
          <dt className="w-28 shrink-0 text-muted-foreground">Time</dt>
          <dd className="font-medium">{formatTimeRange(start, end)}</dd>
        </div>
        <div className="flex flex-col gap-1 py-3.5 sm:flex-row sm:gap-4">
          <dt className="w-28 shrink-0 text-muted-foreground">Status</dt>
          <dd className="flex flex-wrap items-center gap-2 font-medium">
            {SESSION_STATUS_LABEL[session.status] ?? session.status}
            {!started && (
              <span className="inline-flex items-center gap-1 text-sm font-normal text-muted-foreground">
                <CalendarClock className="size-4" aria-hidden="true" />
                Starts {formatRelative(start, now)}
              </span>
            )}
          </dd>
        </div>
      </dl>

      <p className="mt-6 text-sm text-muted-foreground">
        When the session starts, go to the <span className="font-medium text-foreground">Viva room</span> to answer
        the AI's questions by voice. Get your microphone ready and find a quiet place before the exam.
      </p>

      <div className="mt-6 flex flex-col gap-2 sm:flex-row">
        <Button asChild>
          <Link to="/student/sessions">
            <CalendarClock aria-hidden="true" />
            My sessions
          </Link>
        </Button>
        <Button variant="secondary" onClick={onEnrollAnother}>
          Enter another code
        </Button>
      </div>
    </section>
  )
}
