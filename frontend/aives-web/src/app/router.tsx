import { Route, Routes } from 'react-router-dom'
import { GuestRoute, LoginPage, ProtectedRoute, RegisterPage, ROLE_HOME, type UserRole } from '@/features/auth'
import { LandingPage } from '@/features/landing'
import { featuresFor } from './features'
import { AppShell } from './layout/AppShell'
import NotFoundPage from './NotFoundPage'
import FeaturePlaceholderPage from './pages/FeaturePlaceholderPage'
import RoleHomePage from './pages/RoleHomePage'

const ROLES: UserRole[] = ['ADMIN', 'TEACHER', 'STUDENT']

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<LandingPage />} />

      <Route element={<GuestRoute />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
      </Route>

      {/* Each role gets its own shell; features render placeholders until their UI is built. */}
      {ROLES.map((role) => (
        <Route key={role} element={<ProtectedRoute roles={[role]} />}>
          <Route path={ROLE_HOME[role]} element={<AppShell role={role} />}>
            <Route index element={<RoleHomePage role={role} />} />
            {featuresFor(role).map((feature) => (
              <Route key={feature.path} path={feature.path} element={<FeaturePlaceholderPage feature={feature} />} />
            ))}
          </Route>
        </Route>
      ))}

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
