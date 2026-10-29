import { http } from '@/lib/http/client'
import type { ResultForReviewDto, ReviewResultRequest, RetakeDto, GrantRetakeRequest } from '../types'

export const teacherResultsApi = {
  async retakeHistory(resultId: number): Promise<RetakeDto[]> {
    return (await http.get<RetakeDto[]>(`/api/results/${resultId}/retake-history`)).data
  },
  async grantRetake(resultId: number, body: GrantRetakeRequest): Promise<RetakeDto> {
    return (await http.post<RetakeDto>(`/api/results/${resultId}/retake`, body)).data
  },
  async rescheduleRetake(retakeId: number, body: GrantRetakeRequest): Promise<RetakeDto> {
    return (await http.put<RetakeDto>(`/api/retakes/${retakeId}/schedule`, body)).data
  },
  /** Results in PENDING_REVIEW or REVIEWED, newest first. */
  async listPending(): Promise<ResultForReviewDto[]> {
    const { data } = await http.get<ResultForReviewDto[]>('/api/results/pending')
    return data
  },

  async listBySession(sessionId: number): Promise<ResultForReviewDto[]> {
    const { data } = await http.get<ResultForReviewDto[]>(`/api/results/session/${sessionId}`)
    return data
  },

  async review(resultId: number, body: ReviewResultRequest): Promise<ResultForReviewDto> {
    const { data } = await http.put<ResultForReviewDto>(`/api/results/${resultId}/review`, body)
    return data
  },

  async publish(resultId: number): Promise<ResultForReviewDto> {
    const { data } = await http.post<ResultForReviewDto>(`/api/results/${resultId}/publish`, {})
    return data
  },

  async retryAnswer(answerId: number, reason?: string): Promise<{ jobId: string; status: string }> {
    const { data } = await http.post<{ jobId: string; status: string }>(`/api/answers/${answerId}/retry`, { reason })
    return data
  },
}
