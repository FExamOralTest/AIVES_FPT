import { http } from '@/lib/http/client'
import type { AdminCourse, CreateCourseInput } from '../types'

export const adminCoursesApi = {
  async list(): Promise<AdminCourse[]> {
    const { data } = await http.get<AdminCourse[]>('/api/admin/courses')
    return data
  },

  async create(body: CreateCourseInput): Promise<AdminCourse> {
    const { data } = await http.post<AdminCourse>('/api/admin/courses', body)
    return data
  },

  async assignTeacher(courseId: number, teacherId: number): Promise<AdminCourse> {
    const { data } = await http.put<AdminCourse>(`/api/admin/courses/${courseId}/assign-teacher`, {
      teacherId,
    })
    return data
  },

  async delete(courseId: number): Promise<void> {
    await http.delete(`/api/admin/courses/${courseId}`)
  },
}
