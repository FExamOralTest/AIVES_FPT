export interface StartVivaSessionResponse {
  sessionId: number
  sessionName: string
  examTitle: string
  assignedExamId?: number
  assignedExamCode?: string
  assignedSlotId?: number
  assignedSlotName?: string
  slotStartTime?: string
  slotEndTime?: string
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
}

export interface FinishVivaSessionResponse {
  sessionId: number
  studentId: number
  status: string
  message: string
  submittedAt: string
  aiScore?: number
}
