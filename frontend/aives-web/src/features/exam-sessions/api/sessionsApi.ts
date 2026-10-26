import { SESSION_STATUS_CODE, type SessionStatus } from '@/lib/domain/sessionStatus'
import { http } from '@/lib/http/client'
import type {
  BulkEnrollResult,
  CreateSessionInput,
  DistributeExamsResponse,
  DistributeSlotsRequest,
  DistributeSlotsResponse,
  ExamSession,
  ExamSessionSummary,
  SessionMonitor,
  UpdateSessionInput,
} from '../types'

export const sessionsApi = {
  async listMine(teacherId: number): Promise<ExamSessionSummary[]> {
    const { data } = await http.get<ExamSessionSummary[]>(`/api/exam-sessions/teacher/${teacherId}`)
    return data
  },

  async get(sessionId: number): Promise<ExamSession> {
    const { data } = await http.get<ExamSession>(`/api/exam-sessions/${sessionId}`)
    return data
  },

  async create(input: CreateSessionInput): Promise<ExamSession> {
    const { data } = await http.post<ExamSession>('/api/exam-sessions', input)
    return data
  },

  /** Sends the enum number: "IN_PROGRESS" as a string is rejected with 400 (no JsonStringEnumConverter). */
  async updateStatus(sessionId: number, status: SessionStatus): Promise<ExamSession> {
    const { data } = await http.put<ExamSession>(`/api/exam-sessions/${sessionId}/status`, {
      status: SESSION_STATUS_CODE[status],
    })
    return data
  },

  async monitor(sessionId: number): Promise<SessionMonitor> {
    const { data } = await http.get<SessionMonitor>(`/api/staff/sessions/${sessionId}/monitor`)
    return data
  },

  // Editing, deleting and manual enrollment live under /api/staff/sessions.
  async update(sessionId: number, input: UpdateSessionInput): Promise<ExamSession> {
    const { data } = await http.put<ExamSession>(`/api/staff/sessions/${sessionId}`, input)
    return data
  },

  /** Only for sessions that have not started (or were cancelled) and have no students. */
  async remove(sessionId: number): Promise<void> {
    await http.delete(`/api/staff/sessions/${sessionId}`)
  },

  async addStudents(
    sessionId: number,
    payload: { studentIds?: number[]; identifiers?: string[] } | number[]
  ): Promise<BulkEnrollResult> {
    const body = Array.isArray(payload) ? { studentIds: payload } : payload
    const { data } = await http.post<BulkEnrollResult>(`/api/staff/sessions/${sessionId}/enrollments`, body)
    return data
  },

  /** Tự động nhập toàn bộ sinh viên đã ghi danh vào môn học của ca thi. */
  async enrollCourseStudents(sessionId: number): Promise<BulkEnrollResult> {
    const { data } = await http.post<BulkEnrollResult>(
      `/api/staff/sessions/${sessionId}/enroll-course-students`
    )
    return data
  },

  async removeStudent(sessionId: number, studentId: number): Promise<void> {
    await http.delete(`/api/staff/sessions/${sessionId}/enrollments/${studentId}`)
  },

  async distributeExams(
    sessionId: number,
    request?: { randomSeed?: number; strategy?: string }
  ): Promise<DistributeExamsResponse> {
    const { data } = await http.post<DistributeExamsResponse>(
      `/api/staff/sessions/${sessionId}/distribute-exams`,
      request ?? {}
    )
    return data
  },

  async assignStudentExam(sessionId: number, studentId: number, examId: number): Promise<void> {
    await http.put(`/api/staff/sessions/${sessionId}/enrollments/${studentId}/assign-exam`, {
      examId,
    })
  },

  async distributeSlots(
    sessionId: number,
    request?: DistributeSlotsRequest
  ): Promise<DistributeSlotsResponse> {
    const { data } = await http.post<DistributeSlotsResponse>(
      `/api/staff/sessions/${sessionId}/distribute-slots`,
      request ?? {}
    )
    return data
  },

  async assignStudentSlot(sessionId: number, studentId: number, slotId: number): Promise<void> {
    await http.put(`/api/staff/sessions/${sessionId}/enrollments/${studentId}/assign-slot`, {
      slotId,
    })
  },
}
