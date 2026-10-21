import type { ExamStatus } from '../types'

export const EXAM_STATUS_LABEL: Record<ExamStatus, string> = {
  DRAFT: 'Draft',
  SCHEDULED: 'Scheduled',
  OPEN: 'Open',
  CLOSED: 'Closed',
  CANCELLED: 'Cancelled',
}

export const EXAM_STATUS_TONE: Record<ExamStatus, string> = {
  DRAFT: 'bg-muted text-foreground',
  SCHEDULED: 'bg-brand-soft text-brand',
  OPEN: 'bg-success-soft text-success',
  CLOSED: 'bg-muted text-muted-foreground',
  CANCELLED: 'bg-[#fef1f0] text-destructive',
}
