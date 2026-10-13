import { ArrowUpRight } from 'lucide-react'
import type { ReactNode } from 'react'
import { Button } from '@/components/ui/button'

/** Black pill submit button with the round arrow companion from the design. */
export function SubmitButton({ children, pending }: { children: ReactNode; pending?: boolean }) {
  return (
    <div className="flex items-center gap-1.5">
      <Button type="submit" disabled={pending} className="flex-1">
        {children}
      </Button>
      <span
        aria-hidden="true"
        className="inline-flex size-12 shrink-0 items-center justify-center rounded-full bg-primary text-primary-foreground"
      >
        <ArrowUpRight className="size-[18px]" />
      </span>
    </div>
  )
}
