export interface QuestionItemDto {
  questionId: number
  questionText: string
  level: number
  maxScore?: number | null
  isFollowUpAllowed?: boolean | null
  courseId: number
  courseName?: string
}

export interface GenerateQuestionsRequest {
  questionCount?: number
  focusTopics?: string
}

export interface GenerateQuestionsResponse {
  count?: number
  message?: string
  questions: QuestionItemDto[]
}
