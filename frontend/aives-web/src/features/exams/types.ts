export type ExamStatus = 'DRAFT' | 'SCHEDULED' | 'OPEN' | 'CLOSED' | 'CANCELLED'

export interface ExamSlotTemplate {
  slotTemplateId?: number
  slotIndex: number
  slotName: string
  durationMinutes: number
  maxCapacity: number
  examPaperIds?: number[]
  examPapers?: {
    examId: number
    examCode: string
    title: string
    durationMinutes: number
    totalQuestions: number
  }[]
}

export interface CreateExamSlotTemplateInput {
  slotIndex: number
  slotName: string
  durationMinutes: number
  maxCapacity: number
  examPaperIds?: number[]
}

export interface ExamQuestion {
  questionId: number
  questionText: string
  questionScore: number
  maxScore: number | null
  isFollowUpAllowed: boolean | null
  level?: number | null
}

export interface Exam {
  canEdit: boolean
  examId: number
  title: string
  examCode: string
  courseId: number
  courseName: string | null
  createdBy: number
  createdByName: string | null
  durationMinutes: number
  maxQuestions: number
  examStatus: ExamStatus | null
  createdAt: string | null
  questions: ExamQuestion[]
  slotTemplates?: ExamSlotTemplate[]
}

export interface CreateExamQuestionItem {
  questionId: number
  questionScore: number
}

export interface CreateSubPaperInput {
  slotIndex: number
  title: string
  examCode?: string
  questions: CreateExamQuestionItem[]
}

export interface CreateExamInput {
  title: string
  courseId: number
  durationMinutes: number
  maxQuestions: number
  questions?: CreateExamQuestionItem[]
  slotTemplates?: CreateExamSlotTemplateInput[]
  autoGenerateSlotPapers?: boolean
  questionsPerSlot?: number
  generatedSubPapers?: CreateSubPaperInput[]
}

export type DocType = 'CONTENT' | 'LEARNING_OUTCOMES'

export interface UpdateExamInput {
  title: string
  durationMinutes: number
  maxQuestions: number
  questions: CreateExamQuestionItem[]
  slotTemplates?: CreateExamSlotTemplateInput[]
  autoGenerateSlotPapers?: boolean
  questionsPerSlot?: number
  generatedSubPapers?: CreateSubPaperInput[]
}

export interface AutoPartitionSlotPapersRequest {
  courseId: number
  parentExamTitle?: string
  parentExamCode?: string
  slotCount: number
  questionsPerSlot: number
  questionIds: number[]
  slotTemplates?: CreateExamSlotTemplateInput[]
}

export interface SlotPartitionPreviewDto {
  slotIndex: number
  slotName: string
  subPaperCode: string
  subPaperTitle: string
  questions: ExamQuestion[]
  easyCount: number
  mediumCount: number
  hardCount: number
}

export interface AutoPartitionPreviewResponse {
  slots: SlotPartitionPreviewDto[]
  reserveQuestions: ExamQuestion[]
  totalSelected: number
  totalAllocated: number
  totalReserve: number
  summary: string
}
