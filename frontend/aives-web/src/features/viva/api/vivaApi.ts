import { http } from '@/lib/http/client'
import type {
  FinishVivaSessionResponse,
  StartVivaSessionResponse,
  SubmitVivaAnswerResponse,
} from '../types'

export const vivaApi = {
  async start(sessionId: number): Promise<StartVivaSessionResponse> {
    const { data } = await http.post<StartVivaSessionResponse>(`/api/student/viva-sessions/${sessionId}/start`)
    return data
  },

  /**
   * Nộp câu trả lời. `audioUploadId` là uploadId của phiên chunked đã COMPLETED
   * (server đã có 1 file lớn trên R2) — khi đó không gửi file audio lần nữa.
   */
  async submitAnswer(
    sessionId: number,
    formData: FormData,
    audioUploadId?: string,
  ): Promise<SubmitVivaAnswerResponse> {
    if (audioUploadId) {
      formData.append('audioUploadId', audioUploadId)
    }
    const { data } = await http.post<SubmitVivaAnswerResponse>(
      `/api/student/viva-sessions/${sessionId}/submit-answer`,
      formData,
      {
        headers: { 'Content-Type': 'multipart/form-data' },
      },
    )
    return data
  },

  async finish(sessionId: number): Promise<FinishVivaSessionResponse> {
    const { data } = await http.post<FinishVivaSessionResponse>(`/api/student/viva-sessions/${sessionId}/finish`)
    return data
  },
}
