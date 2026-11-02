import type { StoredAnswerAnalysis } from '@/features/teacher-results/types'

export interface StudentAnswerItemDto {
  answerId: number
  questionId: number
  questionText: string
  studentAnswer?: string | null
  transcript?: string | null
  audioUrl?: string | null
  finalScore?: number | null
  teacherComment?: string | null
  isFollowUp: boolean
  aiAnalysis?: StoredAnswerAnalysis | null
}

export interface StudentResultDto {
  resultId: number
  sessionId: number
  sessionName: string
  examTitle: string
  status: string
  isPublished: boolean
  message?: string | null
  finalScore?: number | null
  finalFeedback?: string | null
  submittedAt?: string | null
  publishedAt?: string | null
  answers: StudentAnswerItemDto[]
}
