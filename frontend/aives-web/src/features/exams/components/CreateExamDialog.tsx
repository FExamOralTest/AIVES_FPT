import { useEffect, useState } from 'react'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { teacherCoursesApi } from '@/features/teacher-courses/api/teacherCoursesApi'
import type { TeacherCourse } from '@/features/teacher-courses/types'
import { toApiError } from '@/lib/http/apiError'
import { examsApi } from '../api/examsApi'
import type { Exam } from '../types'
import { ExamForm } from './ExamForm'

interface Props { open: boolean; onClose: () => void; onCreated: (exam: Exam) => void }

export function CreateExamDialog({ open, onClose, onCreated }: Props) {
  const [pending, setPending] = useState(false)
  return <Dialog open={open} onOpenChange={(value) => !value && !pending && onClose()}>
    <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
      <DialogHeader><DialogTitle>Create exam</DialogTitle><DialogDescription>Choose a course and questions. A draft without questions needs some before you can create a session.</DialogDescription></DialogHeader>
      {open && <CreateExam onClose={onClose} onCreated={onCreated} pending={pending} onPending={setPending} />}
    </DialogContent>
  </Dialog>
}

function CreateExam({ onClose, onCreated, pending, onPending }: { onClose: () => void; onCreated: Props['onCreated']; pending: boolean; onPending: (pending: boolean) => void }) {
  const [courses, setCourses] = useState<TeacherCourse[]>([])
  const [courseId, setCourseId] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    let active = true
    teacherCoursesApi.listMine().then((items) => {
      if (active) { setCourses(items); setCourseId(items[0]?.courseId ?? 0); setLoading(false) }
    }).catch((err) => { if (active) { setError(toApiError(err).message); setLoading(false) } })
    return () => { active = false }
  }, [])
  const course = courses.find((item) => item.courseId === courseId)
  const draft: Exam = { examId: 0, canEdit: true, title: '', examCode: '', courseId, courseName: course?.courseName ?? null, createdBy: 0, createdByName: null, durationMinutes: 30, maxQuestions: 10, examStatus: 'DRAFT', createdAt: null, questions: [] }
  return <>
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    {loading ? <p role="status">Loading courses…</p> : !courses.length ? <p>You are not assigned to any course.</p> : <>
      <label className="text-sm">Course<select className="mt-1 block w-full rounded-xl border bg-background px-3 py-2" value={courseId} disabled={pending} onChange={(event) => setCourseId(Number(event.target.value))}>{courses.map((item) => <option key={item.courseId} value={item.courseId}>{item.courseCode} — {item.courseName}</option>)}</select></label>
      <ExamForm key={courseId} exam={draft} onSubmit={(input) => examsApi.create({ ...input, courseId })} onSaved={onCreated} onCancel={onClose} onPending={onPending} submitLabel="Create exam" />
    </>}
  </>
}
