import { http } from '@/lib/http/client'
import type { CreateUserInput, UpdateUserInput, User, UserRole } from '../types'

// The backend has no JsonStringEnumConverter yet, so request bodies must send the enum's number
// ("TEACHER" → 400). Responses already use names. Numbers keep working once strings are enabled.
const ROLE_CODE: Record<UserRole, number> = { ADMIN: 0, STUDENT: 1, TEACHER: 2 }

export const usersApi = {
  async list(): Promise<User[]> {
    const { data } = await http.get<User[]>('/api/auth/admin/users')
    return data
  },

  async create({ role, ...rest }: CreateUserInput): Promise<User> {
    const { data } = await http.post<User>('/api/auth/admin/create-user', { ...rest, role: ROLE_CODE[role] })
    return data
  },

  async update(userId: number, { role, ...rest }: UpdateUserInput): Promise<User> {
    const body = role ? { ...rest, role: ROLE_CODE[role] } : rest
    const { data } = await http.put<User>(`/api/auth/admin/users/${userId}`, body)
    return data
  },
}
