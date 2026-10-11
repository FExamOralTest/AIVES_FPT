import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { ROLE_HOME } from '../lib/roles'
import type { UserRole } from '../types'

function FullPageLoader() {
  return (
    <div role="status" className="flex min-h-svh items-center justify-center gap-3 text-muted-foreground">
      <span className="size-3 animate-pulse rounded-full bg-brand motion-reduce:animate-none" />
      Đang kiểm tra phiên đăng nhập…
    </div>
  )
}

/** Requires a signed-in user; optionally restricts to specific roles. */
export function ProtectedRoute({ roles }: { roles?: UserRole[] }) {
  const { user, status } = useAuth()
  const location = useLocation()

  if (status === 'loading') return <FullPageLoader />
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (roles && !roles.includes(user.role)) return <Navigate to={ROLE_HOME[user.role]} replace />

  return <Outlet />
}

/** Login/register pages: send signed-in users to their dashboard. */
export function GuestRoute() {
  const { user, status } = useAuth()

  if (status === 'loading') return <FullPageLoader />
  if (user) return <Navigate to={ROLE_HOME[user.role]} replace />

  return <Outlet />
}
