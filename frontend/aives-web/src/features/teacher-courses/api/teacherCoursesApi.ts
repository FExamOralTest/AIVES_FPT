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

  async getDocument(courseId: number, documentId: number): Promise<CourseDocument> {
    const { data } = await http.get<CourseDocument>(`/api/courses/${courseId}/documents/${documentId}`)
    return data
  },

  async uploadDocument(courseId: number, formData: FormData): Promise<CourseDocument> {
    const { data } = await http.post<CourseDocument>(`/api/courses/${courseId}/documents`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async updateDocument(courseId: number, documentId: number, formData: FormData): Promise<CourseDocument> {
    const { data } = await http.put<CourseDocument>(`/api/courses/${courseId}/documents/${documentId}`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async deleteDocument(courseId: number, documentId: number): Promise<void> {
    await http.delete(`/api/courses/${courseId}/documents/${documentId}`)
  },
}
