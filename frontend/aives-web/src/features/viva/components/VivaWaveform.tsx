import { cn } from '@/lib/utils'

interface VivaWaveformProps {
  isRecording: boolean
  volumeLevel: number
  className?: string
}

export function VivaWaveform({ isRecording, volumeLevel, className }: VivaWaveformProps) {
  // 16 bars representing frequency visualization
  const barCount = 16

  return (
    <div
      className={cn(
        'flex h-12 items-center justify-center gap-1 rounded-2xl bg-muted/60 px-4 py-2 transition-all',
        isRecording ? 'border border-brand/30 bg-brand-soft/20 ring-4 ring-brand/10' : '',
        className,
      )}
    >
      {Array.from({ length: barCount }).map((_, index) => {
        // Compute pseudo frequency offset based on index and volumeLevel
        const centerDistance = Math.abs(index - barCount / 2) / (barCount / 2)
        const curve = 1 - centerDistance * 0.4
        const rawHeight = isRecording ? Math.max(12, Math.min(100, (volumeLevel + Math.sin(index * 1.5) * 20) * curve)) : 8

        return (
          <div
            key={index}
            className={cn(
              'w-1.5 rounded-full transition-all duration-75',
              isRecording
                ? 'bg-gradient-to-t from-brand to-logo'
                : 'bg-muted-foreground/30',
            )}
            style={{
              height: `${rawHeight}%`,
              opacity: isRecording ? 0.7 + (rawHeight / 100) * 0.3 : 0.4,
            }}
          />
        )
      })}
    </div>
  )
}
