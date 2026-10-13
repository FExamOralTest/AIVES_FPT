import { Link } from 'react-router-dom'
import { cn } from '@/lib/utils'

export function LogoMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 466 466" aria-hidden="true" className={cn('size-7 shrink-0', className)}>
      <path fill="var(--logo)" d="M0 0H311A155 155 0 0 1 466 155V466H311V155H0Z" />
      <path fill="var(--logo)" d="M155 156A155 155 0 1 0 310 311H155Z" />
    </svg>
  )
}

interface LogoProps {
  to?: string
  className?: string
}

/** Mark + "aives" wordmark, linking home. */
export function Logo({ to = '/', className }: LogoProps) {
  return (
    <Link
      to={to}
      aria-label="AIVES, về trang chủ"
      className={cn('inline-flex items-center gap-2.5 font-heading text-xl tracking-tight text-foreground', className)}
    >
      <LogoMark />
      aives
    </Link>
  )
}
