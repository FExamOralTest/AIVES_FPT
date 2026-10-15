import { createContext } from 'react'
import type { LoginRequest, User } from '../types'

export type AuthStatus = 'loading' | 'authenticated' | 'guest'

export interface AuthContextValue {
  user: User | null
  status: AuthStatus
  login: (payload: LoginRequest) => Promise<User>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)
