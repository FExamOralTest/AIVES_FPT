export type ExamStatus = 'DRAFT' | 'SCHEDULED' | 'OPEN' | 'CLOSED' | 'CANCELLED'

export interface ExamQuestion {
  questionId: number
  questionText: string
  questionScore: number
  maxScore: number | null
  isFollowUpAllowed: boolean | null
}
export interface Exam {
  canEdit: boolean
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
export interface UpdateExamInput {
  title: string
  durationMinutes: number
  maxQuestions: number
  questions: CreateExamQuestionItem[]
}
