import type { SessionStatus } from '@/lib/domain/sessionStatus'



export type { SessionStatus }



export interface SessionExamSummary {

  examId: number

  examCode: string

  title: string

  durationMinutes: number

  totalQuestions: number

  assignedStudentsCount: number

}



export interface ExamSessionSlot {

  slotId: number

  sessionId?: number

  slotIndex: number

  slotName: string

  slotCode?: string | null

  startTime: string

  endTime: string

  maxCapacity: number

  assignedStudentsCount?: number

  slotTemplateId?: number | null

  allowedExams?: SessionExamSummary[]

}



export interface CreateSessionSlotInput {

  slotIndex: number

  slotName: string

  startTime: string

  endTime: string

  maxCapacity?: number

  slotTemplateId?: number | null

  allowedExamIds?: number[]

}



/** GET /api/exam-sessions/teacher/{teacherId}: no session code here, load the detail for it. */

export interface ExamSessionSummary {

  sessionId: number

  sessionName: string

  examId?: number | null

  examTitle: string

  examCode: string

  courseId?: number | null

  courseName?: string

  room: string | null

  periodId?: number | null

  periodName?: string | null

  startTime: string

  endTime: string

  status: SessionStatus

  enrolledStudents: number

  exams?: SessionExamSummary[]

  slots?: ExamSessionSlot[]

}



/** GET /api/exam-sessions/{id}, POST /api/exam-sessions, PUT …/status. */

export interface ExamSession extends ExamSessionSummary {

  examId?: number | null

  durationMinutes: number

  scheduledBy: number

  scheduledByName: string | null

  createdAt: string | null

  /** Enroll code students type, "ESS-XXXXXXXX". */

  sessionCode: string

  maxCapacity: number

  courseId?: number | null

  courseName?: string

  periodId?: number | null

  periodName?: string | null

  exams?: SessionExamSummary[]

  slots?: ExamSessionSlot[]

}



export interface CreateSessionInput {

  examId?: number

  examIds?: number[]

  slots?: CreateSessionSlotInput[]

  courseId?: number

  periodId?: number

  sessionName: string

  room?: string

  /** ISO UTC strings. */

  startTime: string

  endTime: string

  maxCapacity: number

}



/** PUT /api/staff/sessions/{id}. */

export interface UpdateSessionInput {

  sessionName?: string

  room?: string

  examIds?: number[]

  slots?: CreateSessionSlotInput[]

  periodId?: number

  /** ISO UTC strings. */

  startTime?: string

  endTime?: string

  maxCapacity?: number

}



export type EnrollOutcome = 'added' | 'already_enrolled' | 'not_student' | 'not_found'



/** POST /api/staff/sessions/{id}/enrollments: one entry per requested student ID. */

export interface BulkEnrollResult {

  sessionId?: number

  addedCount: number

  skippedCount: number

  currentEnrollment: number

  results: {

    studentId: number

    identifier?: string

    studentCode?: string

    studentName?: string

    status: EnrollOutcome

    reason: string | null

  }[]

}



export interface ExamAllocationCount {

  examId: number

  examCode: string

  title: string

  studentCount: number

}



export interface DistributeExamsResponse {

  sessionId: number

  totalStudents: number

  totalExams: number

  allocations: ExamAllocationCount[]

}



export interface SlotAllocationCount {

  slotId: number

  slotIndex: number

  slotName: string

  startTime: string

  endTime: string

  studentCount: number

}



export interface DistributeSlotsResponse {

  sessionId: number

  totalStudents: number

  totalSlots: number

  allocations: SlotAllocationCount[]

}



export interface DistributeSlotsRequest {

  randomSeed?: number

  strategy?: string

  examAssignmentMode?: 'EXAM_PER_SLOT' | 'SHUFFLE_IN_SLOT'

}



export interface StudentProgress {

  sessionEnrollmentId: number

  studentId: number

  studentName: string

  studentEmail: string

  enrollmentStatus: SessionStatus

  enrolledAt: string | null

  startedAt: string | null

  progressPercent: number

  assignedExamId?: number | null

  assignedExamCode?: string | null

  assignedExamTitle?: string | null

  assignedSlotId?: number | null

  assignedSlotName?: string | null

  slotStartTime?: string | null

  slotEndTime?: string | null

  answeredQuestions?: number

  totalQuestions?: number

}



/** GET /api/exam-sessions/{id}/monitor. */

export interface SessionMonitor {

  sessionId?: number

  sessionName?: string

  examTitle?: string

  examCode?: string

  currentEnrollment: number

  maxCapacity: number

  exams?: SessionExamSummary[]

  slots?: ExamSessionSlot[]

  students: StudentProgress[]

}



export interface ExamPeriod {
  periodId: number
  courseId: number
  courseName?: string
  periodName: string
  description?: string
  startDate: string
  endDate: string
  status: string
  sessionsCount?: number
  createdAt?: string
}

export interface CreateExamPeriodRequest {
  courseId: number
  periodName: string
  description?: string
  startDate: string
  endDate: string
  status?: string
}
