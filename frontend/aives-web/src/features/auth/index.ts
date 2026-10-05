// Public API of the auth feature: other features and the app shell import from here only.
export { AuthProvider } from './context/AuthProvider'
export { GuestRoute, ProtectedRoute } from './components/RouteGuards'
export { useAuth } from './hooks/useAuth'
export { ROLE_HOME, ROLE_LABEL } from './lib/roles'
export { default as LoginPage } from './pages/LoginPage'
export { default as RegisterPage } from './pages/RegisterPage'
export type { User, UserRole } from './types'
