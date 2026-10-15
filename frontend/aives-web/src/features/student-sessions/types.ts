import type { SessionStatus } from '@/lib/domain/sessionStatus'

export interface StudentSessionEnrollment {
  sessionEnrollmentId: number
  sessionId: number
  sessionName: string
  examTitle: string
  courseName: string
  room: string | null
  startTime: string
  endTime: string
  enrolledAt: string | null
  sessionStatus: SessionStatus
  enrollmentStatus: SessionStatus
  isPublished: boolean
}
