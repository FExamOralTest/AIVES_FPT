import type { ReactNode } from 'react'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'

interface SectionHeadingProps {
  label: string
  title: ReactNode
  description?: string
  align?: 'center' | 'start'
}

/** Grey pill label above a section title, as in the design. */
export function SectionHeading({ label, title, description, align = 'center' }: SectionHeadingProps) {
  return (
    <div className={cn('flex max-w-2xl flex-col gap-4', align === 'center' ? 'items-center text-center' : 'items-start')}>
      <Badge variant="secondary" className="h-[30px] px-3.5 text-[13px] text-foreground/75">
        {label}
      </Badge>
      <h2 className="font-heading text-4xl leading-[1.12] font-medium tracking-tight text-balance md:text-[44px]">{title}</h2>
      {description && <p className="text-base leading-relaxed text-muted-foreground">{description}</p>}
    </div>
  )
}
