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

/** Button text, confirmation text and tone for moving an exam to each status. */
export const EXAM_STATUS_ACTION: Record<ExamStatus, { label: string; confirm: string; destructive: boolean }> = {
  DRAFT: {
    label: 'Back to draft',
    confirm: 'The exam returns to draft. You can edit it again if it has no sessions.',
    destructive: false,
  },
  SCHEDULED: {
    label: 'Schedule',
    confirm: 'The exam is marked as scheduled. Its questions are locked and can no longer be edited.',
    destructive: false,
  },
  OPEN: {
    label: 'Open exam',
    confirm: 'The exam is marked as open. Its questions are locked, and once open it can only be closed.',
    destructive: false,
  },
  CLOSED: {
    label: 'Close exam',
    confirm: 'The exam is closed. This cannot be undone.',
    destructive: true,
  },
  CANCELLED: {
    label: 'Cancel exam',
    confirm: 'The exam is cancelled. This cannot be undone.',
    destructive: true,
  },
}
