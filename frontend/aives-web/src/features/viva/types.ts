export interface StartVivaSessionResponse {
  sessionId: number
  sessionName: string
  examTitle: string
  durationMinutes: number
  totalQuestions: number
  currentQuestionIndex: number
  currentQuestion: VivaQuestionDto | null
  startedAt: string | null
}

export interface VivaQuestionDto {
  questionId: number
  questionIndex: number
  totalQuestions: number
  questionText: string
  level: number
  maxScore: number | null
  isFollowUpAllowed: boolean
  isFollowUp: boolean
}

export interface SubmitVivaAnswerResponse {
  success: boolean
  message?: string
  transcript?: string
  shouldFollowUp: boolean
  followUpQuestion?: string
  currentQuestionIndex: number
  totalQuestions: number
  isCompleted: boolean
  nextQuestion?: VivaQuestionDto | null
  /** PK của Answer vừa được tạo/cập nhật. */
  answerId: number
}

export interface FinishVivaSessionResponse {
  sessionId: number
  studentId: number
  status: string
  message: string
  submittedAt: string
  aiScore?: number
}
