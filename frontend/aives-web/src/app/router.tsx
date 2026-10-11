import type { ComponentType } from 'react'
import { Route, Routes } from 'react-router-dom'
import { GuestRoute, LoginPage, ProtectedRoute, RegisterPage, ROLE_HOME, type UserRole } from '@/features/auth'
import { EnrollPage } from '@/features/enroll'
import { LandingPage } from '@/features/landing'
import { featuresFor } from './features'
import { AppShell } from './layout/AppShell'
import NotFoundPage from './NotFoundPage'
import FeaturePlaceholderPage from './pages/FeaturePlaceholderPage'
import RoleHomePage from './pages/RoleHomePage'

const ROLES: UserRole[] = ['ADMIN', 'TEACHER', 'STUDENT']

// Features whose real page is built, keyed "ROLE/path"; every other feature renders its placeholder.
const PAGES: Record<string, ComponentType> = {
  'STUDENT/enroll': EnrollPage,
}

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<LandingPage />} />

      <Route element={<GuestRoute />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
      </Route>

      {/* Each role gets its own shell; features render placeholders until they're added to PAGES. */}
      {ROLES.map((role) => (
        <Route key={role} element={<ProtectedRoute roles={[role]} />}>
          <Route path={ROLE_HOME[role]} element={<AppShell role={role} />}>
            <Route index element={<RoleHomePage role={role} />} />
            {featuresFor(role).map((feature) => {
              const Page = PAGES[`${role}/${feature.path}`]
              return (
                <Route
                  key={feature.path}
                  path={feature.path}
                  element={Page ? <Page /> : <FeaturePlaceholderPage feature={feature} />}
                />
              )
            })}
          </Route>
        </Route>
      ))}

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
