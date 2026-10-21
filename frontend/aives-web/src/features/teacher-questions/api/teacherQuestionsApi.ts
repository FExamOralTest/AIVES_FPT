import { http } from '@/lib/http/client'
import type {
  GenerateQuestionsRequest,
  GenerateQuestionsResponse,
  QuestionApprovalStatus,
  QuestionItemDto,
  QuestionPage,
  QuestionQuery,
  WriteQuestionInput,
} from '../types'

export const teacherQuestionsApi = {
  async getQuestions(courseId: number, query: QuestionQuery = {}): Promise<QuestionPage> {
    const { data } = await http.get<QuestionPage>(`/api/courses/${courseId}/questions`, { params: query })
    return data
  },

  async getAllQuestions(courseId: number, status?: QuestionApprovalStatus): Promise<QuestionItemDto[]> {
    const first = await this.getQuestions(courseId, { page: 1, pageSize: 100, status })
    const items = [...first.items]
    for (let page = 2; page <= first.totalPages; page++) {
      items.push(...(await this.getQuestions(courseId, { page, pageSize: 100, status })).items)
    }
    return items
  },

  /** How many questions are in each review state (three 1-item page requests; the API has no counts endpoint). */
  async countByStatus(courseId: number): Promise<Record<QuestionApprovalStatus, number>> {
    const statuses: QuestionApprovalStatus[] = ['PENDING_REVIEW', 'APPROVED', 'REJECTED']
    const pages = await Promise.all(statuses.map((status) => this.getQuestions(courseId, { page: 1, pageSize: 1, status })))
    return { PENDING_REVIEW: pages[0].totalCount, APPROVED: pages[1].totalCount, REJECTED: pages[2].totalCount }
  },

  /** Only PENDING_REVIEW questions can be approved or rejected (409 otherwise). */
  async approve(courseId: number, questionId: number): Promise<QuestionItemDto> {
    const { data } = await http.post<QuestionItemDto>(`/api/courses/${courseId}/questions/${questionId}/approve`)
    return data
  },

  async reject(courseId: number, questionId: number, reason: string): Promise<QuestionItemDto> {
    const { data } = await http.post<QuestionItemDto>(`/api/courses/${courseId}/questions/${questionId}/reject`, { reason })
    return data
  },

  async get(courseId: number, questionId: number): Promise<QuestionItemDto> {
    const { data } = await http.get<QuestionItemDto>(`/api/courses/${courseId}/questions/${questionId}`)
    return data
  },

  async create(courseId: number, input: WriteQuestionInput): Promise<QuestionItemDto> {
    const { data } = await http.post<QuestionItemDto>(`/api/courses/${courseId}/questions`, input)
    return data
  },

  async update(courseId: number, questionId: number, input: WriteQuestionInput): Promise<QuestionItemDto> {
    const { data } = await http.put<QuestionItemDto>(`/api/courses/${courseId}/questions/${questionId}`, input)
    return data
  },

  async delete(courseId: number, questionId: number): Promise<void> {
    await http.delete(`/api/courses/${courseId}/questions/${questionId}`)
  },

  async generateQuestions(courseId: number, body: GenerateQuestionsRequest = {}): Promise<GenerateQuestionsResponse> {
    const { data } = await http.post<GenerateQuestionsResponse>(`/api/courses/${courseId}/documents/generate-questions`, {
      questionCount: body.questionCount ?? 10, focusTopics: body.focusTopics ?? '', maxScore: 1,
    }, { timeout: 310000 })
    return data
  },

  async suggestQuestions(courseId: number, maxSuggestions = 20): Promise<{ suggestedQuestions: QuestionItemDto[]; rationale: string }> {
    const { data } = await http.post<{ suggestedQuestions: QuestionItemDto[]; rationale: string }>('/api/exams/suggest-questions', {
      courseId, maxSuggestions: Math.min(100, maxSuggestions),
    })
    return data
  },
}
