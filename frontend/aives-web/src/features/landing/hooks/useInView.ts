import { useEffect, useRef, useState } from 'react'

/** True once the element has scrolled into view; stays true afterwards (reveal runs once). */
export function useInView<T extends Element>(threshold = 0.15) {
  const ref = useRef<T>(null)
  // Without IntersectionObserver support, show everything immediately.
  const [inView, setInView] = useState(() => typeof IntersectionObserver === 'undefined')

  useEffect(() => {
    const el = ref.current
    if (!el || typeof IntersectionObserver === 'undefined') return

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setInView(true)
          observer.disconnect()
        }
      },
      { threshold, rootMargin: '0px 0px -60px 0px' },
    )
    observer.observe(el)
    return () => observer.disconnect()
  }, [threshold])

  return { ref, inView }
}
