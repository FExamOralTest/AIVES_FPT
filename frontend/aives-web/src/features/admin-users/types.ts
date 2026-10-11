import type { User, UserRole } from '@/features/auth'

export type { User, UserRole }

export interface CreateUserInput {
  username: string
  fullName: string
  email: string
  password: string
  role: UserRole
}

/** Omitted fields are left unchanged by the backend. */
export interface UpdateUserInput {
  fullName?: string
  email?: string
  password?: string
  role?: UserRole
}
