import { cn } from '@/lib/utils'

// Deterministic bar heights (0–1) so the waveform reads as a spoken phrase, not noise.
const WAVE = [
  0.18, 0.3, 0.52, 0.4, 0.66, 0.9, 0.72, 0.48, 0.6, 0.84, 1, 0.78, 0.5, 0.34, 0.22, 0.14, 0.26, 0.44, 0.7, 0.58,
  0.82, 0.62, 0.4, 0.3, 0.46, 0.68, 0.54, 0.36, 0.2, 0.12, 0.1, 0.16, 0.32, 0.5, 0.42, 0.28, 0.18, 0.12, 0.1, 0.08,
]

interface WaveformProps {
  className?: string
  barClassName?: string
}

export function Waveform({ className, barClassName }: WaveformProps) {
  return (
    <div aria-hidden="true" className={cn('flex h-11 items-center gap-[3px]', className)}>
      {WAVE.map((h, i) => (
        <span
          key={i}
          className={cn('w-1 shrink-0 rounded-full bg-brand', barClassName)}
          style={{ height: `${Math.round(h * 100)}%`, opacity: 0.35 + h * 0.65 }}
        />
      ))}
    </div>
  )
}
