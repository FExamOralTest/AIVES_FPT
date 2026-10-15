export interface AdminCourse {
  courseId: number
  courseCode: string
  courseName: string
  teacherId?: number | null
  teacherName?: string | null
  teacherFullName?: string | null
  createdAt?: string | null
}

export interface CreateCourseInput {
  courseCode: string
  courseName: string
  teacherId?: number | null
  createdBy?: number | null
}
