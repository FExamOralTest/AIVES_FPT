import axios from 'axios'
import { http } from '@/lib/http/client'
import type { ReportSummary, StatisticsFilters } from '../types'

/** Start of the local day "YYYY-MM-DD" as ISO UTC; `endOfDay` gives its last millisecond instead. */
function dayToIso(day: string, endOfDay = false): string {
  const [y, m, d] = day.split('-').map(Number)
  return (endOfDay ? new Date(y, m - 1, d, 23, 59, 59, 999) : new Date(y, m - 1, d)).toISOString()
}

export const statisticsApi = {
  /** null when the backend has no summary endpoint (404), so the page can say so instead of failing. */
  async summary(filters: StatisticsFilters): Promise<ReportSummary | null> {
    const params: Record<string, string | number> = {}
    if (filters.courseId) params.CourseId = filters.courseId
    if (filters.fromDate) params.FromDate = dayToIso(filters.fromDate)
    if (filters.toDate) params.ToDate = dayToIso(filters.toDate, true)
    if (filters.sessionStatus) params.SessionStatus = filters.sessionStatus
    try {
      const { data } = await http.get<ReportSummary>('/api/admin/reports/summary', { params })
      return data
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 404) return null
      throw err
    }
  },
}
