import { cn } from '@/lib/utils'
import { ENDPOINT_STATE_LABEL, STATUS_LABEL, type EndpointState, type FeatureStatus } from '../features'

const TONE = {
  ready: 'bg-success-soft text-success',
  ok: 'bg-success-soft text-success',
  partial: 'bg-warning-soft text-warning',
  issue: 'bg-warning-soft text-warning',
  pending: 'bg-muted text-muted-foreground',
  missing: 'bg-muted text-muted-foreground',
} as const

const DOT = {
  ready: 'bg-success',
  ok: 'bg-success',
  partial: 'bg-warning',
  issue: 'bg-warning',
  pending: 'bg-muted-foreground/60',
  missing: 'bg-muted-foreground/60',
} as const

export function StatusBadge({ status, className }: { status: FeatureStatus; className?: string }) {
  return <Pill tone={status} label={STATUS_LABEL[status]} className={className} />
}

export function EndpointStateBadge({ state }: { state: EndpointState }) {
  return <Pill tone={state} label={ENDPOINT_STATE_LABEL[state]} />
}

function Pill({ tone, label, className }: { tone: keyof typeof TONE; label: string; className?: string }) {
  return (
    <span
      className={cn(
        'inline-flex h-6 shrink-0 items-center gap-1.5 rounded-full px-2.5 text-xs font-medium whitespace-nowrap',
        TONE[tone],
        className,
      )}
    >
      <span aria-hidden="true" className={cn('size-1.5 rounded-full', DOT[tone])} />
      {label}
    </span>
  )
}
