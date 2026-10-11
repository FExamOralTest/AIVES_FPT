import type { CSSProperties, ReactNode } from 'react'
import { cn } from '@/lib/utils'
import { useInView } from '../hooks/useInView'

interface RevealProps {
  children: ReactNode
  className?: string
}

/** Marks a block whose `RevealItem` children fade and slide up when it scrolls into view. */
export function Reveal({ children, className }: RevealProps) {
  const { ref, inView } = useInView<HTMLDivElement>()

  return (
    <div ref={ref} data-shown={inView} className={cn('group/reveal', className)}>
      {children}
    </div>
  )
}

interface RevealItemProps {
  children: ReactNode
  className?: string
  /** Position in a staggered list; each step adds ~110 ms of delay. */
  index?: number
  as?: 'div' | 'li'
}

export function RevealItem({ children, className, index = 0, as: Tag = 'div' }: RevealItemProps) {
  const style: CSSProperties = { transitionDelay: `${120 + index * 110}ms` }

  return (
    <Tag
      style={style}
      className={cn(
        'transition-[opacity,translate] duration-700 ease-[cubic-bezier(.2,.8,.2,1)]',
        'group-data-[shown=false]/reveal:translate-y-8 group-data-[shown=false]/reveal:opacity-0',
        'motion-reduce:translate-y-0! motion-reduce:transition-opacity',
        className,
      )}
    >
      {children}
    </Tag>
  )
}
