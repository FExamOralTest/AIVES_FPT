import { parseApiDate } from '@/lib/format/date'
import type { StudentSessionEnrollment } from '../types'

export type StudentSessionState = 'waiting' | 'ready' | 'inProgress' | 'completed' | 'published' | 'closed' | 'cancelled'
export type SessionFilter = 'all' | 'upcoming' | 'active' | 'completed' | 'closed'

export const SESSION_FILTERS: { value: SessionFilter; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'upcoming', label: 'Upcoming' },
  { value: 'active', label: 'Open now' },
  { value: 'completed', label: 'Completed' },
  { value: 'closed', label: 'Closed / cancelled' },
]

export const STUDENT_SESSION_STATES: Record<StudentSessionState, { label: string; tone: string; filter: SessionFilter }> = {
  waiting: { label: 'Not started', tone: 'bg-muted text-foreground', filter: 'upcoming' },
  ready: { label: 'Ready to start', tone: 'bg-success-soft text-success', filter: 'active' },
  inProgress: { label: 'In progress', tone: 'bg-success-soft text-success', filter: 'active' },
  completed: { label: 'Awaiting result', tone: 'bg-warning-soft text-warning', filter: 'completed' },
  published: { label: 'Result published', tone: 'bg-brand-soft text-brand', filter: 'completed' },
  closed: { label: 'Closed', tone: 'bg-muted text-muted-foreground', filter: 'closed' },
  cancelled: { label: 'Cancelled', tone: 'bg-[#fef1f0] text-destructive', filter: 'closed' },
}

export function getStudentSessionState(session: StudentSessionEnrollment, now: number): StudentSessionState {
  if (session.isPublished) return 'published'
  if (session.enrollmentStatus === 'COMPLETED') return 'completed'
  if (session.sessionStatus === 'CANCELLED' || session.enrollmentStatus === 'CANCELLED') return 'cancelled'
  const rawEnd = session.retakeEndsAt ?? session.slotEndTime ?? session.endTime
  const effectiveEnd = parseApiDate(rawEnd).getTime()
  const effectiveStart = session.slotStartTime ? parseApiDate(session.slotStartTime).getTime() : parseApiDate(session.startTime).getTime()

  if (session.sessionStatus === 'COMPLETED' || now >= effectiveEnd) return 'closed'
  if (session.sessionStatus !== 'IN_PROGRESS' || now < effectiveStart) return 'waiting' 
  return session.enrollmentStatus === 'IN_PROGRESS' ? 'inProgress' : 'ready'
}
