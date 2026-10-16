import type { ComponentType } from 'react'
import { Route, Routes } from 'react-router-dom'
import { AdminCoursesPage } from '@/features/admin-courses'
import { UsersPage } from '@/features/admin-users'
import { GuestRoute, LoginPage, ProtectedRoute, RegisterPage, ROLE_HOME, type UserRole } from '@/features/auth'
import { EnrollPage } from '@/features/enroll'
import { SessionsPage } from '@/features/exam-sessions'
import { ExamsPage } from '@/features/exams'
import { LandingPage } from '@/features/landing'
import { StudentResultsPage } from '@/features/student-results'
import { StudentSessionsPage } from '@/features/student-sessions'
import { TeacherCoursesPage } from '@/features/teacher-courses'
import { TeacherQuestionsPage } from '@/features/teacher-questions'
import { TeacherResultsPage } from '@/features/teacher-results'
import { VivaRoomPage } from '@/features/viva'
import { featuresFor } from './features'
import { AppShell } from './layout/AppShell'
import NotFoundPage from './NotFoundPage'
import FeaturePlaceholderPage from './pages/FeaturePlaceholderPage'
import RoleHomePage from './pages/RoleHomePage'

const ROLES: UserRole[] = ['ADMIN', 'STAFF', 'EXAMINER', 'STUDENT']

// Features whose real page is built, keyed "ROLE/path"; every other feature renders its placeholder.
const PAGES: Record<string, ComponentType> = {
  'ADMIN/users': UsersPage,
  'ADMIN/courses': AdminCoursesPage,
  'STAFF/courses': TeacherCoursesPage,
  'STAFF/questions': TeacherQuestionsPage,
  'STAFF/exams': ExamsPage,
  'STAFF/sessions': SessionsPage,
  'EXAMINER/results': TeacherResultsPage,
  'STUDENT/enroll': EnrollPage,
  'STUDENT/sessions': StudentSessionsPage,
  'STUDENT/viva': VivaRoomPage,
  'STUDENT/results': StudentResultsPage,
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
