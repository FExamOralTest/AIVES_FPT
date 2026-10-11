import type { User } from '../types'

const TOKEN_KEY = 'accessToken'
const USER_KEY = 'user'
const EXPIRES_AT_KEY = 'accessTokenExpiresAt'

export const tokenStorage = {
  getToken(): string | null {
    const token = localStorage.getItem(TOKEN_KEY)
    if (!token) return null

    const expiresAt = Number(localStorage.getItem(EXPIRES_AT_KEY))
    if (expiresAt && Date.now() >= expiresAt) {
      tokenStorage.clear()
      return null
    }
    return token
  },

  getUser(): User | null {
    const raw = localStorage.getItem(USER_KEY)
    if (!raw) return null
    try {
      return JSON.parse(raw) as User
    } catch {
      localStorage.removeItem(USER_KEY)
      return null
    }
  },

  save(token: string, expiresInSeconds: number, user: User) {
    localStorage.setItem(TOKEN_KEY, token)
    localStorage.setItem(EXPIRES_AT_KEY, String(Date.now() + expiresInSeconds * 1000))
    localStorage.setItem(USER_KEY, JSON.stringify(user))
  },

  saveUser(user: User) {
    localStorage.setItem(USER_KEY, JSON.stringify(user))
  },

  clear() {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(EXPIRES_AT_KEY)
    localStorage.removeItem(USER_KEY)
  },
}
