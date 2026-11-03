/** AI-generated and edited questions start as PENDING_REVIEW; only APPROVED ones can go into an exam. */
export type QuestionApprovalStatus = 'PENDING_REVIEW' | 'APPROVED' | 'REJECTED'

export interface QuestionItemDto {
  questionId: number
  approvalStatus: QuestionApprovalStatus
  reviewedBy?: number | null
  reviewedAt?: string | null
  rejectionReason?: string | null
  questionText: string
  topic?: string | null
  level: number
  maxScore?: number | null
  isFollowUpAllowed?: boolean | null
  courseId: number
  documentId?: number | null
  expectedKeywords?: string[]
  courseName?: string
  sampleAnswer?: string | null
  learningOutcome?: string | null
  rubric?: { description: string; maxScore: number }[]
  isInUse?: boolean
  createdAt?: string | null
}

export interface WriteQuestionInput {
  questionText: string
  topic: string
  sampleAnswer: string
  learningOutcome: string
  rubric: { description: string; maxScore: number }[]
  level: number
  maxScore: number
  isFollowUpAllowed: boolean
  expectedKeywords: string[]
}

export interface QuestionQuery {
  page?: number
  pageSize?: number
  search?: string
  level?: number
  status?: QuestionApprovalStatus
  sortBy?: 'createdAt' | 'questionText' | 'level' | 'maxScore'
  sortDirection?: 'asc' | 'desc'
}

export interface QuestionPage {
  items: QuestionItemDto[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface GenerateQuestionsRequest {
  questionCount?: number
  focusTopics?: string
}

export interface GenerateQuestionsResponse {
  count?: number
  generatedCount?: number
  message?: string
  questions: QuestionItemDto[]
}
