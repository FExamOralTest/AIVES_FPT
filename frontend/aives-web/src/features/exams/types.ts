export type ExamStatus = 'DRAFT' | 'SCHEDULED' | 'OPEN' | 'CLOSED' | 'CANCELLED'

export interface ExamQuestion {
  questionId: number
  questionText: string
  questionScore: number
  maxScore: number | null
  isFollowUpAllowed: boolean | null
}

/** GET /api/exams/my, GET /api/exams/{id}, POST /api/exams. */
export interface Exam {
  examId: number
  title: string
  examCode: string
  courseId: number
  courseName: string | null
  createdBy: number
  createdByName: string | null
  durationMinutes: number
  maxQuestions: number
  examStatus: ExamStatus | null
  createdAt: string | null
  /** In the list endpoint question texts are empty; fetch the exam by id for the full list. */
  questions: ExamQuestion[]
}

export interface CreateExamQuestionItem {
  questionId: number
  questionScore: number
}

export interface CreateExamInput {
  title: string
  courseId: number
  durationMinutes: number
  maxQuestions: number
  questions?: CreateExamQuestionItem[]
}

export type DocType = 'CONTENT' | 'LEARNING_OUTCOMES'

/** Result of checking a courseId the teacher typed (there is no "my courses" API yet). */
export type CourseCheck =
  | { ok: true; docTypes: DocType[] }
  | { ok: false; message: string }
