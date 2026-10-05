import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { configureHttpAuth } from '@/lib/http/client'
import { toApiError } from '@/lib/http/apiError'
import { authApi } from '../api/authApi'
import { tokenStorage } from '../lib/tokenStorage'
import type { LoginRequest, User } from '../types'
import { AuthContext, type AuthContextValue, type AuthStatus } from './authContext'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => (tokenStorage.getToken() ? tokenStorage.getUser() : null))
  const [status, setStatus] = useState<AuthStatus>(() => (tokenStorage.getToken() ? 'loading' : 'guest'))

  useEffect(() => {
    // Attach the token to every API call; a 401 on an authenticated call signs the user out.
    configureHttpAuth({
      getToken: tokenStorage.getToken,
      onUnauthorized: () => {
        tokenStorage.clear()
        setUser(null)
        setStatus('guest')
      },
    })

    // On reload, confirm the stored token is still accepted by the backend.
    if (!tokenStorage.getToken()) return
    let cancelled = false

    authApi
      .me()
      .then((me) => {
        if (cancelled) return
        tokenStorage.saveUser(me)
        setUser(me)
        setStatus('authenticated')
      })
      .catch((error) => {
        if (cancelled) return
        // Backend unreachable: keep the cached user rather than signing them out.
        const cached = tokenStorage.getUser()
        if (toApiError(error).status === null && cached) {
          setUser(cached)
          setStatus('authenticated')
          return
        }
        tokenStorage.clear()
        setUser(null)
        setStatus('guest')
      })

    return () => {
      cancelled = true
    }
  }, [])

  const login = useCallback(async (payload: LoginRequest) => {
    const res = await authApi.login(payload)
    tokenStorage.save(res.accessToken, res.expiresIn, res.user)
    setUser(res.user)
    setStatus('authenticated')
    return res.user
  }, [])

  const logout = useCallback(async () => {
    try {
      if (tokenStorage.getToken()) await authApi.logout()
    } catch {
      // Logout is stateless on the backend; clearing locally is what matters.
    } finally {
      tokenStorage.clear()
      setUser(null)
      setStatus('guest')
    }
  }, [])

  const value = useMemo<AuthContextValue>(() => ({ user, status, login, logout }), [user, status, login, logout])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
