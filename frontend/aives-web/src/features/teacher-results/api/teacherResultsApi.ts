import { http } from '@/lib/http/client'
import type { ResultForReviewDto, ReviewResultRequest } from '../types'

export const teacherResultsApi = {
  async listBySession(sessionId: number): Promise<ResultForReviewDto[]> {
    const { data } = await http.get<ResultForReviewDto[]>(`/api/results/session/${sessionId}`)
    return data
  },

  async get(resultId: number): Promise<ResultForReviewDto> {
    const { data } = await http.get<ResultForReviewDto>(`/api/results/${resultId}`)
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
}
