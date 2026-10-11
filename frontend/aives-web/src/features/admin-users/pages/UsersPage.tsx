import { CircleAlert, CircleCheck, Pencil, Plus, RotateCw, Search, Trash2, UserRound } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { Button } from '@/components/ui/button'
import { ROLE_LABEL, useAuth } from '@/features/auth'
import { toApiError } from '@/lib/http/apiError'
import { cn } from '@/lib/utils'
import { usersApi } from '../api/usersApi'
import { UserFormDialog, type UserFormMode } from '../components/UserFormDialog'
import type { User, UserRole } from '../types'

type Filter = 'ALL' | UserRole

const FILTERS: { value: Filter; label: string }[] = [
  { value: 'ALL', label: 'Tất cả' },
  { value: 'TEACHER', label: ROLE_LABEL.TEACHER },
  { value: 'STUDENT', label: ROLE_LABEL.STUDENT },
  { value: 'ADMIN', label: ROLE_LABEL.ADMIN },
]

const ROLE_ORDER: Record<UserRole, number> = { ADMIN: 0, TEACHER: 1, STUDENT: 2 }

const ROLE_TONE: Record<UserRole, string> = {
  ADMIN: 'bg-brand-soft text-brand',
  TEACHER: 'bg-success-soft text-success',
  STUDENT: 'bg-muted text-foreground',
}

type LoadState = { status: 'loading' } | { status: 'error'; message: string } | { status: 'ready' }

export default function UsersPage() {
  const { user: me } = useAuth()
  const [users, setUsers] = useState<User[]>([])
  const [load, setLoad] = useState<LoadState>({ status: 'loading' })
  const [query, setQuery] = useState('')
  const [filter, setFilter] = useState<Filter>('ALL')
  const [dialog, setDialog] = useState<UserFormMode | null>(null)
  const [flash, setFlash] = useState<string | null>(null)
  // Bumped by "Thử lại" to refetch; load starts as 'loading' and retries reset it in the handler.
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let cancelled = false
    usersApi
      .list()
      .then((list) => {
        if (cancelled) return
        setUsers(list)
        setLoad({ status: 'ready' })
      })
      .catch((err) => {
        if (!cancelled) setLoad({ status: 'error', message: toApiError(err, 'Không tải được danh sách người dùng.').message })
      })
    return () => {
      cancelled = true
    }
  }, [attempt])

  const counts = useMemo(() => {
    const c: Record<Filter, number> = { ALL: users.length, ADMIN: 0, TEACHER: 0, STUDENT: 0 }
    for (const u of users) c[u.role]++
    return c
  }, [users])

  const visible = useMemo(() => {
    const q = normalize(query)
    return users
      .filter((u) => filter === 'ALL' || u.role === filter)
      .filter((u) => !q || [u.fullName, u.username, u.email ?? ''].some((s) => normalize(s).includes(q)))
      .sort((a, b) => ROLE_ORDER[a.role] - ROLE_ORDER[b.role] || a.fullName.localeCompare(b.fullName, 'vi'))
  }, [users, query, filter])

  function handleSaved(saved: User, created: boolean) {
    setUsers((prev) => (created ? [...prev, saved] : prev.map((u) => (u.userId === saved.userId ? saved : u))))
    setFlash(created ? `Đã tạo tài khoản ${saved.username} (${ROLE_LABEL[saved.role]}).` : `Đã lưu thay đổi cho ${saved.fullName}.`)
    setDialog(null)
  }

  return (
    <>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-3xl font-medium tracking-tight">Người dùng</h1>
          <p className="mt-1.5 max-w-2xl text-muted-foreground">
            Tạo tài khoản giảng viên, sinh viên và quản trị viên; sửa thông tin và đổi vai trò.
          </p>
        </div>
        <Button onClick={() => setDialog({ type: 'create' })}>
          <Plus aria-hidden="true" />
          Thêm người dùng
        </Button>
      </header>

      {/* Polite live region: announces create/update results without stealing focus. */}
      <div role="status" aria-live="polite">
        {flash && (
          <p className="mt-6 flex items-center gap-2 rounded-[14px] bg-success-soft px-4 py-3 text-sm text-success">
            <CircleCheck className="size-4 shrink-0" aria-hidden="true" />
            {flash}
          </p>
        )}
      </div>

      <div className="mt-6 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div role="group" aria-label="Lọc theo vai trò" className="flex gap-1 overflow-x-auto">
          {FILTERS.map(({ value, label }) => (
            <button
              key={value}
              type="button"
              onClick={() => setFilter(value)}
              aria-pressed={filter === value}
              className={cn(
                'inline-flex h-9 shrink-0 items-center gap-1.5 rounded-full px-3.5 text-sm transition-colors',
                filter === value ? 'bg-foreground text-background' : 'bg-muted text-muted-foreground hover:text-foreground',
              )}
            >
              {label}
              <span className={cn('tabular-nums', filter === value ? 'text-background/70' : 'text-muted-foreground/80')}>
                {counts[value]}
              </span>
            </button>
          ))}
        </div>
        <label className="relative block lg:w-72">
          <span className="sr-only">Tìm người dùng</span>
          <Search className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
          <input
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Tìm theo tên, tên đăng nhập, email"
            className="h-10 w-full rounded-full border border-input bg-background pr-4 pl-10 text-sm outline-none focus-visible:border-brand focus-visible:ring-4 focus-visible:ring-brand/15"
          />
        </label>
      </div>

      <section aria-label="Danh sách người dùng" className="mt-4 overflow-hidden rounded-2xl border">
        {load.status === 'loading' && <ListSkeleton />}

        {load.status === 'error' && (
          <div className="flex flex-col items-center gap-3 px-6 py-14 text-center">
            <CircleAlert className="size-7 text-destructive" aria-hidden="true" />
            <p className="max-w-sm text-muted-foreground">{load.message}</p>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => {
                setLoad({ status: 'loading' })
                setAttempt((n) => n + 1)
              }}
            >
              <RotateCw aria-hidden="true" />
              Thử lại
            </Button>
          </div>
        )}

        {load.status === 'ready' && visible.length === 0 && (
          <div className="flex flex-col items-center gap-2 px-6 py-14 text-center">
            <UserRound className="size-7 text-muted-foreground" aria-hidden="true" />
            <p className="text-muted-foreground">
              {users.length === 0 ? 'Chưa có người dùng nào.' : 'Không có người dùng phù hợp với bộ lọc.'}
            </p>
          </div>
        )}

        {load.status === 'ready' && visible.length > 0 && (
          <ul className="divide-y">
            <li
              aria-hidden="true"
              className="hidden grid-cols-[minmax(0,1.4fr)_minmax(0,1.4fr)_8rem_6.5rem] gap-4 bg-muted/60 px-5 py-2.5 text-xs font-medium text-muted-foreground uppercase md:grid"
            >
              <span>Người dùng</span>
              <span>Email</span>
              <span>Vai trò</span>
              <span className="text-right">Thao tác</span>
            </li>
            {visible.map((u) => (
              <UserRow
                key={u.userId}
                user={u}
                isSelf={u.userId === me?.userId}
                onEdit={() => {
                  setFlash(null)
                  setDialog({ type: 'edit', user: u })
                }}
              />
            ))}
          </ul>
        )}
      </section>

      <p className="mt-3 text-[13px] text-muted-foreground">
        Xoá tài khoản đang hoàn thiện: chờ backend sửa lỗi API xoá người dùng.
      </p>

      <UserFormDialog
        mode={dialog}
        currentUserId={me?.userId ?? -1}
        onClose={() => setDialog(null)}
        onSaved={handleSaved}
      />
    </>
  )
}

function UserRow({ user, isSelf, onEdit }: { user: User; isSelf: boolean; onEdit: () => void }) {
  return (
    <li className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-4 gap-y-1 px-5 py-4 md:grid-cols-[minmax(0,1.4fr)_minmax(0,1.4fr)_8rem_6.5rem]">
      <div className="flex min-w-0 items-center gap-3">
        <span
          aria-hidden="true"
          className="flex size-9 shrink-0 items-center justify-center rounded-full bg-muted text-sm font-semibold text-muted-foreground"
        >
          {initials(user.fullName)}
        </span>
        <div className="min-w-0">
          <p className="flex items-center gap-2 truncate font-medium">
            <span className="truncate">{user.fullName}</span>
            {isSelf && <span className="shrink-0 rounded-full bg-muted px-2 py-0.5 text-xs text-muted-foreground">Bạn</span>}
          </p>
          <p className="truncate text-sm text-muted-foreground">@{user.username}</p>
        </div>
      </div>

      <p className="col-span-2 row-start-2 truncate pl-12 text-sm text-muted-foreground md:col-span-1 md:row-start-auto md:pl-0 md:text-foreground">
        {user.email ?? 'Chưa có email'}
      </p>

      <span
        className={cn(
          'col-start-2 row-start-1 w-fit rounded-full px-2.5 py-1 text-xs font-medium md:col-start-auto md:row-start-auto',
          ROLE_TONE[user.role],
        )}
      >
        {ROLE_LABEL[user.role]}
      </span>

      <div className="col-span-2 row-start-3 flex justify-end gap-1 md:col-span-1 md:row-start-auto">
        <Button variant="ghost" size="icon-sm" onClick={onEdit} aria-label={`Sửa ${user.fullName}`} title="Sửa">
          <Pencil aria-hidden="true" />
        </Button>
        <Button
          variant="ghost"
          size="icon-sm"
          disabled
          aria-label={`Xoá ${user.fullName} (đang hoàn thiện)`}
          title="Xoá tài khoản: đang hoàn thiện"
        >
          <Trash2 aria-hidden="true" />
        </Button>
      </div>
    </li>
  )
}

function ListSkeleton() {
  return (
    <ul aria-busy="true" aria-label="Đang tải" className="divide-y">
      {Array.from({ length: 4 }, (_, i) => (
        <li key={i} className="flex items-center gap-3 px-5 py-4">
          <span className="size-9 animate-pulse rounded-full bg-muted motion-reduce:animate-none" />
          <span className="h-4 w-40 animate-pulse rounded bg-muted motion-reduce:animate-none" />
          <span className="ml-auto h-4 w-20 animate-pulse rounded bg-muted motion-reduce:animate-none" />
        </li>
      ))}
    </ul>
  )
}

function initials(name: string) {
  const parts = name.trim().split(/\s+/)
  const letters = parts.length > 1 ? parts[0][0] + parts[parts.length - 1][0] : parts[0]?.slice(0, 2) ?? ''
  return letters.toUpperCase()
}

/** Lowercase and strip Vietnamese diacritics so "huyen" matches "Huyền". */
function normalize(s: string) {
  return s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'd')
    .toLowerCase()
    .trim()
}
