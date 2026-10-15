import { cn } from '@/lib/utils'

/** Decorative 5×3 dot pattern used on the grey visual panels. */
export function DotGrid({ className }: { className?: string }) {
  return (
    <div aria-hidden="true" className={cn('grid grid-cols-5 gap-2', className)}>
      {Array.from({ length: 15 }, (_, i) => (
        <span key={i} className="size-1.5 rounded-full bg-brand/35" />
      ))}
    </div>
  )
}
