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

  /** Spoken audio of the current question. Rejects with 503 when the server has no TTS. */
  async getQuestionAudio(sessionId: number, questionId: number, signal?: AbortSignal): Promise<Blob> {
    const { data } = await http.get<Blob>(`/api/student/viva-sessions/${sessionId}/current-question/audio`, {
      params: { questionId },
      responseType: 'blob',
      timeout: 30000,
      signal,
    })
    return data
  },

  async finish(sessionId: number): Promise<FinishVivaSessionResponse> {
    const { data } = await http.post<FinishVivaSessionResponse>(`/api/student/viva-sessions/${sessionId}/finish`, undefined, { timeout: 310000 })
    return data
  },
}
