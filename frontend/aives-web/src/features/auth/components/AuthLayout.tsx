import { ChevronLeft } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Logo } from '@/components/common/Logo'
import { Button } from '@/components/ui/button'

interface AuthLayoutProps {
  children: ReactNode
  /** Grey visual panel on the right (hidden on small screens). */
  aside: ReactNode
}

export function AuthLayout({ children, aside }: AuthLayoutProps) {
  return (
    <div className="grid min-h-svh gap-4 p-4 lg:grid-cols-2">
      <div className="flex flex-col px-2 pt-2 pb-8 sm:px-6">
        <header className="flex items-center justify-between gap-3">
          <Logo />
          <Button asChild variant="secondary" size="sm">
            <Link to="/">
              <ChevronLeft />
              Trang chủ
            </Link>
          </Button>
        </header>
        <main className="flex flex-1 items-center justify-center py-10">
          <div className="w-full max-w-[440px]">{children}</div>
        </main>
      </div>

      <aside className="relative hidden min-h-[560px] overflow-hidden rounded-[32px] bg-muted lg:flex">{aside}</aside>
    </div>
  )
}

