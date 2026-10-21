import { useEffect, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { teacherCoursesApi } from '@/features/teacher-courses/api/teacherCoursesApi'
import type { TeacherCourse } from '@/features/teacher-courses/types'
import { toApiError } from '@/lib/http/apiError'
import { teacherQuestionsApi } from '../api/teacherQuestionsApi'
import { QuestionFormDialog } from '../components/QuestionFormDialog'
import type { QuestionItemDto, QuestionPage, QuestionQuery } from '../types'

const control = 'rounded-xl border bg-background px-3 py-2 text-sm'
const emptyPage: QuestionPage = { items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0 }
const levels = ['Basic', 'Intermediate', 'Advanced']

export default function TeacherQuestionsPage() {
  const [params] = useSearchParams()
  const requestedCourse = Number(params.get('courseId'))
  const [courses, setCourses] = useState<TeacherCourse[]>([])
  const [courseId, setCourseId] = useState(0)
  const [query, setQuery] = useState<QuestionQuery>({ page: 1, pageSize: 10, sortBy: 'createdAt', sortDirection: 'desc' })
  const [search, setSearch] = useState('')
  const [data, setData] = useState<QuestionPage>(emptyPage)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [flash, setFlash] = useState<string | null>(null)
  const [revision, setRevision] = useState(0)
  const [dialog, setDialog] = useState<{ questionId?: number; readOnly: boolean } | null>(null)
  const [deleting, setDeleting] = useState<QuestionItemDto | null>(null)
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const [deletePending, setDeletePending] = useState(false)
  const [generateCount, setGenerateCount] = useState(10)
  const [focusTopics, setFocusTopics] = useState('')
  const [generating, setGenerating] = useState(false)

  useEffect(() => {
    let active = true
    teacherCoursesApi.listMine().then((list) => {
      if (!active) return
      setCourses(list)
      setCourseId(list.find((course) => course.courseId === requestedCourse)?.courseId ?? list[0]?.courseId ?? 0)
      if (!list.length) setLoading(false)
    }).catch((err) => { if (active) { setError(toApiError(err).message); setLoading(false) } })
    return () => { active = false }
  }, [requestedCourse])

  useEffect(() => {
    if (!courseId) return
    let active = true
    teacherQuestionsApi.getQuestions(courseId, query).then((page) => {
      if (!active) return
      if ((query.page ?? 1) > Math.max(1, page.totalPages)) {
        setQuery((previous) => ({ ...previous, page: Math.max(1, page.totalPages) }))
        return
      }
      setData(page)
      setLoading(false)
    }).catch((err) => { if (active) { setError(toApiError(err).message); setLoading(false) } })
    return () => { active = false }
  }, [courseId, query, revision])

  function refresh() { setError(null); setLoading(true); setRevision((value) => value + 1) }
  function changeQuery(changes: QuestionQuery) {
    setError(null); setLoading(true); setQuery((previous) => ({ ...previous, page: 1, ...changes }))
  }

  async function remove() {
    if (!deleting || deletePending) return
    setDeletePending(true); setDeleteError(null)
    try {
      await teacherQuestionsApi.delete(courseId, deleting.questionId)
      setDeleting(null); setFlash('Question deleted.'); refresh()
    } catch (err) { setDeleteError(toApiError(err).message) }
    finally { setDeletePending(false) }
  }

  async function generate(event: FormEvent) {
    event.preventDefault()
    if (!courseId || generating) return
    setGenerating(true); setError(null); setFlash(null)
    try {
      const result = await teacherQuestionsApi.generateQuestions(courseId, { questionCount: generateCount, focusTopics: focusTopics.trim() })
      setFlash(`Generated ${result.generatedCount ?? result.questions.length} questions.`)
      changeQuery({ page: 1 }); refresh()
    } catch (err) { setError(toApiError(err).message) }
    finally { setGenerating(false) }
  }

  return (
    <div className="mx-auto max-w-5xl space-y-6 py-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div><h1 className="font-heading text-3xl font-medium">Question bank</h1><p className="mt-2 text-muted-foreground">Manage questions, model answers, learning outcomes and rubrics.</p></div>
        <Button disabled={!courseId || loading || generating} onClick={() => setDialog({ readOnly: false })}>Add question</Button>
      </header>
      <label className="block text-sm">Course
        <select className={`${control} ml-3 max-w-full`} value={courseId} disabled={generating} onChange={(event) => { setCourseId(Number(event.target.value)); setData(emptyPage); changeQuery({ page: 1 }); setFlash(null) }}>
          {!courses.length && <option value={0}>No assigned courses</option>}
          {courses.map((course) => <option key={course.courseId} value={course.courseId}>{course.courseCode} — {course.courseName}</option>)}
        </select>
      </label>
      {error && <p role="alert" className="rounded-xl bg-destructive/10 p-4 text-sm text-destructive">{error} <Button variant="ghost" onClick={refresh}>Try again</Button></p>}
      {flash && <p role="status" className="rounded-xl bg-success-soft p-4 text-sm text-success">{flash}</p>}
      <section className="rounded-2xl border p-5">
        <h2 className="font-heading font-medium">Generate questions with AI</h2>
        <form onSubmit={generate} className="mt-4 flex flex-wrap items-end gap-3">
          <label className="text-sm">Questions<input aria-label="Number of questions to generate" type="number" required min={5} max={200} className={`${control} mt-1 block w-24`} value={generateCount} onChange={(event) => setGenerateCount(Number(event.target.value))} /></label>
          <label className="flex-1 text-sm">Focus topics<input className={`${control} mt-1 block w-full`} maxLength={1000} value={focusTopics} onChange={(event) => setFocusTopics(event.target.value)} /></label>
          <Button disabled={generating || !courseId}>{generating ? 'Generating…' : 'Generate'}</Button>
        </form>
      </section>
      <section className="space-y-4" aria-label="Question list" aria-busy={loading}>
        <form onSubmit={(event) => { event.preventDefault(); changeQuery({ search: search.trim() }) }} className="flex flex-wrap items-end gap-3">
          <label className="flex-1 text-sm">Search questions<input type="search" maxLength={1000} className={`${control} mt-1 block w-full`} value={search} onChange={(event) => setSearch(event.target.value)} /></label>
          <Button variant="outline" disabled={!courseId}>Search</Button>
          <label className="text-sm">Difficulty<select className={`${control} mt-1 block`} value={query.level ?? ''} onChange={(event) => changeQuery({ level: event.target.value ? Number(event.target.value) : undefined })}><option value="">All</option>{levels.map((name, index) => <option key={name} value={index + 1}>{name}</option>)}</select></label>
          <label className="text-sm">Sort by<select className={`${control} mt-1 block`} value={query.sortBy} onChange={(event) => changeQuery({ sortBy: event.target.value as QuestionQuery['sortBy'] })}><option value="createdAt">Date created</option><option value="questionText">Text</option><option value="level">Difficulty</option><option value="maxScore">Max score</option></select></label>
          <label className="text-sm">Order<select className={`${control} mt-1 block`} value={query.sortDirection} onChange={(event) => changeQuery({ sortDirection: event.target.value as 'asc' | 'desc' })}><option value="desc">Descending</option><option value="asc">Ascending</option></select></label>
        </form>
        <p className="text-sm text-muted-foreground">{data.totalCount} questions</p>
        {loading ? <p role="status">Loading questions…</p> : error ? null : !data.items.length ? <p className="rounded-xl border border-dashed p-8 text-center">No matching questions.</p> : data.items.map((question) => (
          <article key={question.questionId} className="space-y-3 rounded-2xl border p-5">
            <p className="font-medium">{question.questionText}</p>
            <p className="text-sm text-muted-foreground">{question.topic ? `${question.topic} · ` : ''}{levels[question.level - 1]} · {question.maxScore} pts</p>
            {question.isInUse && <p className="text-xs text-muted-foreground">Already used in an exam. Create a new question to change its content.</p>}
            <div className="flex gap-2">
              <Button size="sm" variant="outline" onClick={() => setDialog({ questionId: question.questionId, readOnly: true })}>View</Button>
              <Button size="sm" variant="outline" disabled={question.isInUse} onClick={() => setDialog({ questionId: question.questionId, readOnly: false })}>Edit</Button>
              <Button size="sm" variant="ghost" disabled={question.isInUse} onClick={() => { setDeleting(question); setDeleteError(null) }}>Delete</Button>
            </div>
          </article>
        ))}
        <div className="flex items-center justify-between gap-3">
          <Button variant="outline" disabled={loading || (query.page ?? 1) <= 1} onClick={() => changeQuery({ page: (query.page ?? 1) - 1 })}>Previous</Button>
          <span className="text-sm">Page {query.page ?? 1}/{Math.max(1, data.totalPages)}</span>
          <Button variant="outline" disabled={loading || (query.page ?? 1) >= data.totalPages} onClick={() => changeQuery({ page: (query.page ?? 1) + 1 })}>Next</Button>
        </div>
      </section>
      <Button asChild variant="outline"><Link to="/staff/exams">Manage exams</Link></Button>
      {dialog && <QuestionFormDialog key={`${courseId}/${dialog.questionId ?? 'new'}/${dialog.readOnly}`} courseId={courseId} {...dialog} onClose={() => setDialog(null)} onSaved={() => { setDialog(null); setFlash('Question saved.'); refresh() }} />}
      <ConfirmDialog open={deleting !== null} title="Delete question?" description={deleting?.questionText ?? ''} pending={deletePending} error={deleteError} onClose={() => setDeleting(null)} onConfirm={() => void remove()} />
    </div>
  )
}
