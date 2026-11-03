import type { CSSProperties } from 'react'

/**
 * Helpers for artwork laid out in a design file's own pixel coordinates.
 * The element holding the artwork is an `@container` whose width stands for `frameWidth`
 * design px; every length goes through `u()` into container-width units, so the whole
 * composition scales as one piece and keeps the design's proportions at any size.
 */
export function designFrame(frameWidth: number) {
  const u = (px: number) => `${((px * 100) / frameWidth).toFixed(3)}cqw`
  const box = (x: number, y: number, w: number, h?: number): CSSProperties => ({
    left: u(x),
    top: u(y),
    width: u(w),
    height: h === undefined ? undefined : u(h),
  })
  return { u, box }
}
