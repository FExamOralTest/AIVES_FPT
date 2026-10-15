import { ArrowUpRight } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Logo } from '@/components/common/Logo'
import { Button } from '@/components/ui/button'
import { ROLE_HOME, useAuth } from '@/features/auth'
import { NAV_LINKS } from '../content'

export function SiteHeader() {
  const { user } = useAuth()

  return (
    <header className="px-4 sm:px-6">
      <div className="mx-auto flex min-h-20 max-w-[1200px] flex-wrap items-center justify-between gap-x-6 gap-y-3 py-3">
        <Logo />

        <nav aria-label="Điều hướng chính" className="order-3 flex w-full flex-wrap gap-x-8 gap-y-2 md:order-none md:w-auto">
          {NAV_LINKS.map((link) => (
            <a key={link.href} href={link.href} className="text-sm font-medium text-foreground/75 hover:text-foreground">
              {link.label}
            </a>
          ))}
        </nav>

        <div className="flex items-center gap-1.5">
          {user ? (
            <Button asChild size="sm" className="h-11 px-5">
              <Link to={ROLE_HOME[user.role]}>Vào trang của tôi</Link>
            </Button>
          ) : (
            <>
              <Button asChild variant="ghost" size="sm" className="h-11 px-4">
                <Link to="/register">Đăng ký</Link>
              </Button>
              <Button asChild size="sm" className="h-11 px-5">
                <Link to="/login">Đăng nhập</Link>
              </Button>
              <Button asChild size="icon" className="size-11" aria-label="Đăng nhập">
                <Link to="/login">
                  <ArrowUpRight />
                </Link>
              </Button>
            </>
          )}
        </div>
      </div>
    </header>
  )
}
