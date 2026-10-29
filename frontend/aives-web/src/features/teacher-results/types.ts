export type AnswerStatusType = 'PROCESSING' | 'EVALUATED' | 'ERROR'

export interface AnswerForReviewDto {
  answerId: number
  status?: AnswerStatusType | string
  errorMessage?: string | null
  questionId: number
  questionText: string
  maxScore?: number | null
  studentAnswer?: string | null
  transcript?: string | null
  audioUrl?: string | null
  aiScore?: number | null
  aiFeedback?: string | null
  finalScore?: number | null
  teacherComment?: string | null
  isFollowUp: boolean
}

export interface ResultForReviewDto {
  resultId: number
  sessionId: number
  studentId: number
  studentName: string
  studentEmail: string
  sessionName: string
  examTitle: string
  aiScore?: number | null
  aiFeedback?: string | null
  finalScore?: number | null
  finalFeedback?: string | null
  aiAnalysis?: {
    maxTotalScore: number
    percentage: number
    studySuggestions: string[]
  } | null
  status: string
  submittedAt?: string | null
  reviewedAt?: string | null
  answers: AnswerForReviewDto[]
}

export interface ReviewResultRequest {
  finalScore?: number | null
  finalFeedback?: string | null
  answerReviews: {
    answerId: number
    finalScore?: number | null
    teacherComment?: string | null
  }[]
}

export interface GrantRetakeRequest { reason: string; startTime: string; endTime: string; durationMinutes: number; examId?: number }
export interface RetakeDto {
  retakeId: number; originalResultId: number; originalStatus: string; retakeSessionId: number; retakeResultId: number | null;
  studentId: number; grantedBy: number; reason: string; grantedAt: string; startTime: string; endTime: string;
  durationMinutes: number; consumedAt: string | null; status: string; resultStatus: string | null; finalScore: number | null
}
