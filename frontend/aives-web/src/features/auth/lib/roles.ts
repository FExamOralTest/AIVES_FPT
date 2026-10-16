import type { UserRole } from '../types'

export const ROLE_HOME: Record<UserRole, string> = {
  ADMIN: '/admin',
  STAFF: '/staff',
  EXAMINER: '/examiner',
  STUDENT: '/student',
}

export const ROLE_LABEL: Record<UserRole, string> = {
  ADMIN: 'Quản trị viên',
  STAFF: 'Cán bộ Chuyên môn',
  EXAMINER: 'Cán bộ Khảo thí',
  STUDENT: 'Sinh viên',
}

