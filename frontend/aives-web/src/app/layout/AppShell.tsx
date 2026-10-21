import { LayoutDashboard, LogOut } from 'lucide-react'
import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { Logo } from '@/components/common/Logo'
import { Button } from '@/components/ui/button'
import { ROLE_HOME, ROLE_LABEL, useAuth, type UserRole } from '@/features/auth'
import { cn } from '@/lib/utils'
import { featuresFor } from '../features'

/** Signed-in layout: role navigation on the left (top on small screens), page content on the right. */
export function AppShell({ role }: { role: UserRole }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [loggingOut, setLoggingOut] = useState(false)

  const home = ROLE_HOME[role]
  const links = [
    { to: home, label: 'Overview', icon: LayoutDashboard, end: true },
    ...featuresFor(role).map((f) => ({ to: `${home}/${f.path}`, label: f.title, icon: f.icon, end: false })),
  ]

  async function handleLogout() {
    setLoggingOut(true)
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <div className="min-h-svh lg:grid lg:grid-cols-[264px_1fr]">
      <aside className="border-b bg-muted/60 lg:sticky lg:top-0 lg:flex lg:h-svh lg:flex-col lg:border-r lg:border-b-0">
        <div className="flex h-16 items-center justify-between gap-3 px-4 lg:h-20 lg:px-6">
          <Logo to={home} />
          <span className="rounded-full bg-brand-soft px-3 py-1 text-[13px] font-medium text-brand">
            {ROLE_LABEL[role]}
          </span>
        </div>

        <nav aria-label="Features" className="overflow-x-auto px-4 pb-3 lg:flex-1 lg:overflow-y-auto lg:px-3 lg:pb-0">
          <ul className="flex gap-1 lg:flex-col">
            {links.map(({ to, label, icon: Icon, end }) => (
              <li key={to}>
                <NavLink
                  to={to}
                  end={end}
                  className={({ isActive }) =>
                    cn(
                      'flex h-10 items-center gap-3 rounded-xl px-3 text-[15px] whitespace-nowrap transition-colors',
                      isActive
                        ? 'bg-background font-medium text-foreground shadow-sm'
                        : 'text-muted-foreground hover:bg-background/70 hover:text-foreground',
                    )
                  }
                >
                  <Icon className="size-[18px] shrink-0" aria-hidden="true" />
                  {label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <div className="hidden border-t p-4 lg:block">
          <p className="truncate font-medium">{user?.fullName}</p>
          <p className="truncate text-sm text-muted-foreground">{user?.email ?? user?.username}</p>
          <Button variant="secondary" size="sm" className="mt-3 w-full" onClick={handleLogout} disabled={loggingOut}>
            <LogOut aria-hidden="true" />
            {loggingOut ? 'Signing out…' : 'Sign out'}
          </Button>
        </div>
      </aside>

      <div className="min-w-0">
        {/* Small screens: the sidebar footer is hidden, so account + logout live in a slim bar. */}
        <div className="flex items-center justify-between gap-3 border-b px-4 py-2.5 lg:hidden">
          <span className="truncate text-sm font-medium">{user?.fullName}</span>
          <Button variant="ghost" size="sm" onClick={handleLogout} disabled={loggingOut}>
            <LogOut aria-hidden="true" />
            {loggingOut ? 'Signing out…' : 'Sign out'}
          </Button>
        </div>
        <main className="mx-auto max-w-[1100px] px-4 py-8 sm:px-8 lg:py-12">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
