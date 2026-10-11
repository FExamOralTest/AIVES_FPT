import type { UserRole } from '../types'

export const ROLE_HOME: Record<UserRole, string> = {
  ADMIN: '/admin',
  TEACHER: '/teacher',
  STUDENT: '/student',
}

export const ROLE_LABEL: Record<UserRole, string> = {
  ADMIN: 'Quản trị viên',
  TEACHER: 'Giảng viên',
  STUDENT: 'Sinh viên',
}
