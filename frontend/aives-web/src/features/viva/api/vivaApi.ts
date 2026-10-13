import { http } from '@/lib/http/client'
import type {
  FinishVivaSessionResponse,
  StartVivaSessionResponse,
  SubmitVivaAnswerResponse,
  VivaQuestionDto,
} from '../types'

export const vivaApi = {
  async start(sessionId: number): Promise<StartVivaSessionResponse> {
    const { data } = await http.post<StartVivaSessionResponse>(`/api/student/viva-sessions/${sessionId}/start`)
    return data
  },

  async getCurrentQuestion(sessionId: number): Promise<VivaQuestionDto> {
    const { data } = await http.get<VivaQuestionDto>(`/api/student/viva-sessions/${sessionId}/current-question`)
    return data
  },

  async submitAnswer(sessionId: number, formData: FormData): Promise<SubmitVivaAnswerResponse> {
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
