import { CalendarClock, Plus, RotateCw } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { teacherCoursesApi } from '@/features/teacher-courses/api/teacherCoursesApi'
import { useApi } from '@/lib/http/useApi'
import { periodsApi } from '../api/periodsApi'
import { CreatePeriodDialog } from '../components/CreatePeriodDialog'

export default function StaffPeriodsPage() {
  const courses = useApi(() => teacherCoursesApi.listMine(), '', 'Could not load your courses.')
  const periods = useApi(() => periodsApi.list(), '', 'Could not load exam periods.')
  const [courseId, setCourseId] = useState('')
  const [creating, setCreating] = useState(false)
  const course = courses.state.status === 'ready' ? courses.state.data.find(c => c.courseId === Number(courseId)) : undefined
  const visible = periods.state.status === 'ready' ? periods.state.data.filter(p => !courseId || p.courseId === Number(courseId)) : []
  return (
    <div className="mx-auto max-w-5xl py-6">
      <header className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <div><h1 className="font-heading text-3xl font-medium">Exam periods</h1><p className="mt-2 text-muted-foreground">Create a period for your course, then assign exam sessions to it.</p></div>
        <Button variant="outline" onClick={() => { courses.reload(); periods.reload() }}><RotateCw className="size-4" /> Refresh</Button>
      </header>
      <div className="mb-5 flex flex-wrap gap-3">
        <select aria-label="Course for exam period" className="h-10 max-w-md flex-1 rounded-xl border bg-background px-3 text-sm" value={courseId} onChange={e => setCourseId(e.target.value)} disabled={courses.state.status !== 'ready'}>
          <option value="">Choose a course to create a period</option>
          {courses.state.status === 'ready' && courses.state.data.map(c => <option key={c.courseId} value={c.courseId}>{c.courseCode} · {c.courseName}</option>)}
        </select>
        <Button disabled={!course} onClick={() => setCreating(true)}><Plus className="size-4" /> New period</Button>
      </div>
      {(courses.state.status === 'error' || periods.state.status === 'error') && <Alert variant="destructive" className="mb-4"><AlertDescription>{courses.state.status === 'error' ? courses.state.message : periods.state.status === 'error' ? periods.state.message : ''}</AlertDescription></Alert>}
      {periods.state.status === 'loading' ? <p role="status">Loading periods…</p> : visible.length === 0 ? <div className="rounded-2xl border border-dashed p-10 text-center"><CalendarClock className="mx-auto mb-2 text-brand" />No periods found. Select a course and create one.</div> : (
        <div className="divide-y rounded-2xl border">{visible.map(p => <article key={p.periodId} className="flex flex-wrap items-center justify-between gap-4 p-5">
          <div><h2 className="font-medium">{p.periodName}</h2><p className="text-sm text-muted-foreground">{p.courseName} · {p.startDate.slice(0, 10)} — {p.endDate.slice(0, 10)} · {p.status}</p>{p.description && <p className="mt-1 text-sm">{p.description}</p>}</div>
          <Link to={`/staff/sessions?periodId=${p.periodId}`} className="text-sm font-medium text-brand underline">View sessions ({p.sessionsCount ?? 0})</Link>
        </article>)}</div>
      )}
      {creating && course && <CreatePeriodDialog key={course.courseId} open courseId={course.courseId} courseName={course.courseName} onClose={() => setCreating(false)} onCreated={() => { setCreating(false); periods.reload() }} />}
    </div>
  )
}
