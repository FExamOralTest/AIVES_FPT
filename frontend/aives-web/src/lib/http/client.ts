import axios from 'axios'

interface HttpAuthHooks {
  getToken: () => string | null
  onUnauthorized: () => void
}

// The auth feature registers these at startup, so this shared client never imports a feature.
let authHooks: HttpAuthHooks = { getToken: () => null, onUnauthorized: () => {} }

export function configureHttpAuth(hooks: HttpAuthHooks) {
  authHooks = hooks
}

// Empty base URL = same origin; in dev, Vite proxies /api to the backend (see vite.config.ts).
// When the backend is deployed, set VITE_API_BASE_URL to its origin.
export const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '',
  headers: { 'Content-Type': 'application/json' },
  timeout: 15000,
})

http.interceptors.request.use((config) => {
  const token = authHooks.getToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

http.interceptors.response.use(
  (response) => response,
  (error) => {
    const hadToken = Boolean(error.config?.headers?.Authorization)
    if (axios.isAxiosError(error) && error.response?.status === 401 && hadToken) {
      authHooks.onUnauthorized()
    }
    return Promise.reject(error)
  },
)
