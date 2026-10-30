export type AnswerStatusType = 'PROCESSING' | 'EVALUATED' | 'ERROR'

export interface CriterionAnalysis {
  criterionId: string
  awardedScore: number
  judgment: 'met' | 'partial' | 'not_met'
  explanation: string
  errors: string[]
  omissions: string[]
  evidenceQuotes: string[]
}

export interface StoredAnswerAnalysis {
  schemaVersion: string
  provider: string
  model: string
  gradedAt: string
  transcript: string
  rubric: { criterionId: string; description: string; maxScore: number }[]
  evaluation: {
    questionId: string
    questionText: string
    studentAnswer: string
    score: number
    maxScore: number
    feedback: string
    strengths: string[]
    weaknesses: string[]
    missingContent: string[]
    sentenceFeedback: { sentence: string; judgment: string; comment: string }[]
    criterionAnalyses: CriterionAnalysis[]
    improvements: string[]
  }
}

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
  aiAnalysis?: StoredAnswerAnalysis | null
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
  aiEvaluationStatus?: string | null
  aiEvaluationError?: string | null
  finalScore?: number | null
  finalFeedback?: string | null
  aiAnalysis?: {
    schemaVersion?: string
    provider?: string
    model?: string
    gradedAt?: string
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
