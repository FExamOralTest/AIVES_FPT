import { http } from '@/lib/http/client'
import type { CreateUserInput, UpdateUserInput, User, UserRole } from '../types'
const ROLE_CODE: Record<UserRole, number> = { ADMIN: 0, STUDENT: 1, STAFF: 2, EXAMINER: 3 }


export const usersApi = {
  async list(): Promise<User[]> {
    const { data } = await http.get<User[]>('/api/auth/admin/users')
    return data
  },

  async get(userId: number): Promise<User> {
    const { data } = await http.get<User>(`/api/admin/users/${userId}`)
    return data
  },

  async delete(userId: number): Promise<void> {
    await http.delete(`/api/admin/users/${userId}`)
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
