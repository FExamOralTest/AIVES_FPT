import type { QuestionApprovalStatus } from '../types'

export const APPROVAL_STATUSES: QuestionApprovalStatus[] = ['PENDING_REVIEW', 'APPROVED', 'REJECTED']

export const APPROVAL_LABEL: Record<QuestionApprovalStatus, string> = {
  PENDING_REVIEW: 'Pending review',
  APPROVED: 'Approved',
  REJECTED: 'Rejected',
}

export const APPROVAL_TONE: Record<QuestionApprovalStatus, string> = {
  PENDING_REVIEW: 'bg-warning-soft text-warning',
  APPROVED: 'bg-success-soft text-success',
  REJECTED: 'bg-[#fef1f0] text-destructive',
}

export const REJECTION_REASON_MAX = 2000
