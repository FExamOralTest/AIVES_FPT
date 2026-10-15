import { http } from '@/lib/http/client'
import type {
  GenerateQuestionsRequest,
  GenerateQuestionsResponse,
  QuestionItemDto,
} from '../types'

export const teacherQuestionsApi = {
  async getQuestions(
    courseId: number,
    query: { page?: number; pageSize?: number; search?: string } = {},
  ): Promise<{ items: QuestionItemDto[]; totalCount: number }> {
    const { data } = await http.get<{
      items: {
        questionId: number
        questionText: string
        topic?: string
        level?: number
        maxScore?: number
        isFollowUpAllowed?: boolean
      }[]
      totalCount: number
    }>(`/api/courses/${courseId}/questions`, {
      params: { page: query.page ?? 1, pageSize: query.pageSize ?? 100, search: query.search },
    })

    return {
      items: (data.items ?? []).map((q) => ({
        questionId: q.questionId,
        questionText: q.questionText,
        topic: q.topic,
        level: q.level ?? 2,
        maxScore: q.maxScore ?? 1.0,
        isFollowUpAllowed: q.isFollowUpAllowed ?? true,
        courseId,
      })),
      totalCount: data.totalCount ?? 0,
    }
  },

  async generateQuestions(
    courseId: number,
    body: GenerateQuestionsRequest = {},
  ): Promise<GenerateQuestionsResponse> {
    const { data } = await http.post<GenerateQuestionsResponse>(
      `/api/courses/${courseId}/documents/generate-questions`,
      {
        courseId,
        questionCount: body.questionCount ?? 10,
        focusTopics: body.focusTopics ?? '',
        maxScore: 1.0,
      },
    )
    return data
  },

  async suggestQuestions(
    courseId: number,
    maxSuggestions = 20,
  ): Promise<{ suggestedQuestions: QuestionItemDto[]; aiRationale?: string }> {
    const { data } = await http.post<{
      suggestedQuestions: {
        questionId: number
        questionText: string
        level?: number
        maxScore?: number
        isFollowUpAllowed?: boolean
      }[]
      aiRationale?: string
    }>('/api/exams/ai-suggest-questions', {
      courseId,
      maxSuggestions,
    })

    return {
      suggestedQuestions: data.suggestedQuestions.map((q) => ({
        questionId: q.questionId,
        questionText: q.questionText,
        level: q.level ?? 2,
        maxScore: q.maxScore ?? 1.0,
        isFollowUpAllowed: q.isFollowUpAllowed ?? true,
        courseId,
      })),
      aiRationale: data.aiRationale,
    }
  },
}
