import { http } from '@/lib/http/client'
import type { CourseDocument, TeacherCourse } from '../types'

export const teacherCoursesApi = {
  async listMine(): Promise<TeacherCourse[]> {
    const { data } = await http.get<TeacherCourse[]>('/api/teacher/courses')
    return data
  },

  async getDocuments(courseId: number): Promise<CourseDocument[]> {
    const { data } = await http.get<CourseDocument[]>(`/api/courses/${courseId}/documents`)
    return data
  },

  async uploadDocument(courseId: number, formData: FormData): Promise<CourseDocument> {
    const { data } = await http.post<CourseDocument>(`/api/courses/${courseId}/documents`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },
}
