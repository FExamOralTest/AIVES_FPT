import { cn } from '@/lib/utils'

interface VivaWaveformProps {
  isRecording: boolean
  volumeLevel: number
  className?: string
}

export function VivaWaveform({ isRecording, volumeLevel, className }: VivaWaveformProps) {
  const barCount = 24

  return (
    <div
      className={cn(
        'flex h-16 items-center justify-center gap-1.5 rounded-2xl bg-muted/40 px-4 py-3 transition-all duration-300',
        isRecording
          ? 'border border-destructive/30 bg-destructive/5 ring-4 ring-destructive/10'
          : 'border border-border/40',
        className,
      )}
    >
      {Array.from({ length: barCount }).map((_, index) => {
        // Curve factor: highest in center, lower at edges
        const centerDistance = Math.abs(index - barCount / 2) / (barCount / 2)
        const curve = 1 - centerDistance * 0.45

        // Dynamic height calculation
        const baseHeight = isRecording ? 18 : 8
        const dynamicFactor = (volumeLevel * 0.95 + Math.sin(index * 0.8) * 15) * curve
        const rawHeight = isRecording
          ? Math.max(14, Math.min(100, baseHeight + dynamicFactor))
          : 10

        return (
          <div
            key={index}
            className={cn(
              'w-1.5 rounded-full transition-all duration-100 ease-out',
              isRecording
                ? 'bg-gradient-to-t from-red-500 to-rose-400 shadow-[0_0_8px_rgba(239,68,68,0.4)]'
                : 'bg-muted-foreground/20',
            )}
            style={{
              height: `${rawHeight}%`,
              opacity: isRecording ? 0.8 + (rawHeight / 100) * 0.2 : 0.35,
            }}
          />
        )
      })}
    </div>
  )
}
