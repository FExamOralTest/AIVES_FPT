import {
  BookOpen,
  CircleAlert,
  Eye,
  LayoutDashboard,
  LayoutGrid,
  Pencil,
  PlusCircle,
  RotateCw,
  Trash2,
  UserCheck,
  UserRoundPlus,
  UserX,
  type LucideIcon,
} from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import {
  ActionMenu,
  FilterTabs,
  FlashBar,
  PageBar,
  SortHeader,
  SortMenu,
  TableCheckbox,
  TableSkeleton,
  Toolbar,
  ToolbarSearch,
  TOOLBAR_BUTTON,
} from '@/components/common/FlatPage'
import { usersApi } from '@/features/admin-users/api/usersApi'
import type { User } from '@/features/admin-users/types'
import { ROLE_HOME } from '@/features/auth'
import { formatLongDate, parseApiDate } from '@/lib/format/date'
import { toApiError } from '@/lib/http/apiError'
import { cn } from '@/lib/utils'
import { adminCoursesApi } from '../api/adminCoursesApi'
import type { AdminCourse } from '../types'

const inputClass = 'mt-1 block w-full rounded-xl border bg-background px-3 py-2 text-sm'
type Mode = 'create' | 'edit' | 'view' | 'assign'

type Filter = 'ALL' | 'ASSIGNED' | 'UNASSIGNED'

const FILTERS: { value: Filter; label: string; icon: LucideIcon }[] = [
  { value: 'ALL', label: 'All courses', icon: LayoutGrid },
  { value: 'ASSIGNED', label: 'Staff assigned', icon: UserCheck },
  { value: 'UNASSIGNED', label: 'No staff yet', icon: UserX },
]

type SortKey = 'courseName' | 'courseCode' | 'staff' | 'createdAt'
type Sort = { key: SortKey; dir: 'asc' | 'desc' }

const SORT_LABEL: Record<SortKey, string> = {
  courseName: 'Name',
  courseCode: 'Code',
  staff: 'Staff in charge',
  createdAt: 'Created on',
}

const staffName = (course: AdminCourse) => course.teacherFullName ?? course.teacherName ?? null

function compareCourses(a: AdminCourse, b: AdminCourse, key: SortKey) {
  if (key === 'createdAt') return (a.createdAt ?? '').localeCompare(b.createdAt ?? '')
  if (key === 'staff') return (staffName(a) ?? '').localeCompare(staffName(b) ?? '', 'vi')
  return a[key].localeCompare(b[key], 'vi')
}

export default function AdminCoursesPage() {
  const [courses, setCourses] = useState<AdminCourse[]>([])
  const [teachers, setTeachers] = useState<User[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [flash, setFlash] = useState<string | null>(null)
  const [revision, setRevision] = useState(0)
  const [mode, setMode] = useState<Mode | null>(null)
  const [course, setCourse] = useState<AdminCourse | null>(null)
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [teacherId, setTeacherId] = useState('')
  const [detailFailed, setDetailFailed] = useState(false)
  const [detailLoading, setDetailLoading] = useState(false)
  const [pending, setPending] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  // Either one row from its menu, or every selected row.
  const [deleting, setDeleting] = useState<AdminCourse[] | null>(null)
  const [query, setQuery] = useState('')
  const [searchOpen, setSearchOpen] = useState(false)
  const [filter, setFilter] = useState<Filter>('ALL')
  const [sort, setSort] = useState<Sort>({ key: 'courseCode', dir: 'asc' })
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const requestId = useRef(0)

  const counts: Record<Filter, number> = {
    ALL: courses.length,
    ASSIGNED: courses.filter((item) => item.teacherId).length,
    UNASSIGNED: courses.filter((item) => !item.teacherId).length,
  }

  const visibleCourses = useMemo(() => {
    const q = query.trim().toLowerCase()
    const sign = sort.dir === 'asc' ? 1 : -1
    return courses
      .filter((item) => filter === 'ALL' || (filter === 'ASSIGNED') === Boolean(item.teacherId))
      .filter((item) => !q || [item.courseCode, item.courseName, item.teacherFullName ?? '', item.teacherName ?? ''].some((field) => field.toLowerCase().includes(q)))
      .sort((a, b) => sign * compareCourses(a, b, sort.key))
  }, [courses, query, filter, sort])

  const selectedVisible = visibleCourses.filter((item) => selected.has(item.courseId))

  function toggleAll() {
    setSelected((previous) => {
      const next = new Set(previous)
      const allOn = selectedVisible.length === visibleCourses.length
      for (const item of visibleCourses) {
        if (allOn) next.delete(item.courseId)
        else next.add(item.courseId)
      }
      return next
    })
  }

  function toggleOne(courseId: number) {
    setSelected((previous) => {
      const next = new Set(previous)
      if (!next.delete(courseId)) next.add(courseId)
      return next
    })
  }

  function sortBy(key: SortKey) {
    setSort((previous) => ({ key, dir: previous.key === key && previous.dir === 'asc' ? 'desc' : 'asc' }))
  }

  function reload() {
    setLoading(true); setError(null); setRevision((value) => value + 1)
  }

  useEffect(() => {
    let active = true
    Promise.all([adminCoursesApi.list(), usersApi.list()]).then(([list, users]) => {
      if (!active) return
      setCourses(list); setTeachers(users.filter((user) => user.role === 'STAFF')); setLoading(false)
    }).catch((err) => { if (active) { setError(toApiError(err).message); setLoading(false) } })
    return () => { active = false }
  }, [revision])

  function close() { if (!pending) { requestId.current++; setMode(null) } }

  async function open(nextMode: Mode, summary: AdminCourse | null = null) {
    const currentRequest = ++requestId.current
    setMode(nextMode); setCourse(summary); setCode(summary?.courseCode ?? ''); setName(summary?.courseName ?? '')
    setTeacherId(summary?.teacherId?.toString() ?? ''); setDetailFailed(false); setFormError(null); setDetailLoading(summary !== null)
    if (!summary) return
    try {
      const detail = await adminCoursesApi.get(summary.courseId)
      if (currentRequest !== requestId.current) return
      setCourse(detail); setCode(detail.courseCode); setName(detail.courseName); setTeacherId(detail.teacherId?.toString() ?? '')
    } catch (err) { if (currentRequest === requestId.current) { setFormError(toApiError(err).message); setDetailFailed(true) } }
    finally { if (currentRequest === requestId.current) setDetailLoading(false) }
  }

  async function save(event: FormEvent) {
    event.preventDefault()
    if (pending || detailLoading || detailFailed) return
    if (mode !== 'assign' && (!code.trim() || !name.trim())) { setFormError('Enter the course code and name.'); return }
    setPending(true); setFormError(null)
    try {
      let saved: AdminCourse
      if (mode === 'create') saved = await adminCoursesApi.create({ courseCode: code.trim().toUpperCase(), courseName: name.trim(), teacherId: teacherId ? Number(teacherId) : null })
      else if (mode === 'assign' && course) saved = await adminCoursesApi.assignTeacher(course.courseId, Number(teacherId))
      else if (course) saved = await adminCoursesApi.update(course.courseId, { courseCode: code.trim().toUpperCase(), courseName: name.trim() })
      else return
      setCourses((list) => mode === 'create' ? [saved, ...list] : list.map((item) => item.courseId === saved.courseId ? saved : item))
      setMode(null); setFlash('Course saved.'); setError(null)
    } catch (err) { setFormError(toApiError(err).message) }
    finally { setPending(false) }
  }

  async function remove() {
    if (!deleting || pending) return
    setPending(true); setFormError(null)
    const deleted: number[] = []
    const failed: string[] = []
    for (const item of deleting) {
      try {
        await adminCoursesApi.delete(item.courseId)
        deleted.push(item.courseId)
      } catch (err) { failed.push(deleting.length === 1 ? toApiError(err).message : item.courseCode) }
    }
    setCourses((list) => list.filter((item) => !deleted.includes(item.courseId)))
    setSelected((previous) => new Set([...previous].filter((id) => !deleted.includes(id))))
    setPending(false)
    if (failed.length === 0) {
      setDeleting(null); setFlash(deleted.length === 1 ? 'Course deleted.' : `Deleted ${deleted.length} courses.`); setError(null)
    } else if (deleting.length === 1) {
      setFormError(failed[0])
    } else {
      setDeleting((previous) => previous?.filter((item) => !deleted.includes(item.courseId)) ?? null)
      setFormError(`Deleted ${deleted.length}. These courses have related data and were kept: ${failed.join(', ')}.`)
    }
  }

  const header = (key: SortKey, label: string, count?: number) => (
    <SortHeader label={label} count={count} active={sort.key === key} dir={sort.dir} onSort={() => sortBy(key)} />
  )

  return (
    <>
      <PageBar
        parent={{ label: 'Administration', to: ROLE_HOME.ADMIN, icon: LayoutDashboard }}
        title="Courses"
        icon={BookOpen}
        actions={[
          { label: 'New course', icon: PlusCircle, onSelect: () => void open('create') },
          { label: 'Reload list', icon: RotateCw, onSelect: reload },
        ]}
      />
      <FlashBar message={flash} />

      <section aria-label="Course list">
        <Toolbar>
          <FilterTabs
            label="Filter by staff"
            tabs={FILTERS.map((f) => ({ ...f, count: counts[f.value] }))}
            value={filter}
            onChange={setFilter}
          />
          <div className="flex items-center gap-2">
            {selectedVisible.length > 0 && (
              <Button variant="destructive" className={TOOLBAR_BUTTON} onClick={() => { setDeleting(selectedVisible); setFormError(null) }}>
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
              label="Search courses"
              placeholder="Course code, name or staff"
            />
            <Button variant="brand" className={TOOLBAR_BUTTON} onClick={() => void open('create')}>
              <PlusCircle aria-hidden="true" />
              New course
            </Button>
          </div>
        </Toolbar>

        {loading ? <TableSkeleton /> : error ? (
          <div className="flex flex-col items-center gap-3 px-6 py-14 text-center">
            <CircleAlert className="size-7 text-destructive" aria-hidden="true" />
            <p role="alert" className="max-w-sm text-muted-foreground">{error}</p>
            <Button variant="secondary" size="sm" onClick={reload}>
              <RotateCw aria-hidden="true" />
              Try again
            </Button>
          </div>
        ) : !visibleCourses.length ? (
          <div className="flex flex-col items-center gap-2 px-6 py-14 text-center">
            <BookOpen className="size-7 text-muted-foreground" aria-hidden="true" />
            <p className="text-muted-foreground">{courses.length ? 'No courses match these filters.' : 'No courses yet.'}</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[880px] table-fixed text-left text-[15px]">
              <colgroup>
                <col className="w-12 sm:w-14" />
                <col className="w-[28%]" />
                <col className="w-[14%]" />
                <col />
                <col className="w-[19%]" />
                <col className="w-40" />
              </colgroup>
              <thead>
                <tr className="border-b">
                  <th scope="col" className="py-3 pr-1 pl-4 sm:pl-6">
                    <TableCheckbox
                      label="Select all courses"
                      checked={selectedVisible.length === visibleCourses.length}
                      indeterminate={selectedVisible.length > 0 && selectedVisible.length < visibleCourses.length}
                      onChange={toggleAll}
                    />
                  </th>
                  {header('courseName', 'Course', visibleCourses.length)}
                  {header('courseCode', 'Code')}
                  {header('staff', 'Staff in charge')}
                  {header('createdAt', 'Created on')}
                  <th scope="col" className="px-3 py-3 font-medium">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {visibleCourses.map((item) => {
                  const isSelected = selected.has(item.courseId)
                  const staff = staffName(item)
                  return (
                    <tr key={item.courseId} className={cn('transition-colors hover:bg-muted/50', isSelected && 'bg-brand-soft/50 hover:bg-brand-soft/70')}>
                      <td className="py-3.5 pr-1 pl-4 sm:pl-6">
                        <TableCheckbox label={`Select ${item.courseName}`} checked={isSelected} onChange={() => toggleOne(item.courseId)} />
                      </td>
                      <td className="max-w-0 px-3 py-3.5">
                        <div className="flex min-w-0 items-center gap-3">
                          <span className="truncate">{item.courseName}</span>
                          <ActionMenu
                            label={`Actions for ${item.courseName}`}
                            actions={[
                              { label: 'View details', icon: Eye, onSelect: () => void open('view', item) },
                              { label: 'Edit', icon: Pencil, onSelect: () => void open('edit', item) },
                              { label: 'Assign staff', icon: UserRoundPlus, onSelect: () => void open('assign', item) },
                              { label: 'Delete course', icon: Trash2, onSelect: () => { setDeleting([item]); setFormError(null) }, destructive: true, separated: true },
                            ]}
                          />
                        </div>
                      </td>
                      <td className="max-w-0 truncate px-3 py-3.5 font-mono text-sm text-brand">{item.courseCode}</td>
                      <td className={cn('max-w-0 truncate px-3 py-3.5', !staff && 'text-muted-foreground')}>{staff ?? 'Not assigned'}</td>
                      <td className="max-w-0 truncate px-3 py-3.5">{item.createdAt ? formatLongDate(parseApiDate(item.createdAt)) : '—'}</td>
                      <td className="px-3 py-3.5">
                        <span
                          className={cn(
                            'inline-flex rounded-md px-2 py-0.5 text-[13px] font-medium',
                            staff
                              ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300'
                              : 'bg-orange-50 text-orange-600 dark:bg-orange-950/50 dark:text-orange-300',
                          )}
                        >
                          {staff ? 'Staff assigned' : 'Needs staff'}
                        </span>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
      <Dialog open={mode !== null} onOpenChange={(value) => !value && close()}>
        <DialogContent>
          <DialogHeader><DialogTitle>{mode === 'create' ? 'Add course' : mode === 'edit' ? 'Edit course' : mode === 'assign' ? 'Assign staff' : 'Course details'}</DialogTitle><DialogDescription>{course?.courseName ?? 'Enter the new course details.'}</DialogDescription></DialogHeader>
          {detailLoading ? <p>Loading details…</p> : (
            <form onSubmit={save} className="space-y-4">
              {formError && <p role="alert" className="text-sm text-destructive">{formError}</p>}
              {mode === 'view' ? <dl className="space-y-3 text-sm">{[['Course code', code], ['Course name', name], ['Staff in charge', course?.teacherFullName ?? course?.teacherName ?? 'Not assigned'], ['ID', String(course?.courseId ?? '')]].map(([label, value]) => <div key={label}><dt className="text-muted-foreground">{label}</dt><dd className="break-words font-medium">{value}</dd></div>)}</dl> : <fieldset disabled={pending || detailFailed} className="space-y-4">
                {mode !== 'assign' && <>
                  <label className="block text-sm">Course code<input required maxLength={20} className={inputClass} value={code} onChange={(event) => setCode(event.target.value)} /></label>
                  <label className="block text-sm">Course name<input required maxLength={150} className={inputClass} value={name} onChange={(event) => setName(event.target.value)} /></label>
                </>}
                {(mode === 'create' || mode === 'assign') && <label className="block text-sm">Staff in charge<select required={mode === 'assign'} className={inputClass} value={teacherId} onChange={(event) => setTeacherId(event.target.value)}><option value="">{mode === 'create' ? 'Assign later' : 'Choose a staff member'}</option>{teachers.map((teacher) => <option key={teacher.userId} value={teacher.userId}>{teacher.fullName}</option>)}</select></label>}
              </fieldset>}
              <DialogFooter><Button type="button" variant="outline" disabled={pending} onClick={close}>Close</Button>{mode !== 'view' && <Button disabled={pending || detailFailed}>{pending ? 'Saving…' : 'Save'}</Button>}</DialogFooter>
            </form>
          )}
        </DialogContent>
      </Dialog>
      <ConfirmDialog
        open={deleting !== null}
        title={deleting && deleting.length > 1 ? `Delete ${deleting.length} courses?` : 'Delete course?'}
        description={
          deleting && deleting.length > 1
            ? `Delete ${deleting.length} courses? Courses with related data cannot be deleted.`
            : `Delete ${deleting?.[0]?.courseName ?? ''}? Courses with related data cannot be deleted.`
        }
        pending={pending}
        error={formError}
        onClose={() => setDeleting(null)}
        onConfirm={() => void remove()}
      />
    </>
  )
}
