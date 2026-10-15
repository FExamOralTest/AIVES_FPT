import { http } from '@/lib/http/client'
import type { LoginRequest, LoginResponse, RegisterRequest, User } from '../types'

export const authApi = {
  async register(payload: RegisterRequest): Promise<User> {
    const { data } = await http.post<User>('/api/auth/register', payload)
    return data
  },

  async login(payload: LoginRequest): Promise<LoginResponse> {
    const { data } = await http.post<LoginResponse>('/api/auth/login', payload)
    return data
  },

  async me(): Promise<User> {
    const { data } = await http.get<User>('/api/auth/me')
    return data
  },

  async logout(): Promise<void> {
    await http.post('/api/auth/logout')
  },
}
