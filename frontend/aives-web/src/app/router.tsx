import { Route, Routes } from 'react-router-dom'
import { GuestRoute, LoginPage, ProtectedRoute, RegisterPage } from '@/features/auth'
import { DashboardPage } from '@/features/dashboard'
import { LandingPage } from '@/features/landing'
import NotFoundPage from './NotFoundPage'

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<LandingPage />} />

      <Route element={<GuestRoute />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
      </Route>

      <Route element={<ProtectedRoute roles={['ADMIN']} />}>
        <Route path="/admin" element={<DashboardPage />} />
      </Route>
      <Route element={<ProtectedRoute roles={['TEACHER']} />}>
        <Route path="/teacher" element={<DashboardPage />} />
      </Route>
      <Route element={<ProtectedRoute roles={['STUDENT']} />}>
        <Route path="/student" element={<DashboardPage />} />
      </Route>

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
