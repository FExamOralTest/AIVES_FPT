export type AnswerStatusType = 'PROCESSING' | 'EVALUATED' | 'ERROR'

export interface AnswerAudioPlaybackDto {
  answerId: number
  resultId: number
  playbackUrl: string
  expiresAt: string
  contentType?: string
}

export interface RetryAnswerResponseDto {
  answerId: number
  jobId: string
  status: string
  previousStatus: string
  enqueuedAt: string
}

export interface AnswerForReviewDto {
  answerId: number
  status?: AnswerStatusType | string
  errorMessage?: string | null
  questionId: number
  questionText: string
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
