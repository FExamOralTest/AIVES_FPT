export type DocType = 'CONTENT' | 'LEARNING_OUTCOMES'

export interface CourseDocument {
  documentId: number
  courseId: number
  fileName: string
  docType: DocType
  uploadedAt: string
  content?: string
}

export interface TeacherCourse {
  courseId: number
  courseCode: string
  courseName: string
  teacherId?: number | null
  teacherName?: string | null
  teacherFullName?: string | null
}
