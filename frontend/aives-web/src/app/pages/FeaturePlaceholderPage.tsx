import { Construction, PlugZap } from 'lucide-react'
import type { FeatureDef } from '../features'
import { EndpointStateBadge, StatusBadge } from '../layout/StatusBadge'

const EMPTY_STATE: Record<FeatureDef['status'], { title: string; body: string }> = {
  ready: {
    title: 'Giao diện đang được xây dựng',
    body: 'API cho trang này đã gọi được. Giao diện sẽ được hoàn thiện trong bước tiếp theo.',
  },
  partial: {
    title: 'Giao diện đang được xây dựng',
    body: 'Phần lớn API đã gọi được. Những phần còn lỗi sẽ hiển thị "đang hoàn thiện" cho đến khi backend sửa xong.',
  },
  pending: {
    title: 'Đang hoàn thiện',
    body: 'Chức năng này đang chờ backend hoàn tất API. Trang sẽ được xây dựng ngay khi API sẵn sàng.',
  },
}

/** Blank page for a feature whose UI isn't built yet: shows what it will do and what the backend still owes. */
export default function FeaturePlaceholderPage({ feature }: { feature: FeatureDef }) {
  const { title, summary, status, endpoints, blockers, icon: Icon } = feature
  const empty = EMPTY_STATE[status]
  const EmptyIcon = status === 'pending' ? Construction : PlugZap

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
        aria-label="Nội dung trang"
        className="mt-8 flex min-h-[280px] flex-col items-center justify-center gap-3 rounded-3xl border-2 border-dashed px-6 py-12 text-center"
      >
        <EmptyIcon className="size-9 text-muted-foreground" aria-hidden="true" />
        <h2 className="font-heading text-xl font-medium">{empty.title}</h2>
        <p className="max-w-md text-muted-foreground">{empty.body}</p>
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

      <section aria-labelledby="api-heading" className="mt-10">
        <h2 id="api-heading" className="font-heading text-lg font-medium">
          API sử dụng
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">Trạng thái theo nhánh backend dev ngày 11/10/2026.</p>
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
    </>
  )
}
