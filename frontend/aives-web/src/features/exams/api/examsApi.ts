import { toApiError } from '@/lib/http/apiError'
import { http } from '@/lib/http/client'
import type { CourseCheck, CreateExamInput, DocType, Exam } from '../types'

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

  async updateQuestions(examId: number, questions: { questionId: number; questionScore: number }[]): Promise<Exam> {
    const { data } = await http.put<Exam>(`/api/exams/${examId}/questions`, { questions })
    return data
  },

  /**
   * Stand-in for the missing GET /api/teacher/courses: the documents endpoint answers 404 for an
   * unknown course and 403 when the teacher isn't assigned to it, and lists the uploaded .md files.
   */
  async checkCourse(courseId: number): Promise<CourseCheck> {
    try {
      const { data } = await http.get<{ docType: DocType }[]>(`/api/courses/${courseId}/documents`)
      return { ok: true, docTypes: [...new Set(data.map((d) => d.docType))] }
    } catch (err) {
      const { status, message } = toApiError(err)
      if (status === 404) return { ok: false, message: 'Không tìm thấy môn học có mã số này.' }
      if (status === 403) return { ok: false, message: 'Bạn chưa được phân công phụ trách môn học này.' }
      return { ok: false, message }
    }
  },
}
