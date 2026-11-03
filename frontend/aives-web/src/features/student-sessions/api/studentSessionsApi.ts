import { http } from '@/lib/http/client'
import type { StudentSessionEnrollment } from '../types'

export const studentSessionsApi = {
  async listMine(): Promise<StudentSessionEnrollment[]> {
    const { data } = await http.get<StudentSessionEnrollment[]>('/api/student/enrollments')
    return data
  },
}
