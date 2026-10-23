import { useEffect, useRef, useState } from 'react'

/**
 * Milliseconds left until `endsAt` (epoch ms, never below 0), ticking every second; null when there is no deadline.
 * `onExpire` is called from the timer tick once the deadline has passed (also when it had already passed on mount).
 */
export function useCountdown(endsAt: number | null, onExpire?: () => void): number | null {
  const [now, setNow] = useState(() => Date.now())
  const onExpireRef = useRef(onExpire)

  useEffect(() => {
    onExpireRef.current = onExpire
  }, [onExpire])

  useEffect(() => {
    if (endsAt === null) return
    let expired = false
    const tick = () => {
      const current = Date.now()
      setNow(current)
      if (!expired && current >= endsAt) {
        expired = true
        onExpireRef.current?.()
      }
    }
    const id = window.setInterval(tick, 1000)
    return () => window.clearInterval(id)
  }, [endsAt])

  return endsAt === null ? null : Math.max(0, endsAt - now)
}
