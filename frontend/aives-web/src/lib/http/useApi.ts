import { useCallback, useEffect, useState } from 'react'
import { toApiError } from './apiError'

export type ApiState<T> =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; data: T }

/**
 * Loads data on mount and whenever `key` changes; `reload()` refetches (e.g. a "Try again" button),
 * `update()` patches the loaded data after a create/edit so the list doesn't need a refetch.
 * `load` is read on each run but not tracked; pass whatever it depends on through `key`.
 */
export function useApi<T>(load: () => Promise<T>, key: string | number = '', errorFallback?: string) {
  const [state, setState] = useState<ApiState<T>>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let cancelled = false
    load()
      .then((data) => {
        if (!cancelled) setState({ status: 'ready', data })
      })
      .catch((err) => {
        if (!cancelled) setState({ status: 'error', message: toApiError(err, errorFallback).message })
      })
    return () => {
      cancelled = true
    }
    // `load` is intentionally untracked (callers pass inline functions); `key` drives refetching.
  }, [attempt, key]) // eslint-disable-line react-hooks/exhaustive-deps

  const reload = useCallback(() => {
    setState({ status: 'loading' })
    setAttempt((n) => n + 1)
  }, [])

  const update = useCallback((fn: (data: T) => T) => {
    setState((s) => (s.status === 'ready' ? { status: 'ready', data: fn(s.data) } : s))
  }, [])

  return { state, reload, update }
}
