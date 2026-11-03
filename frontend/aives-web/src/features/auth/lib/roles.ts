import type { UserRole } from '../types'

export const ROLE_HOME: Record<UserRole, string> = {
  ADMIN: '/admin',
  STAFF: '/staff',
  EXAMINER: '/examiner',
  STUDENT: '/student',
}

export const ROLE_LABEL: Record<UserRole, string> = {
  ADMIN: 'Administrator',
  STAFF: 'Academic staff',
  EXAMINER: 'Examiner',
  STUDENT: 'Student',
}

