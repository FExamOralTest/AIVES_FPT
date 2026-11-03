import {
  BookOpen,
  CircleAlert,
  ClipboardCheck,
  Eye,
  GraduationCap,
  LayoutDashboard,
  LayoutGrid,
  Pencil,
  PlusCircle,
  RotateCw,
  ShieldCheck,
  Trash2,
  UserRound,
  Users,
  type LucideIcon,
} from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { Button } from '@/components/ui/button'
import {
  ActionMenu,
  FilterTabs,
  FlashBar,
  GradientTile,
  PageBar,
  SortHeader,
  SortMenu,
  TableCheckbox,
  TableSkeleton,
  Toolbar,
  ToolbarSearch,
  TOOLBAR_BUTTON,
} from '@/components/common/FlatPage'
import { ROLE_HOME, ROLE_LABEL, useAuth } from '@/features/auth'
import { toApiError } from '@/lib/http/apiError'
import { cn } from '@/lib/utils'
import { usersApi } from '../api/usersApi'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { useApi } from '@/lib/http/useApi'
import { UserFormDialog, type UserFormMode } from '../components/UserFormDialog'
import type { User, UserRole } from '../types'

type Filter = 'ALL' | UserRole

const FILTERS: { value: Filter; label: string; icon: LucideIcon }[] = [
  { value: 'ALL', label: 'All users', icon: LayoutGrid },
  { value: 'STAFF', label: ROLE_LABEL.STAFF, icon: BookOpen },
  { value: 'EXAMINER', label: ROLE_LABEL.EXAMINER, icon: ClipboardCheck },
  { value: 'STUDENT', label: ROLE_LABEL.STUDENT, icon: GraduationCap },
  { value: 'ADMIN', label: ROLE_LABEL.ADMIN, icon: ShieldCheck },
]

const ROLE_ORDER: Record<UserRole, number> = { ADMIN: 0, STAFF: 1, EXAMINER: 2, STUDENT: 3 }

// Soft tag colours, one hue per role.
const ROLE_TAG: Record<UserRole, string> = {
  ADMIN: 'bg-indigo-50 text-indigo-600 dark:bg-indigo-950/50 dark:text-indigo-300',
  STAFF: 'bg-cyan-50 text-cyan-700 dark:bg-cyan-950/50 dark:text-cyan-300',
  EXAMINER: 'bg-orange-50 text-orange-600 dark:bg-orange-950/50 dark:text-orange-300',
  STUDENT: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300',
}

type SortKey = 'role' | 'fullName' | 'username' | 'email'
type Sort = { key: SortKey; dir: 'asc' | 'desc' }

const SORT_LABEL: Record<SortKey, string> = { role: 'Role', fullName: 'Name', username: 'Username', email: 'Email' }

function compareUsers(a: User, b: User, key: SortKey) {
  if (key === 'role') return ROLE_ORDER[a.role] - ROLE_ORDER[b.role] || a.fullName.localeCompare(b.fullName, 'vi')
  return (a[key] ?? '').localeCompare(b[key] ?? '', 'vi')
}

type LoadState = { status: 'loading' } | { status: 'error'; message: string } | { status: 'ready' }

export default function UsersPage() {
  const { user: me } = useAuth()
  const [users, setUsers] = useState<User[]>([])
  const [load, setLoad] = useState<LoadState>({ status: 'loading' })
  const [query, setQuery] = useState('')
  const [searchOpen, setSearchOpen] = useState(false)
  const [filter, setFilter] = useState<Filter>('ALL')
  const [sort, setSort] = useState<Sort>({ key: 'role', dir: 'asc' })
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [dialog, setDialog] = useState<UserFormMode | null>(null)
  const [flash, setFlash] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)
  // Either one row from its menu, or every selected row.
  const [deleting, setDeleting] = useState<User[] | null>(null)
  const [deletePending, setDeletePending] = useState(false)
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const [viewing, setViewing] = useState<User | null>(null)

  async function handleDelete() {
    if (!deleting || deletePending) return
    setDeletePending(true)
    setDeleteError(null)
    const deleted: number[] = []
    const failed: string[] = []
    for (const user of deleting) {
      try {
        await usersApi.delete(user.userId)
        deleted.push(user.userId)
      } catch (err) {
        failed.push(deleting.length === 1 ? toApiError(err).message : user.username)
      }
    }
    setUsers((previous) => previous.filter((user) => !deleted.includes(user.userId)))
    setSelected((previous) => new Set([...previous].filter((id) => !deleted.includes(id))))
    setDeletePending(false)
    if (failed.length === 0) {
      setFlash(deleted.length === 1 ? `Deleted account ${deleting[0].username}.` : `Deleted ${deleted.length} accounts.`)
      setDeleting(null)
    } else if (deleting.length === 1) {
      setDeleteError(failed[0])
    } else {
      setDeleting((previous) => previous?.filter((user) => !deleted.includes(user.userId)) ?? null)
      setDeleteError(`Deleted ${deleted.length}. These accounts have related data and were kept: ${failed.join(', ')}.`)
    }
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
    const sign = sort.dir === 'asc' ? 1 : -1
    return users
      .filter((u) => filter === 'ALL' || u.role === filter)
      .filter((u) => !q || [u.fullName, u.username, u.email ?? ''].some((s) => normalize(s).includes(q)))
      .sort((a, b) => sign * compareUsers(a, b, sort.key))
  }, [users, query, filter, sort])

  // You cannot delete your own account, so it is never selectable.
  const selectable = visible.filter((u) => u.userId !== me?.userId)
  const selectedVisible = selectable.filter((u) => selected.has(u.userId))

  function toggleAll() {
    setSelected((previous) => {
      const next = new Set(previous)
      const allOn = selectable.length > 0 && selectedVisible.length === selectable.length
      for (const u of selectable) {
        if (allOn) next.delete(u.userId)
        else next.add(u.userId)
      }
      return next
    })
  }

  function toggleOne(userId: number) {
    setSelected((previous) => {
      const next = new Set(previous)
      if (!next.delete(userId)) next.add(userId)
      return next
    })
  }

  function sortBy(key: SortKey) {
    setSort((previous) => ({ key, dir: previous.key === key && previous.dir === 'asc' ? 'desc' : 'asc' }))
  }

  function reload() {
    setLoad({ status: 'loading' })
    setAttempt((n) => n + 1)
  }

  function handleSaved(saved: User, created: boolean) {
    setUsers((prev) => (created ? [...prev, saved] : prev.map((u) => (u.userId === saved.userId ? saved : u))))
    setFlash(created ? `Created account ${saved.username} (${ROLE_LABEL[saved.role]}).` : `Saved changes for ${saved.fullName}.`)
    setDialog(null)
  }

  const header = (key: SortKey, label: string, count?: number) => (
    <SortHeader label={label} count={count} active={sort.key === key} dir={sort.dir} onSort={() => sortBy(key)} />
  )

  return (
    <>
      <PageBar
        parent={{ label: 'Administration', to: ROLE_HOME.ADMIN, icon: LayoutDashboard }}
        title="Users"
        icon={Users}
        actions={[
          { label: 'New user', icon: PlusCircle, onSelect: () => setDialog({ type: 'create' }) },
          { label: 'Reload list', icon: RotateCw, onSelect: reload },
        ]}
      />
      <FlashBar message={flash} />

      <section aria-label="User list">
        <Toolbar>
          <FilterTabs
            label="Filter by role"
            tabs={FILTERS.map((f) => ({ ...f, count: counts[f.value] }))}
            value={filter}
            onChange={setFilter}
          />
          <div className="flex items-center gap-2">
            {selectedVisible.length > 0 && (
              <Button
                variant="destructive"
                className={TOOLBAR_BUTTON}
                onClick={() => {
                  setDeleting(selectedVisible)
                  setDeleteError(null)
                }}
              >
                <Trash2 aria-hidden="true" />
                Delete {selectedVisible.length}
              </Button>
            )}
            <SortMenu labels={SORT_LABEL} sort={sort} onSort={setSort} />
            <ToolbarSearch
              open={searchOpen}
              onOpenChange={setSearchOpen}
              value={query}
              onChange={setQuery}
              label="Search users"
              placeholder="Search name, username or email"
            />
            <Button variant="brand" className={TOOLBAR_BUTTON} onClick={() => setDialog({ type: 'create' })}>
              <PlusCircle aria-hidden="true" />
              New user
            </Button>
          </div>
        </Toolbar>

        {load.status === 'loading' && <TableSkeleton />}

        {load.status === 'error' && (
          <div className="flex flex-col items-center gap-3 px-6 py-14 text-center">
            <CircleAlert className="size-7 text-destructive" aria-hidden="true" />
            <p className="max-w-sm text-muted-foreground">{load.message}</p>
            <Button variant="secondary" size="sm" onClick={reload}>
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
          <div className="overflow-x-auto">
            <table className="w-full min-w-[820px] table-fixed text-left text-[15px]">
              <colgroup>
                <col className="w-12 sm:w-14" />
                <col className="w-[31%]" />
                <col className="w-[22%]" />
                <col />
                <col className="w-40" />
              </colgroup>
              <thead>
                <tr className="border-b">
                  <th scope="col" className="py-3 pr-1 pl-4 sm:pl-6">
                    <TableCheckbox
                      label="Select all users"
                      checked={selectable.length > 0 && selectedVisible.length === selectable.length}
                      indeterminate={selectedVisible.length > 0 && selectedVisible.length < selectable.length}
                      disabled={selectable.length === 0}
                      onChange={toggleAll}
                    />
                  </th>
                  {header('fullName', 'User', visible.length)}
                  {header('username', 'Username')}
                  {header('email', 'Email')}
                  {header('role', 'Role')}
                </tr>
              </thead>
              <tbody className="divide-y">
                {visible.map((u) => (
                  <UserRow
                    key={u.userId}
                    user={u}
                    isSelf={u.userId === me?.userId}
                    selected={selected.has(u.userId)}
                    onToggle={() => toggleOne(u.userId)}
                    onView={() => setViewing(u)}
                    onDelete={() => {
                      setDeleting([u])
                      setDeleteError(null)
                    }}
                    onEdit={() => {
                      setFlash(null)
                      setDialog({ type: 'edit', user: u })
                    }}
                  />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <ConfirmDialog
        open={deleting !== null}
        title={deleting && deleting.length > 1 ? `Delete ${deleting.length} users?` : 'Delete user?'}
        description={
          deleting && deleting.length > 1
            ? `Delete ${deleting.length} accounts? Accounts with related data cannot be deleted.`
            : `Delete account ${deleting?.[0]?.username ?? ''}? Accounts with related data cannot be deleted.`
        }
        pending={deletePending}
        error={deleteError}
        onClose={() => setDeleting(null)}
        onConfirm={() => void handleDelete()}
      />
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

function UserRow({
  user,
  isSelf,
  selected,
  onToggle,
  onEdit,
  onDelete,
  onView,
}: {
  user: User
  isSelf: boolean
  selected: boolean
  onToggle: () => void
  onEdit: () => void
  onDelete: () => void
  onView: () => void
}) {
  return (
    <tr className={cn('transition-colors hover:bg-muted/50', selected && 'bg-brand-soft/50 hover:bg-brand-soft/70')}>
      <td className="py-3.5 pr-1 pl-4 sm:pl-6">
        <TableCheckbox label={`Select ${user.fullName}`} checked={selected} disabled={isSelf} onChange={onToggle} />
      </td>
      <td className="max-w-0 px-3 py-3.5">
        <div className="flex min-w-0 items-center gap-3">
          <GradientTile seed={user.userId} />
          <span className="truncate">{user.fullName}</span>
          {isSelf && <span className="shrink-0 rounded-md bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">You</span>}
          <ActionMenu
            label={`Actions for ${user.fullName}`}
            actions={[
              { label: 'View details', icon: Eye, onSelect: onView },
              { label: 'Edit', icon: Pencil, onSelect: onEdit },
              {
                label: isSelf ? 'Cannot delete yourself' : 'Delete account',
                icon: Trash2,
                onSelect: onDelete,
                disabled: isSelf,
                destructive: true,
                separated: true,
              },
            ]}
          />
        </div>
      </td>
      <td className="max-w-0 truncate px-3 py-3.5 text-muted-foreground">@{user.username}</td>
      <td className="max-w-0 truncate px-3 py-3.5">
        {user.email ? (
          <a href={`mailto:${user.email}`} className="text-brand hover:underline">
            {user.email}
          </a>
        ) : (
          <span className="text-muted-foreground">No email</span>
        )}
      </td>
      <td className="px-3 py-3.5">
        <span className={cn('inline-flex rounded-md px-2 py-0.5 text-[13px] font-medium', ROLE_TAG[user.role])}>
          {ROLE_LABEL[user.role]}
        </span>
      </td>
    </tr>
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

function normalize(s: string) {
  return s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'd')
    .toLowerCase()
    .trim()
}
