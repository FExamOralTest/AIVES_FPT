export type UserRole = 'ADMIN' | 'TEACHER' | 'STUDENT'

export interface User {
  userId: number
  username: string
  fullName: string
  email: string | null
  role: UserRole
}

export interface RegisterRequest {
  username: string
  fullName: string
  email: string
  password: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  tokenType: string
  expiresIn: number
  user: User
}
