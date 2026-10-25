import { Search } from 'lucide-react'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { usersApi } from '@/features/admin-users/api/usersApi'
import type { User } from '@/features/admin-users/types'
import { toApiError } from '@/lib/http/apiError'
import { adminCoursesApi } from '../api/adminCoursesApi'
import type { AdminCourse } from '../types'

const inputClass = 'mt-1 block w-full rounded-xl border bg-background px-3 py-2 text-sm'
type Mode = 'create' | 'edit' | 'view' | 'assign'

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
  const [deleting, setDeleting] = useState<AdminCourse | null>(null)
  const [query, setQuery] = useState('')
  const requestId = useRef(0)

  const q = query.trim().toLowerCase()
  const visibleCourses = q
    ? courses.filter((item) => [item.courseCode, item.courseName, item.teacherFullName ?? '', item.teacherName ?? ''].some((field) => field.toLowerCase().includes(q)))
    : courses

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
    try {
      await adminCoursesApi.delete(deleting.courseId)
      setCourses((list) => list.filter((item) => item.courseId !== deleting.courseId))
      setDeleting(null); setFlash('Course deleted.'); setError(null)
    } catch (err) { setFormError(toApiError(err).message) }
    finally { setPending(false) }
  }

  return (
    <div className="mx-auto max-w-5xl space-y-6 py-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div><h1 className="font-heading text-3xl font-medium">Courses</h1><p className="mt-2 text-muted-foreground">Create courses, update their details and assign the staff member in charge.</p></div>
        <Button onClick={() => void open('create')}>Add course</Button>
      </header>
      {error && <p role="alert" className="text-sm text-destructive">{error} <Button variant="outline" onClick={() => { setLoading(true); setError(null); setRevision((value) => value + 1) }}>Try again</Button></p>}
      {flash && <p role="status" className="rounded-xl bg-success-soft p-4 text-success">{flash}</p>}
      {!loading && courses.length > 0 && (
        <label className="relative block sm:max-w-sm">
          <span className="sr-only">Search courses</span>
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
          <input type="search" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Course code, name or staff" className="block w-full rounded-xl border bg-background py-2 pr-3 pl-9 text-sm" />
        </label>
      )}
      {loading ? <p role="status">Loading courses…</p> : !courses.length ? <p className="rounded-2xl border border-dashed p-8 text-center">No courses yet.</p> : !visibleCourses.length ? <p className="rounded-2xl border border-dashed p-8 text-center text-muted-foreground">No courses match “{query.trim()}”.</p> : visibleCourses.map((item) => (
        <article key={item.courseId} className="flex flex-wrap items-center justify-between gap-4 rounded-2xl border p-5">
          <div><p className="font-mono text-sm text-brand">{item.courseCode}</p><h2 className="font-medium">{item.courseName}</h2><p className="mt-1 text-sm text-muted-foreground">{item.teacherFullName ?? item.teacherName ?? 'No staff assigned'}</p></div>
          <div className="flex flex-wrap gap-2">
            <Button size="sm" variant="outline" onClick={() => void open('view', item)}>View</Button>
            <Button size="sm" variant="outline" onClick={() => void open('edit', item)}>Edit</Button>
            <Button size="sm" variant="outline" onClick={() => void open('assign', item)}>Assign staff</Button>
            <Button size="sm" variant="ghost" onClick={() => { setDeleting(item); setFormError(null) }}>Delete</Button>
          </div>
        </article>
      ))}
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
      <ConfirmDialog open={deleting !== null} title="Delete course?" description={`Delete ${deleting?.courseName ?? ''}? Courses with related data cannot be deleted.`} pending={pending} error={formError} onClose={() => setDeleting(null)} onConfirm={() => void remove()} />
    </div>
  )
}
