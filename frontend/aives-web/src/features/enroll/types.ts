export type SessionStatus = 'NOT_STARTED' | 'IN_PROGRESS' | 'COMPLETED' | 'CANCELLED'

export interface EnrollSessionRequest {
  sessionCode: string
}

/** POST /api/enroll/session → 200. Times are UTC; parse with parseApiDate. */
export interface EnrollSessionResponse {
  sessionEnrollmentId: number
  sessionId: number
  sessionName: string
  examTitle: string
  startTime: string
  endTime: string
  status: SessionStatus
}
