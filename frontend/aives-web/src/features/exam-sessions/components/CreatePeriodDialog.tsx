import { AlertCircle, Calendar, Loader2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { FormField } from '@/components/common/FormField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogTitle,
} from '@/components/ui/dialog'
import { toApiError } from '@/lib/http/apiError'
import { periodsApi } from '../api/periodsApi'
import type { ExamPeriod } from '../types'

interface CreatePeriodDialogProps {
  open: boolean
  courseId: number
  courseName?: string | null
  onClose: () => void
  onCreated: (period: ExamPeriod) => void
}

function todayIso() {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

function defaultEndIso() {
  const d = new Date()
  d.setDate(d.getDate() + 90) // ~3 months term
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

export function CreatePeriodDialog({
  open,
  courseId,
  courseName,
  onClose,
  onCreated,
}: CreatePeriodDialogProps) {
  const [periodName, setPeriodName] = useState('')
  const [description, setDescription] = useState('')
  const [startDate, setStartDate] = useState(todayIso())
  const [endDate, setEndDate] = useState(defaultEndIso())
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const name = periodName.trim()
    if (!name) {
      setError('Please provide an exam period name.')
      return
    }

    if (!startDate || !endDate) {
      setError('Please specify both start date and end date.')
      return
    }

    if (new Date(startDate) > new Date(endDate)) {
      setError('Start date cannot be later than end date.')
      return
    }

    if (!courseId) {
      setError('A course is required to create an exam period.')
      return
    }

    setSubmitting(true)
    setError(null)

    try {
      const created = await periodsApi.create({
        courseId,
        periodName: name,
        description: description.trim() || undefined,
        startDate: new Date(startDate).toISOString(),
        endDate: new Date(endDate).toISOString(),
      })
      onCreated(created)
      onClose()
    } catch (err) {
      setError(toApiError(err, 'Failed to create exam period.').message)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-md p-6">
        <div className="flex items-center gap-2">
          <Calendar className="size-5 text-brand" />
          <DialogTitle className="text-lg font-bold">New Exam Period</DialogTitle>
        </div>
        <DialogDescription className="text-xs text-muted-foreground">
          Create an academic term / exam period for {courseName ? <strong className="text-foreground">{courseName}</strong> : 'this course'}.
        </DialogDescription>

        {error && (
          <Alert variant="destructive" className="py-2.5 my-2">
            <AlertCircle className="size-4" />
            <AlertDescription className="text-xs">{error}</AlertDescription>
          </Alert>
        )}

        <form onSubmit={handleSubmit} className="space-y-4 pt-2">
          <FormField
            label="Period / Semester Name"
            placeholder="e.g. Fall 2026 - Final Term"
            value={periodName}
            required
            onChange={(e) => setPeriodName(e.target.value)}
          />

          <FormField
            label="Description (Optional)"
            placeholder="e.g. Main official examination batch"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />

          <div className="grid grid-cols-2 gap-3">
            <FormField
              label="Start Date"
              type="date"
              value={startDate}
              required
              onChange={(e) => setStartDate(e.target.value)}
            />
            <FormField
              label="End Date"
              type="date"
              value={endDate}
              required
              onChange={(e) => setEndDate(e.target.value)}
            />
          </div>

          <DialogFooter className="pt-2 gap-2">
            <Button type="button" variant="outline" size="sm" onClick={onClose} disabled={submitting}>
              Cancel
            </Button>
            <Button type="submit" size="sm" disabled={submitting}>
              {submitting ? (
                <>
                  <Loader2 className="size-3.5 mr-1.5 animate-spin" />
                  Creating…
                </>
              ) : (
                'Create Period'
              )}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
