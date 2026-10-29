import type { SessionStatus } from '@/lib/domain/sessionStatus'

export interface StudentSessionEnrollment {
  sessionEnrollmentId: number
  sessionId: number
  sessionName: string
  examTitle: string
  examCode?: string
  courseName: string
  room: string | null
  startTime: string
  endTime: string
  enrolledAt: string | null
  sessionStatus: SessionStatus
  enrollmentStatus: SessionStatus
  isPublished: boolean
  assignedSlotId?: number | null
  assignedSlotName?: string | null
  slotStartTime?: string | null
  slotEndTime?: string | null
  retakeEndsAt?: string | null
  retakeId?: number | null
  originalResultId?: number | null
  retakeReason?: string | null
  retakeDurationMinutes?: number | null
  replacementSessionId?: number | null
}
