import { http } from '@/lib/http/client'
import type { CreateExamPeriodRequest, ExamPeriod } from '../types'

export const periodsApi = {
  async list(courseId?: number): Promise<ExamPeriod[]> {
    const query = courseId ? `?courseId=${courseId}` : ''
    const { data } = await http.get<ExamPeriod[]>(`/api/exam-periods${query}`)
    return data
  },

  async create(input: CreateExamPeriodRequest): Promise<ExamPeriod> {
    const { data } = await http.post<ExamPeriod>('/api/exam-periods', input)
    return data
  },
}
