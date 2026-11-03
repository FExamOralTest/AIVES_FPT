/** Result.Status values from the backend, with the badge each one gets. */
export const RESULT_STATUS: Record<string, { label: string; tone: string }> = {
  SUBMITTED: { label: 'Submitted', tone: 'bg-muted text-foreground' },
  PENDING_REVIEW: { label: 'Pending review', tone: 'bg-warning-soft text-warning' },
  REVIEWED: { label: 'Reviewed', tone: 'bg-brand-soft text-brand' },
  PUBLISHED: { label: 'Published', tone: 'bg-success-soft text-success' },
  CANCELLED: { label: 'Cancelled', tone: 'bg-[#fef1f0] text-destructive' },
}
