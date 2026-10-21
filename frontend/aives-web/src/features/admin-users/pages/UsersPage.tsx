import { CircleAlert, CircleCheck, Eye, Pencil, Plus, RotateCw, Search, Trash2, UserRound } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { Button } from '@/components/ui/button'
import { ROLE_LABEL, useAuth } from '@/features/auth'
import { toApiError } from '@/lib/http/apiError'
import { cn } from '@/lib/utils'
import { usersApi } from '../api/usersApi'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { useApi } from '@/lib/http/useApi'
import { UserFormDialog, type UserFormMode } from '../components/UserFormDialog'
import type { User, UserRole } from '../types'

type Filter = 'ALL' | UserRole

const FILTERS: { value: Filter; label: string }[] = [
  { value: 'ALL', label: 'All' },
  { value: 'STAFF', label: ROLE_LABEL.STAFF },
  { value: 'EXAMINER', label: ROLE_LABEL.EXAMINER },
  { value: 'STUDENT', label: ROLE_LABEL.STUDENT },
  { value: 'ADMIN', label: ROLE_LABEL.ADMIN },
]

const ROLE_ORDER: Record<UserRole, number> = { ADMIN: 0, STAFF: 1, EXAMINER: 2, STUDENT: 3 }

const ROLE_TONE: Record<UserRole, string> = {
  ADMIN: 'bg-brand-soft text-brand',
  STAFF: 'bg-success-soft text-success',
  EXAMINER: 'bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300',
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
  const [attempt, setAttempt] = useState(0)
  const [deleting, setDeleting] = useState<User | null>(null)
  const [deletePending, setDeletePending] = useState(false)
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const [viewing, setViewing] = useState<User | null>(null)

  async function handleDelete() {
    if (!deleting || deletePending) return
    setDeletePending(true)
    setDeleteError(null)
    try {
      await usersApi.delete(deleting.userId)
      setUsers((previous) => previous.filter((user) => user.userId !== deleting.userId))
      setFlash(`Deleted account ${deleting.username}.`)
      setDeleting(null)
    } catch (err) { setDeleteError(toApiError(err).message) }
    finally { setDeletePending(false) }
  }


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
        if (!cancelled) setLoad({ status: 'error', message: toApiError(err, 'Could not load users.').message })
      })
    return () => {
      cancelled = true
    }
  }, [attempt])

  const counts = useMemo(() => {
    const c: Record<Filter, number> = { ALL: users.length, ADMIN: 0, STAFF: 0, EXAMINER: 0, STUDENT: 0 }
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
    setFlash(created ? `Created account ${saved.username} (${ROLE_LABEL[saved.role]}).` : `Saved changes for ${saved.fullName}.`)
    setDialog(null)
  }

  return (
    <>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-3xl font-medium tracking-tight">Users</h1>
          <p className="mt-1.5 max-w-2xl text-muted-foreground">
            Create staff, examiner, student and admin accounts; edit details and change roles.
          </p>
        </div>
        <Button onClick={() => setDialog({ type: 'create' })}>
          <Plus aria-hidden="true" />
          Add user
        </Button>
      </header>
      <div role="status" aria-live="polite">
        {flash && (
          <p className="mt-6 flex items-center gap-2 rounded-[14px] bg-success-soft px-4 py-3 text-sm text-success">
            <CircleCheck className="size-4 shrink-0" aria-hidden="true" />
            {flash}
          </p>
        )}
      </div>

      <div className="mt-6 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div role="group" aria-label="Filter by role" className="flex gap-1 overflow-x-auto">
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
          <span className="sr-only">Search users</span>
          <Search className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
          <input
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search by name, username or email"
            className="h-10 w-full rounded-full border border-input bg-background pr-4 pl-10 text-sm outline-none focus-visible:border-brand focus-visible:ring-4 focus-visible:ring-brand/15"
          />
        </label>
      </div>

      <section aria-label="User list" className="mt-4 overflow-hidden rounded-2xl border">
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
              Try again
            </Button>
          </div>
        )}

        {load.status === 'ready' && visible.length === 0 && (
          <div className="flex flex-col items-center gap-2 px-6 py-14 text-center">
            <UserRound className="size-7 text-muted-foreground" aria-hidden="true" />
            <p className="text-muted-foreground">
              {users.length === 0 ? 'No users yet.' : 'No users match these filters.'}
            </p>
          </div>
        )}

        {load.status === 'ready' && visible.length > 0 && (
          <ul className="divide-y">
            <li
              aria-hidden="true"
              className="hidden grid-cols-[minmax(0,1.4fr)_minmax(0,1.4fr)_8rem_8rem] gap-4 bg-muted/60 px-5 py-2.5 text-xs font-medium text-muted-foreground uppercase md:grid"
            >
              <span>User</span>
              <span>Email</span>
              <span>Role</span>
              <span className="text-right">Actions</span>
            </li>
            {visible.map((u) => (
              <UserRow
                key={u.userId}
                user={u}
                isSelf={u.userId === me?.userId}
                onView={() => setViewing(u)}
                onDelete={() => { setDeleting(u); setDeleteError(null) }}
                onEdit={() => {
                  setFlash(null)
                  setDialog({ type: 'edit', user: u })
                }}
              />
            ))}
          </ul>
        )}
      </section>

      <ConfirmDialog open={deleting !== null} title="Delete user?" description={`Delete account ${deleting?.username ?? ''}? Accounts with related data cannot be deleted.`} pending={deletePending} error={deleteError} onClose={() => setDeleting(null)} onConfirm={() => void handleDelete()} />
      {viewing && <UserDetail user={viewing} onClose={() => setViewing(null)} />}

      <UserFormDialog
        mode={dialog}
        currentUserId={me?.userId ?? -1}
        onClose={() => setDialog(null)}
        onSaved={handleSaved}
      />
    </>
  )
}

function UserRow({ user, isSelf, onEdit, onDelete, onView }: { user: User; isSelf: boolean; onEdit: () => void; onDelete: () => void; onView: () => void }) {
  return (
    <li className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-4 gap-y-1 px-5 py-4 md:grid-cols-[minmax(0,1.4fr)_minmax(0,1.4fr)_8rem_8rem]">
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
            {isSelf && <span className="shrink-0 rounded-full bg-muted px-2 py-0.5 text-xs text-muted-foreground">You</span>}
          </p>
          <p className="truncate text-sm text-muted-foreground">@{user.username}</p>
        </div>
      </div>

      <p className="col-span-2 row-start-2 truncate pl-12 text-sm text-muted-foreground md:col-span-1 md:row-start-auto md:pl-0 md:text-foreground">
        {user.email ?? 'No email'}
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
        <Button variant="ghost" size="icon-sm" onClick={onView} aria-label={`View ${user.fullName}`} title="View"><Eye aria-hidden="true" /></Button>
        <Button variant="ghost" size="icon-sm" onClick={onEdit} aria-label={`Edit ${user.fullName}`} title="Edit">
          <Pencil aria-hidden="true" />
        </Button>
        <Button
          variant="ghost"
          size="icon-sm"
          disabled={isSelf}
          onClick={onDelete}
          aria-label={`Delete ${user.fullName}`}
          title={isSelf ? 'You cannot delete your own account' : 'Delete account'}
        >
          <Trash2 aria-hidden="true" />
        </Button>
      </div>
    </li>
  )
}

function UserDetail({ user, onClose }: { user: User; onClose: () => void }) {
  const { state, reload } = useApi(() => usersApi.get(user.userId), user.userId)
  return <Dialog open onOpenChange={(open) => !open && onClose()}><DialogContent>
    <DialogHeader><DialogTitle>User details</DialogTitle><DialogDescription>Current account information.</DialogDescription></DialogHeader>
    {state.status === 'loading' ? <p>Loading…</p> : state.status === 'error' ? <p role="alert">{state.message} <Button onClick={reload}>Try again</Button></p> : <dl className="space-y-3 text-sm">
      {[['Full name', state.data.fullName], ['Username', state.data.username], ['Email', state.data.email ?? 'Not set'], ['Role', ROLE_LABEL[state.data.role]]].map(([label, value]) => <div key={label}><dt className="text-muted-foreground">{label}</dt><dd className="break-words font-medium">{value}</dd></div>)}
    </dl>}
  </DialogContent></Dialog>
}

function ListSkeleton() {
  return (
    <ul aria-busy="true" aria-label="Loading" className="divide-y">
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
function normalize(s: string) {
  return s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'd')
    .toLowerCase()
    .trim()
}
