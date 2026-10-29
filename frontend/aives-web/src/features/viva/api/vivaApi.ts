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
   * Nộp câu trả lời. `audioObjectKey` dùng khi audio đã được upload chunked trước đó
   * (R2 đã có 1 file lớn) — khi đó không gửi file audio lần nữa.
   */
  async submitAnswer(
    sessionId: number,
    formData: FormData,
    audioObjectKey?: string,
  ): Promise<SubmitVivaAnswerResponse> {
    if (audioObjectKey) {
      formData.append('audioObjectKey', audioObjectKey)
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
