import { CircleAlert, UserPlus } from 'lucide-react'
import { useId, useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { toApiError } from '@/lib/http/apiError'
import { cn } from '@/lib/utils'
import { sessionsApi } from '../api/sessionsApi'
import type { BulkEnrollResult, EnrollOutcome } from '../types'

const SKIP_LABEL: Record<Exclude<EnrollOutcome, 'added'>, string> = {
  already_enrolled: 'already in this session',
  not_student: 'not a student account',
  not_found: 'no account with this ID',
}

/** Max IDs per request, matching the backend limit. */
const MAX_IDS = 500

/**
 * Adds students by account ID. Staff cannot search users yet (that API is admin-only),
 * so this takes a pasted list of IDs; students can still join on their own with the join code.
 */
export function AddStudentsForm({ sessionId, onAdded }: { sessionId: number; onAdded: () => void }) {
  const inputId = useId()
  const [text, setText] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<BulkEnrollResult | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setResult(null)
    const parts = text.split(/[\s,;]+/).filter(Boolean)
    const invalid = parts.filter((p) => !/^\d+$/.test(p) || Number(p) < 1 || Number(p) > 2147483647)
    if (parts.length === 0) return setError('Enter at least one student ID.')
    if (invalid.length) return setError(`Not a valid ID: ${invalid.slice(0, 5).join(', ')}${invalid.length > 5 ? '…' : ''}`)
    const ids = [...new Set(parts.map(Number))]
    if (ids.length > MAX_IDS) return setError(`Add at most ${MAX_IDS} students at a time.`)

    setError(null)
    setSubmitting(true)
    try {
      const res = await sessionsApi.addStudents(sessionId, ids)
      setResult(res)
      if (res.addedCount > 0) {
        setText('')
        onAdded()
      }
    } catch (err) {
      setError(toApiError(err, 'Could not add the students.').message)
    } finally {
      setSubmitting(false)
    }
  }

  const skipped = result?.results.filter((r) => r.status !== 'added') ?? []

  return (
    <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-2 rounded-[14px] bg-muted/50 p-4">
      <label htmlFor={inputId} className="text-sm font-semibold">
        Add students by ID
      </label>
      <div className="flex flex-col gap-2 sm:flex-row">
        <input
          id={inputId}
          value={text}
          onChange={(e) => {
            setText(e.target.value)
            if (error) setError(null)
          }}
          placeholder="e.g. 12, 15, 21"
          aria-invalid={Boolean(error)}
          aria-describedby={`${inputId}-hint`}
          className={cn(
            'h-10 min-w-0 flex-1 rounded-xl border border-input bg-background px-3.5 text-sm outline-none focus:border-brand focus:ring-4 focus:ring-brand/15',
            error && 'border-destructive ring-3 ring-destructive/20',
          )}
        />
        <Button type="submit" size="sm" disabled={submitting || !text.trim()}>
          <UserPlus aria-hidden="true" />
          {submitting ? 'Adding…' : 'Add'}
        </Button>
      </div>
      <p id={`${inputId}-hint`} className="text-[13px] text-muted-foreground">
        Separate IDs with commas, spaces or new lines.
      </p>

      {error && (
        <p role="alert" className="flex items-center gap-2 text-sm text-destructive">
          <CircleAlert className="size-4 shrink-0" aria-hidden="true" />
          {error}
        </p>
      )}

      {result && (
        <div role="status" className="text-sm">
          <p className={result.addedCount > 0 ? 'text-success' : 'text-muted-foreground'}>
            Added {result.addedCount} student{result.addedCount === 1 ? '' : 's'}.
            {skipped.length > 0 && ` Skipped ${skipped.length}:`}
          </p>
          {skipped.length > 0 && (
            <ul className="mt-1 list-disc pl-5 text-muted-foreground">
              {skipped.map((r) => (
                <li key={r.studentId}>
                  #{r.studentId}: {SKIP_LABEL[r.status as Exclude<EnrollOutcome, 'added'>] ?? r.reason ?? r.status}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </form>
  )
}
