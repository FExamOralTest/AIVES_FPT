import { http } from '@/lib/http/client'
import type { StudentResultDto } from '../types'

export const studentResultsApi = {
  async get(sessionId: number): Promise<StudentResultDto> {
    const { data } = await http.get<StudentResultDto>(`/api/student/results/${sessionId}`)
    return data
  },
}
