import type { SessionStatus } from '@/lib/domain/sessionStatus'

export type { SessionStatus }

/** Query for GET /api/admin/reports/summary; every field is optional, none set = the whole system. */
export interface StatisticsFilters {
  courseId: number | null
  /** "YYYY-MM-DD" from a date input, read as local days. */
  fromDate: string
  toDate: string
  sessionStatus: SessionStatus | null
}

/** GET /api/admin/reports/summary. Scores are null when nothing has been scored yet. */
export interface ReportSummary {
  totalSessions: number
  activeSessions: number
  completedSessions: number
  upcomingSessions: number
  cancelledSessions: number
  totalEnrollments: number
  totalResults: number
  completedAttempts: number
  averageAiScore: number | null
  averageFinalScore: number | null
  highestFinalScore: number | null
  lowestFinalScore: number | null
  submittedCount: number
  pendingReviewCount: number
  reviewedCount: number
  publishedCount: number
  cancelledResultCount: number
  processingAnswerCount: number
  evaluatedAnswerCount: number
  errorAnswerCount: number
  fromDate: string | null
  toDate: string | null
  generatedAt: string
}
