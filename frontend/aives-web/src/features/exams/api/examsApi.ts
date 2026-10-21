import { http } from '@/lib/http/client'
import type { CreateExamInput, Exam, UpdateExamInput } from '../types'

export const examsApi = {
  async listMine(): Promise<Exam[]> {
    const { data } = await http.get<Exam[]>('/api/exams/my')
    return data
  },

  async get(examId: number): Promise<Exam> {
    const { data } = await http.get<Exam>(`/api/exams/${examId}`)
    return data
  },

  async create(input: CreateExamInput): Promise<Exam> {
    const { data } = await http.post<Exam>('/api/exams', input)
    return data
  },

  async update(examId: number, input: UpdateExamInput): Promise<Exam> {
    const { data } = await http.put<Exam>(`/api/exams/${examId}`, input)
    return data
  },

  async delete(examId: number): Promise<void> {
    await http.delete(`/api/exams/${examId}`)
  },
}
