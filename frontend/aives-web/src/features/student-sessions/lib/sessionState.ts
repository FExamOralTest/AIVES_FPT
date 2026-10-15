import { parseApiDate } from '@/lib/format/date'
import type { StudentSessionEnrollment } from '../types'

export type StudentSessionState = 'waiting' | 'ready' | 'inProgress' | 'completed' | 'published' | 'closed' | 'cancelled'
export type SessionFilter = 'all' | 'upcoming' | 'active' | 'completed' | 'closed'

export const SESSION_FILTERS: { value: SessionFilter; label: string }[] = [
  { value: 'all', label: 'Tất cả' },
  { value: 'upcoming', label: 'Chờ bắt đầu' },
  { value: 'active', label: 'Có thể vào thi' },
  { value: 'completed', label: 'Đã hoàn thành' },
  { value: 'closed', label: 'Đã đóng / hủy' },
]

export const STUDENT_SESSION_STATES: Record<StudentSessionState, { label: string; tone: string; filter: SessionFilter }> = {
  waiting: { label: 'Chờ bắt đầu', tone: 'bg-muted text-foreground', filter: 'upcoming' },
  ready: { label: 'Sẵn sàng vào thi', tone: 'bg-success-soft text-success', filter: 'active' },
  inProgress: { label: 'Đang làm bài', tone: 'bg-success-soft text-success', filter: 'active' },
  completed: { label: 'Chờ công bố', tone: 'bg-warning-soft text-warning', filter: 'completed' },
  published: { label: 'Đã công bố kết quả', tone: 'bg-brand-soft text-brand', filter: 'completed' },
  closed: { label: 'Đã đóng', tone: 'bg-muted text-muted-foreground', filter: 'closed' },
  cancelled: { label: 'Đã hủy', tone: 'bg-[#fef1f0] text-destructive', filter: 'closed' },
}

export function getStudentSessionState(session: StudentSessionEnrollment, now: number): StudentSessionState {
  if (session.isPublished) return 'published'
  if (session.enrollmentStatus === 'COMPLETED') return 'completed'
  if (session.sessionStatus === 'CANCELLED' || session.enrollmentStatus === 'CANCELLED') return 'cancelled'
  if (session.sessionStatus === 'COMPLETED' || now >= parseApiDate(session.endTime).getTime()) return 'closed'
  if (session.sessionStatus !== 'IN_PROGRESS' || now < parseApiDate(session.startTime).getTime()) return 'waiting'
  return session.enrollmentStatus === 'IN_PROGRESS' ? 'inProgress' : 'ready'
}
