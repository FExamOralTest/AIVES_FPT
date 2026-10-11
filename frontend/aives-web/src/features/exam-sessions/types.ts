import type { SessionStatus } from '@/lib/domain/sessionStatus'

export type { SessionStatus }

/** GET /api/exam-sessions/teacher/{teacherId}: no session code here, load the detail for it. */
export interface ExamSessionSummary {
  sessionId: number
  sessionName: string
  examTitle: string
  examCode: string
  room: string | null
  startTime: string
  endTime: string
  status: SessionStatus
  enrolledStudents: number
}

/** GET /api/exam-sessions/{id}, POST /api/exam-sessions, PUT …/status. */
export interface ExamSession extends ExamSessionSummary {
  examId: number
  durationMinutes: number
  scheduledBy: number
  scheduledByName: string | null
  createdAt: string | null
  /** Enroll code students type, "ESS-XXXXXXXX". */
  sessionCode: string
  maxCapacity: number
}

export interface CreateSessionInput {
  examId: number
  sessionName: string
  room?: string
  /** ISO UTC strings. */
  startTime: string
  endTime: string
  maxCapacity: number
}

export interface StudentProgress {
  sessionEnrollmentId: number
  studentId: number
  studentName: string
  studentEmail: string
  enrollmentStatus: SessionStatus
  enrolledAt: string | null
  startedAt: string | null
  progressPercent: number
}

/** GET /api/exam-sessions/{id}/monitor (only the fields the UI uses). */
export interface SessionMonitor {
  currentEnrollment: number
  maxCapacity: number
  students: StudentProgress[]
}
