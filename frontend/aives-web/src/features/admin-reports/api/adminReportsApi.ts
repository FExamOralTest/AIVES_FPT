import { http } from '@/lib/http/client'
import type { ExamSessionSummary, ResultForReviewDto } from '../types'

export const adminReportsApi = {
  /** Every session in the system: the staff endpoint only narrows to assigned courses for STAFF callers. */
  async listSessions(): Promise<ExamSessionSummary[]> {
    const { data } = await http.get<ExamSessionSummary[]>('/api/staff/sessions')
    return data
  },

  /** All results of one session, whatever their review status. */
  async listResults(sessionId: number): Promise<ResultForReviewDto[]> {
    const { data } = await http.get<ResultForReviewDto[]>(`/api/results/session/${sessionId}`)
    return data
  },
}
