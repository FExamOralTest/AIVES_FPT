import { Construction } from 'lucide-react'
import type { FeatureDef } from '../features'
import { EndpointStateBadge, StatusBadge } from '../layout/StatusBadge'

/** Blank page for a pending feature: shows what it will do and which backend APIs it still needs. */
export default function FeaturePlaceholderPage({ feature }: { feature: FeatureDef }) {
  const { title, summary, status, endpoints = [], blockers, icon: Icon } = feature

  return (
    <>
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex min-w-0 items-start gap-4">
          <span className="flex size-12 shrink-0 items-center justify-center rounded-2xl bg-muted">
            <Icon className="size-6" aria-hidden="true" />
          </span>
          <div className="min-w-0">
            <h1 className="font-heading text-3xl font-medium tracking-tight">{title}</h1>
            <p className="mt-1.5 max-w-2xl text-muted-foreground">{summary}</p>
          </div>
        </div>
        <StatusBadge status={status} className="h-7 px-3 text-[13px]" />
      </header>

      <section
        aria-label="Page content"
        className="mt-8 flex min-h-[280px] flex-col items-center justify-center gap-3 rounded-3xl border-2 border-dashed px-6 py-12 text-center"
      >
        <Construction className="size-9 text-muted-foreground" aria-hidden="true" />
        <h2 className="font-heading text-xl font-medium">Coming soon</h2>
        <p className="max-w-md text-muted-foreground">
          This feature is waiting for the backend APIs. The page will be built as soon as they are ready.
        </p>
        {blockers?.length ? (
          <ul className="mt-2 max-w-lg space-y-1.5 text-left text-sm">
            {blockers.map((b) => (
              <li key={b} className="rounded-xl bg-warning-soft px-3.5 py-2.5 text-warning">
                {b}
              </li>
            ))}
          </ul>
        ) : null}
      </section>

      {endpoints.length > 0 && (
        <section aria-labelledby="api-heading" className="mt-10">
          <h2 id="api-heading" className="font-heading text-lg font-medium">
            APIs needed
          </h2>
          <ul className="mt-4 divide-y rounded-2xl border">
            {endpoints.map((e) => (
              <li key={`${e.method} ${e.path}`} className="flex flex-col gap-2 p-4 sm:flex-row sm:items-start sm:gap-4">
                <div className="min-w-0 flex-1">
                  <code className="font-mono text-[13px] break-all">
                    <span className="mr-2 font-semibold text-brand">{e.method}</span>
                    {e.path}
                  </code>
                  <p className="mt-1 text-sm text-muted-foreground">{e.note}</p>
                </div>
                <EndpointStateBadge state={e.state} />
              </li>
            ))}
          </ul>
        </section>
      )}
    </>
  )
}
