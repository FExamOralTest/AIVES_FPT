import { ArrowDown, ArrowUp, ArrowUpDown, CircleCheck, Ellipsis, Search, type LucideIcon } from 'lucide-react'
import { DropdownMenu } from 'radix-ui'
import { useEffect, useRef, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

/*
 * Building blocks for flat, edge-to-edge list pages (feature `layout: 'flush'`):
 * a breadcrumb bar, a toolbar of small square controls and a borderless sortable table.
 */

/** Small square controls used in the toolbar row. */
export const TOOLBAR_BUTTON = 'h-9 rounded-lg px-3 text-sm font-medium'

export const MENU_CONTENT =
  'z-50 min-w-44 rounded-xl border bg-background p-1 text-sm shadow-[0_12px_32px_-12px_rgba(17,17,20,0.25)]'
export const MENU_ITEM =
  'flex cursor-pointer items-center gap-2 rounded-lg px-2.5 py-2 outline-none select-none data-[highlighted]:bg-muted data-[disabled]:pointer-events-none data-[disabled]:opacity-50 [&_svg]:size-4 [&_svg]:shrink-0'

// Small gradient tiles that tell rows apart at a glance; picked by record id.
const TILE_GRADIENTS = [
  'from-fuchsia-500 to-orange-400',
  'from-violet-600 to-pink-500',
  'from-sky-400 to-indigo-500',
  'from-emerald-400 to-cyan-500',
  'from-rose-500 to-amber-400',
  'from-indigo-500 to-fuchsia-400',
  'from-cyan-400 to-blue-600',
  'from-amber-400 to-pink-500',
]

export function GradientTile({ seed }: { seed: number }) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        'size-5 shrink-0 rounded-md bg-linear-to-br shadow-[inset_0_0_0_1px_rgba(255,255,255,0.25)]',
        TILE_GRADIENTS[Math.abs(seed) % TILE_GRADIENTS.length],
      )}
    />
  )
}

export interface MenuAction {
  label: string
  icon: LucideIcon
  onSelect: () => void
  disabled?: boolean
  destructive?: boolean
  /** Draw a divider above this item. */
  separated?: boolean
}

/** "···" button that opens a list of actions. */
export function ActionMenu({ label, actions, className }: { label: string; actions: MenuAction[]; className?: string }) {
  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger
        aria-label={label}
        className={cn(
          'inline-flex size-6 shrink-0 items-center justify-center rounded-md text-muted-foreground outline-none hover:bg-muted hover:text-foreground focus-visible:ring-3 focus-visible:ring-ring/50 aria-expanded:bg-muted aria-expanded:text-foreground',
          className,
        )}
      >
        <Ellipsis className="size-4" aria-hidden="true" />
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content align="start" sideOffset={4} className={MENU_CONTENT}>
          {actions.map(({ label, icon: Icon, onSelect, disabled, destructive, separated }) => (
            <div key={label} className="contents">
              {separated && <DropdownMenu.Separator className="my-1 h-px bg-border" />}
              <DropdownMenu.Item
                className={cn(MENU_ITEM, destructive && 'text-destructive data-[highlighted]:bg-destructive/10')}
                disabled={disabled}
                onSelect={onSelect}
              >
                <Icon aria-hidden="true" />
                {label}
              </DropdownMenu.Item>
            </div>
          ))}
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}

/** Breadcrumb bar: parent section / current page (the page's h1) and a page menu. */
export function PageBar({
  parent,
  title,
  icon: Icon,
  actions,
}: {
  parent: { label: string; to: string; icon: LucideIcon }
  title: string
  icon: LucideIcon
  actions: MenuAction[]
}) {
  const ParentIcon = parent.icon
  return (
    <header className="flex h-14 items-center gap-2 border-b px-4 text-[15px] sm:px-6">
      <nav aria-label="Breadcrumb">
        <ol className="flex items-center gap-2">
          <li>
            <Link
              to={parent.to}
              className="inline-flex items-center gap-2 text-muted-foreground transition-colors hover:text-foreground"
            >
              <ParentIcon className="size-4" aria-hidden="true" />
              {parent.label}
            </Link>
          </li>
          <li aria-hidden="true" className="text-muted-foreground/60">
            /
          </li>
          <li>
            <h1 aria-current="page" className="inline-flex items-center gap-2 font-medium">
              <Icon className="size-4" aria-hidden="true" />
              {title}
            </h1>
          </li>
        </ol>
      </nav>
      <ActionMenu label="Page actions" actions={actions} className="size-7" />
    </header>
  )
}

/** Toolbar "Sort" button with a radio list of keys and a direction toggle. */
export function SortMenu<K extends string>({
  labels,
  sort,
  onSort,
}: {
  labels: Record<K, string>
  sort: { key: K; dir: 'asc' | 'desc' }
  onSort: (sort: { key: K; dir: 'asc' | 'desc' }) => void
}) {
  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger asChild>
        <Button variant="outline" className={TOOLBAR_BUTTON}>
          <ArrowUpDown aria-hidden="true" />
          Sort
        </Button>
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content align="end" sideOffset={6} className={MENU_CONTENT}>
          <DropdownMenu.RadioGroup value={sort.key} onValueChange={(key) => onSort({ key: key as K, dir: sort.dir })}>
            {(Object.keys(labels) as K[]).map((key) => (
              <DropdownMenu.RadioItem key={key} value={key} className={MENU_ITEM}>
                <span className="size-4">
                  <DropdownMenu.ItemIndicator>
                    <CircleCheck className="size-4 text-brand" aria-hidden="true" />
                  </DropdownMenu.ItemIndicator>
                </span>
                {labels[key]}
              </DropdownMenu.RadioItem>
            ))}
          </DropdownMenu.RadioGroup>
          <DropdownMenu.Separator className="my-1 h-px bg-border" />
          <DropdownMenu.Item
            className={MENU_ITEM}
            onSelect={() => onSort({ key: sort.key, dir: sort.dir === 'asc' ? 'desc' : 'asc' })}
          >
            {sort.dir === 'asc' ? <ArrowDown aria-hidden="true" /> : <ArrowUp aria-hidden="true" />}
            {sort.dir === 'asc' ? 'Descending' : 'Ascending'}
          </DropdownMenu.Item>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}

/** Column header that sorts by its column; clicking again flips the direction. */
export function SortHeader({
  label,
  count,
  active,
  dir,
  onSort,
}: {
  label: string
  count?: number
  active: boolean
  dir: 'asc' | 'desc'
  onSort: () => void
}) {
  const Icon = !active ? ArrowUpDown : dir === 'asc' ? ArrowUp : ArrowDown
  return (
    <th
      scope="col"
      aria-sort={active ? (dir === 'asc' ? 'ascending' : 'descending') : undefined}
      className="px-3 py-3 font-medium"
    >
      <button
        type="button"
        onClick={onSort}
        className="group inline-flex items-center gap-2 rounded-md outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
      >
        {label}
        {count !== undefined && <span className="font-normal text-muted-foreground tabular-nums">({count})</span>}
        <span
          className={cn(
            'inline-flex size-4 items-center justify-center rounded-full border transition-colors',
            active ? 'border-brand/40 text-brand' : 'text-muted-foreground/70 group-hover:text-foreground',
          )}
        >
          <Icon className="size-2.5" strokeWidth={2.5} aria-hidden="true" />
        </span>
      </button>
    </th>
  )
}

export function TableCheckbox({
  label,
  checked,
  indeterminate = false,
  disabled,
  onChange,
}: {
  label: string
  checked: boolean
  indeterminate?: boolean
  disabled?: boolean
  onChange: () => void
}) {
  const ref = useRef<HTMLInputElement>(null)
  useEffect(() => {
    if (ref.current) ref.current.indeterminate = indeterminate
  }, [indeterminate])
  return (
    <input
      ref={ref}
      type="checkbox"
      aria-label={label}
      checked={checked}
      disabled={disabled}
      onChange={onChange}
      className="block size-[18px] cursor-pointer rounded-[5px] accent-brand disabled:cursor-not-allowed disabled:opacity-40"
    />
  )
}

/** Toolbar search: a square icon button that opens into a field, and folds back when emptied. */
export function ToolbarSearch({
  open,
  onOpenChange,
  value,
  onChange,
  label,
  placeholder,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  value: string
  onChange: (value: string) => void
  label: string
  placeholder: string
}) {
  if (!open && !value) {
    return (
      <Button variant="outline" className="size-9 rounded-lg p-0" aria-label={label} title="Search" onClick={() => onOpenChange(true)}>
        <Search aria-hidden="true" />
      </Button>
    )
  }
  return (
    <label className="relative block w-64 min-w-0">
      <span className="sr-only">{label}</span>
      <Search
        className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
        aria-hidden="true"
      />
      <input
        type="search"
        autoFocus
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onBlur={() => onOpenChange(false)}
        onKeyDown={(e) => {
          if (e.key === 'Escape') {
            onChange('')
            onOpenChange(false)
          }
        }}
        placeholder={placeholder}
        className="h-9 w-full rounded-lg border border-input bg-background pr-3 pl-9 text-sm outline-none focus-visible:border-brand focus-visible:ring-4 focus-visible:ring-brand/15"
      />
    </label>
  )
}

/** Filter tabs on the left of the toolbar, e.g. "All users 124". */
export function FilterTabs<V extends string>({
  label,
  tabs,
  value,
  onChange,
}: {
  label: string
  tabs: { value: V; label: string; icon: LucideIcon; count: number }[]
  value: V
  onChange: (value: V) => void
}) {
  return (
    <div role="group" aria-label={label} className="flex max-w-full gap-1 overflow-x-auto [scrollbar-width:none]">
      {tabs.map(({ value: tab, label, icon: Icon, count }) => (
        <button
          key={tab}
          type="button"
          onClick={() => onChange(tab)}
          aria-pressed={value === tab}
          className={cn(
            'inline-flex h-9 shrink-0 items-center gap-2 rounded-lg border px-2.5 text-sm transition-colors',
            value === tab
              ? 'border-border bg-background text-foreground shadow-xs'
              : 'border-transparent text-muted-foreground hover:bg-muted hover:text-foreground',
          )}
        >
          <Icon className="size-4" aria-hidden="true" />
          {label}
          <span className="text-[13px] text-muted-foreground tabular-nums">{count}</span>
        </button>
      ))}
    </div>
  )
}

/** Toolbar row under the page bar: filter tabs on the left, controls on the right. */
export function Toolbar({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-3 border-b px-4 py-2.5 sm:px-6">
      {children}
    </div>
  )
}

/** Thin success strip under the page bar. */
export function FlashBar({ message }: { message: string | null }) {
  return (
    <div role="status" aria-live="polite">
      {message && (
        <p className="flex items-center gap-2 border-b bg-success-soft px-4 py-2.5 text-sm text-success sm:px-6">
          <CircleCheck className="size-4 shrink-0" aria-hidden="true" />
          {message}
        </p>
      )}
    </div>
  )
}

/** Placeholder rows while the list loads. */
export function TableSkeleton({ rows = 6 }: { rows?: number }) {
  return (
    <ul aria-busy="true" aria-label="Loading" className="divide-y">
      {Array.from({ length: rows }, (_, i) => (
        <li key={i} className="flex items-center gap-4 px-4 py-4 sm:px-6">
          <span className="size-[18px] animate-pulse rounded-[5px] bg-muted motion-reduce:animate-none" />
          <span className="size-5 animate-pulse rounded-md bg-muted motion-reduce:animate-none" />
          <span className="h-4 w-40 animate-pulse rounded bg-muted motion-reduce:animate-none" />
          <span className="ml-auto h-4 w-20 animate-pulse rounded bg-muted motion-reduce:animate-none" />
        </li>
      ))}
    </ul>
  )
}
