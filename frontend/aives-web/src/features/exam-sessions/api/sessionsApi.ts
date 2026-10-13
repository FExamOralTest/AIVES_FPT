import { SESSION_STATUS_CODE, type SessionStatus } from '@/lib/domain/sessionStatus'
import { http } from '@/lib/http/client'
import type { CreateSessionInput, ExamSession, ExamSessionSummary, SessionMonitor } from '../types'

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
    const { data } = await http.get<SessionMonitor>(`/api/exam-sessions/${sessionId}/monitor`)
    return data
  },
}
