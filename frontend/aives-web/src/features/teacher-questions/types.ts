export interface QuestionItemDto {
  questionId: number
  questionText: string
  topic?: string | null
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
  generatedCount?: number
  message?: string
  questions: QuestionItemDto[]
}
