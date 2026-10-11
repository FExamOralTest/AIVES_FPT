import { http } from '@/lib/http/client'
import type { EnrollSessionRequest, EnrollSessionResponse } from '../types'

export const enrollApi = {
  async enroll(payload: EnrollSessionRequest): Promise<EnrollSessionResponse> {
    const { data } = await http.post<EnrollSessionResponse>('/api/enroll/session', payload)
    return data
  },
}
