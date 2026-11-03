import {
  AlertCircle,
  AlertTriangle,
  ArrowRight,
  BookOpen,
  CheckCircle2,
  Download,
  Edit3,
  Eye,
  FileText,
  Trash2,
  Upload,
  X,
} from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { formatDateTime, parseApiDate } from '@/lib/format/date'
import { toApiError } from '@/lib/http/apiError'
import { teacherCoursesApi } from '../api/teacherCoursesApi'
import type { CourseDocument, DocType, TeacherCourse } from '../types'

export default function TeacherCoursesPage() {
  const [courses, setCourses] = useState<TeacherCourse[]>([])
  const [selectedCourse, setSelectedCourse] = useState<TeacherCourse | null>(null)
  const [documents, setDocuments] = useState<CourseDocument[]>([])
  const [isLoadingCourses, setIsLoadingCourses] = useState(true)
  const [isLoadingDocs, setIsLoadingDocs] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isUploadOpen, setIsUploadOpen] = useState(false)
  const [uploadType, setUploadType] = useState<DocType>('CONTENT')
  const [uploadFile, setUploadFile] = useState<File | null>(null)
  const [isUploading, setIsUploading] = useState(false)
  const [uploadError, setUploadError] = useState<string | null>(null)
  const [editingDoc, setEditingDoc] = useState<CourseDocument | null>(null)
  const [editType, setEditType] = useState<DocType>('CONTENT')
  const [editFileName, setEditFileName] = useState('')
  const [editFile, setEditFile] = useState<File | null>(null)
  const [isEditing, setIsEditing] = useState(false)
  const [editError, setEditError] = useState<string | null>(null)
  const [deletingDoc, setDeletingDoc] = useState<CourseDocument | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const [previewDoc, setPreviewDoc] = useState<CourseDocument | null>(null)
  const [previewContent, setPreviewContent] = useState<string>('')
  const [isLoadingPreview, setIsLoadingPreview] = useState(false)
  const [previewError, setPreviewError] = useState<string | null>(null)
  const previewRequest = useRef(0)
  const documentsRequest = useRef(0)

  useEffect(() => {
    let active = true
    teacherCoursesApi.listMine().then(async (list) => {
      if (!active) return
      setCourses(list)
      setIsLoadingCourses(false)
      if (!list.length) return
      setSelectedCourse(list[0])
      const requestId = ++documentsRequest.current
      setIsLoadingDocs(true)
      try {
        const docs = await teacherCoursesApi.getDocuments(list[0].courseId)
        if (active && requestId === documentsRequest.current) setDocuments(docs)
      } catch (err) {
        if (active && requestId === documentsRequest.current) setErrorMessage(toApiError(err).message)
      } finally {
        if (active && requestId === documentsRequest.current) setIsLoadingDocs(false)
      }
    }).catch((err) => {
      if (active) { setErrorMessage(toApiError(err).message); setIsLoadingCourses(false) }
    })
    return () => { active = false }
  }, [])

  async function handleSelectCourse(course: TeacherCourse) {
    const requestId = ++documentsRequest.current
    setDocuments([])
    setSelectedCourse(course)
    setIsLoadingDocs(true)
    setErrorMessage(null)
    try {
      const docs = await teacherCoursesApi.getDocuments(course.courseId)
      if (requestId === documentsRequest.current) setDocuments(docs)
    } catch (err) {
      if (requestId !== documentsRequest.current) return
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
      setDocuments([])
    } finally {
      if (requestId === documentsRequest.current) setIsLoadingDocs(false)
    }
  }

  async function refreshDocuments(courseId: number) {
    const requestId = ++documentsRequest.current
    setIsLoadingDocs(true)
    try {
      const docs = await teacherCoursesApi.getDocuments(courseId)
      if (requestId === documentsRequest.current) setDocuments(docs)
    } catch (err) {
      if (requestId !== documentsRequest.current) return
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
    } finally {
      if (requestId === documentsRequest.current) setIsLoadingDocs(false)
    }
  }

  async function handleUploadSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedCourse || !uploadFile) {
      setUploadError('Please choose a .md file')
      return
    }

    setIsUploading(true)
    setUploadError(null)

    try {
      const formData = new FormData()
      formData.append('file', uploadFile)
      formData.append('docType', uploadType)

      await teacherCoursesApi.uploadDocument(selectedCourse.courseId, formData)
      setIsUploadOpen(false)
      setUploadFile(null)
      await refreshDocuments(selectedCourse.courseId)
    } catch (err) {
      const apiErr = toApiError(err)
      setUploadError(apiErr.message)
    } finally {
      setIsUploading(false)
    }
  }

  function openEditModal(doc: CourseDocument) {
    setEditingDoc(doc)
    setEditType(doc.docType)
    setEditFileName(doc.fileName)
    setEditFile(null)
    setEditError(null)
  }

  async function handleEditSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedCourse || !editingDoc) return

    setIsEditing(true)
    setEditError(null)

    try {
      const formData = new FormData()
      formData.append('docType', editType)
      if (editFileName.trim()) {
        formData.append('fileName', editFileName.trim())
      }
      if (editFile) {
        formData.append('file', editFile)
      }

      await teacherCoursesApi.updateDocument(selectedCourse.courseId, editingDoc.documentId, formData)
      setEditingDoc(null)
      await refreshDocuments(selectedCourse.courseId)
    } catch (err) {
      const apiErr = toApiError(err)
      setEditError(apiErr.message)
    } finally {
      setIsEditing(false)
    }
  }

  function openDeleteModal(doc: CourseDocument) {
    setDeletingDoc(doc)
    setDeleteError(null)
  }

  async function handleDeleteConfirm() {
    if (!selectedCourse || !deletingDoc) return

    setIsDeleting(true)
    setDeleteError(null)

    try {
      await teacherCoursesApi.deleteDocument(selectedCourse.courseId, deletingDoc.documentId)
      setDeletingDoc(null)
      await refreshDocuments(selectedCourse.courseId)
    } catch (err) {
      const apiErr = toApiError(err)
      setDeleteError(apiErr.message)
    } finally {
      setIsDeleting(false)
    }
  }

  async function openPreviewModal(doc: CourseDocument) {
    const requestId = ++previewRequest.current
    setPreviewDoc(doc)
    setPreviewContent('')
    setPreviewError(null)
    setIsLoadingPreview(true)
    try {
      const detail = await teacherCoursesApi.getDocument(doc.courseId, doc.documentId)
      if (requestId === previewRequest.current) setPreviewContent(detail.content ?? '')
    } catch (err) {
      if (requestId === previewRequest.current) setPreviewError(toApiError(err).message)
    } finally {
      if (requestId === previewRequest.current) setIsLoadingPreview(false)
    }
  }

  async function handleDownload(doc: CourseDocument) {
    try {
      const detail = await teacherCoursesApi.getDocument(doc.courseId, doc.documentId)
      const blob = new Blob([detail.content ?? ''], { type: 'text/markdown;charset=utf-8' })
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = detail.fileName
      document.body.appendChild(link)
      link.click()
      link.remove()
      setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch (err) { setErrorMessage(toApiError(err).message) }
  }

  const contentCount = documents.filter((d) => d.docType === 'CONTENT').length
  const outcomesCount = documents.filter((d) => d.docType === 'LEARNING_OUTCOMES').length
  const hasContent = contentCount > 0
  const hasLearningOutcomes = outcomesCount > 0
  const isReadyForAi = contentCount === 1 && outcomesCount === 1

  return (
    <div className="mx-auto max-w-5xl py-6">
      <header className="mb-8">
        <h1 className="font-heading text-3xl font-medium tracking-tight sm:text-4xl">
          Courses & documents
        </h1>
        <p className="mt-2 text-muted-foreground">
          Manage your assigned courses and the Markdown (.md) documents the AI uses to generate exams and viva questions.
        </p>
      </header>

      {errorMessage && (
        <Alert variant="destructive" className="mb-6 rounded-2xl">
          <AlertCircle className="size-4" />
          <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
      )}

      {isLoadingCourses ? (
        <div className="py-12 text-center text-muted-foreground">Loading courses…</div>
      ) : courses.length === 0 ? (
        <div className="rounded-3xl border border-dashed p-12 text-center text-muted-foreground">
          <BookOpen className="mx-auto size-12 opacity-40 mb-3" />
          <p className="text-base font-medium">You are not assigned to any course yet.</p>
          <p className="mt-1 text-sm">Please contact an administrator to be assigned a course.</p>
        </div>
      ) : (
        <div className="grid gap-6 lg:grid-cols-[18rem_1fr]">
          <aside className="space-y-2">
            <h2 className="font-heading text-sm font-semibold text-muted-foreground uppercase tracking-wider px-1">
              Your courses ({courses.length})
            </h2>
            <div className="space-y-1.5">
              {courses.map((c) => {
                const isSelected = selectedCourse?.courseId === c.courseId
                return (
                  <button
                    key={c.courseId}
                    type="button"
                    onClick={() => handleSelectCourse(c)}
                    className={`w-full rounded-2xl p-4 text-left transition-all border ${
                      isSelected
                        ? 'border-brand bg-brand-soft/30 shadow-sm'
                        : 'border-transparent bg-card hover:border-border hover:bg-muted/50'
                    }`}
                  >
                    <span className="font-mono text-xs font-semibold text-brand block mb-0.5">
                      {c.courseCode}
                    </span>
                    <h3 className="font-heading font-medium text-sm text-foreground">
                      {c.courseName}
                    </h3>
                  </button>
                )
              })}
            </div>
          </aside>
          {selectedCourse && (
            <main className="space-y-6">
              <div className="overflow-hidden rounded-3xl border bg-card p-6 shadow-sm sm:p-8">
                <div className="flex flex-wrap items-start justify-between gap-4 border-b pb-6">
                  <div>
                    <span className="font-mono text-xs font-semibold text-brand">
                      {selectedCourse.courseCode}
                    </span>
                    <h2 className="mt-1 font-heading text-2xl font-medium">
                      {selectedCourse.courseName}
                    </h2>
                  </div>

                  <div className="flex items-center gap-2">
                    <Button onClick={() => setIsUploadOpen(true)} className="gap-2 rounded-xl">
                      <Upload className="size-4" />
                      Upload document (.md)
                    </Button>
                  </div>
                </div>
                <div className="mt-6 grid gap-4 sm:grid-cols-2">
                  <div
                    className={`rounded-2xl border p-4 transition-colors ${
                      hasContent
                        ? 'bg-emerald-50/50 border-emerald-200 dark:bg-emerald-950/20 dark:border-emerald-900/40'
                        : 'bg-muted/40'
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        {hasContent ? (
                          <CheckCircle2 className="size-5 text-emerald-600" />
                        ) : (
                          <AlertCircle className="size-5 text-muted-foreground" />
                        )}
                        <span className="font-heading font-medium text-sm">Course content (CONTENT)</span>
                      </div>
                      <Badge
                        variant={contentCount === 1 ? 'outline' : contentCount > 1 ? 'destructive' : 'secondary'}
                        className="text-xs"
                      >
                        {contentCount} file
                      </Badge>
                    </div>
                    <p className="mt-1.5 text-xs text-muted-foreground">
                      {contentCount === 1
                        ? '1 course content Markdown file uploaded.'
                        : contentCount > 1
                        ? 'There is more than 1 CONTENT file. The AI needs exactly 1 CONTENT file.'
                        : 'No course content file yet.'}
                    </p>
                  </div>

                  <div
                    className={`rounded-2xl border p-4 transition-colors ${
                      hasLearningOutcomes
                        ? 'bg-emerald-50/50 border-emerald-200 dark:bg-emerald-950/20 dark:border-emerald-900/40'
                        : 'bg-muted/40'
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        {hasLearningOutcomes ? (
                          <CheckCircle2 className="size-5 text-emerald-600" />
                        ) : (
                          <AlertCircle className="size-5 text-muted-foreground" />
                        )}
                        <span className="font-heading font-medium text-sm">Learning outcomes (LEARNING_OUTCOMES)</span>
                      </div>
                      <Badge
                        variant={outcomesCount === 1 ? 'outline' : outcomesCount > 1 ? 'destructive' : 'secondary'}
                        className="text-xs"
                      >
                        {outcomesCount} file
                      </Badge>
                    </div>
                    <p className="mt-1.5 text-xs text-muted-foreground">
                      {outcomesCount === 1
                        ? '1 learning outcomes file uploaded.'
                        : outcomesCount > 1
                        ? 'There is more than 1 LEARNING_OUTCOMES file.'
                        : 'No learning outcomes file yet.'}
                    </p>
                  </div>
                </div>
                {documents.length > 0 && (!isReadyForAi) && (
                  <div className="mt-4 flex items-start gap-3 rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-xs text-amber-800 dark:text-amber-300">
                    <AlertTriangle className="size-4 shrink-0 text-amber-600 dark:text-amber-400 mt-0.5" />
                    <div className="space-y-1">
                      <p className="font-medium">
                        Document requirements for AI question generation:
                      </p>
                      <p className="text-muted-foreground">
                        The system needs <strong>exactly 1 course content (CONTENT) file</strong> and <strong>1 learning outcomes (LEARNING_OUTCOMES) file</strong>. There are currently {contentCount} content and {outcomesCount} learning outcomes files. Use <strong>Edit</strong> (change the type) or <strong>Delete</strong> (remove extra files) below.
                      </p>
                    </div>
                  </div>
                )}
                <div className="mt-8 space-y-3">
                  <h3 className="font-heading text-sm font-semibold text-muted-foreground uppercase tracking-wider">
                    Uploaded documents ({documents.length})
                  </h3>

                  {isLoadingDocs ? (
                    <div className="py-6 text-center text-sm text-muted-foreground">Loading documents…</div>
                  ) : documents.length === 0 ? (
                    <p className="text-sm text-muted-foreground italic">No documents yet.</p>
                  ) : (
                    <div className="divide-y rounded-2xl border bg-muted/20">
                      {documents.map((doc) => (
                        <div
                          key={doc.documentId}
                          className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-4 hover:bg-muted/30 transition-colors"
                        >
                          <div className="flex items-center gap-3 min-w-0">
                            <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-brand-soft/60 text-brand">
                              <FileText className="size-5" />
                            </div>
                            <div className="min-w-0">
                              <p className="font-medium text-sm text-foreground truncate" title={doc.fileName}>
                                {doc.fileName}
                              </p>
                              <div className="flex flex-wrap items-center gap-2 mt-1">
                                <span className="text-xs text-muted-foreground">
                                  Uploaded {formatDateTime(parseApiDate(doc.uploadedAt))}
                                </span>
                                <Badge
                                  variant={doc.docType === 'CONTENT' ? 'default' : 'secondary'}
                                  className="text-[10px] px-2 py-0"
                                >
                                  {doc.docType === 'CONTENT' ? 'Content (CONTENT)' : 'Learning outcomes (LEARNING_OUTCOMES)'}
                                </Badge>
                              </div>
                            </div>
                          </div>

                          <div className="flex items-center gap-1.5 shrink-0 self-end sm:self-center">
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => openPreviewModal(doc)}
                              className="h-8 gap-1.5 rounded-lg text-xs"
                              title="View document content"
                            >
                              <Eye className="size-3.5" />
                              <span className="hidden sm:inline">Xem</span>
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => openEditModal(doc)}
                              className="h-8 gap-1.5 rounded-lg text-xs hover:text-brand"
                              title="Edit document"
                            >
                              <Edit3 className="size-3.5" />
                              <span className="hidden sm:inline">Edit</span>
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => openDeleteModal(doc)}
                              className="h-8 gap-1.5 rounded-lg text-xs text-destructive hover:bg-destructive/10 hover:text-destructive"
                              title="Delete document"
                            >
                              <Trash2 className="size-3.5" />
                              <span className="hidden sm:inline">Delete</span>
                            </Button>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
                {isReadyForAi && (
                  <div className="mt-8 flex flex-col gap-3 rounded-2xl border border-brand/20 bg-brand-soft/30 p-5 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                      <h4 className="font-heading font-medium text-brand">Ready for AI question generation</h4>
                      <p className="text-xs text-muted-foreground mt-0.5">
                        The course documents are valid (1 CONTENT, 1 LEARNING_OUTCOMES). You can now use AI to build the question bank.
                      </p>
                    </div>
                    <Button asChild size="sm" className="shrink-0 gap-1 rounded-xl">
                      <Link to={`/staff/questions?courseId=${selectedCourse.courseId}`}>
                        Go to question bank
                        <ArrowRight className="size-4" />
                      </Link>
                    </Button>
                  </div>
                )}
              </div>
            </main>
          )}
        </div>
      )}
      {isUploadOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
          <div className="relative w-full max-w-lg rounded-3xl border bg-card p-6 shadow-2xl sm:p-8">
            <header className="flex items-center justify-between border-b pb-4">
              <h2 className="font-heading text-lg font-medium">Upload Markdown document</h2>
              <Button variant="ghost" size="icon" onClick={() => setIsUploadOpen(false)} className="rounded-full">
                <X className="size-5" />
              </Button>
            </header>

            <form onSubmit={handleUploadSubmit} className="mt-6 space-y-5">
              {uploadError && (
                <Alert variant="destructive">
                  <AlertCircle className="size-4" />
                  <AlertDescription>{uploadError}</AlertDescription>
                </Alert>
              )}

              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                  Document type
                </label>
                <div className="grid grid-cols-2 gap-3">
                  <button
                    type="button"
                    onClick={() => setUploadType('CONTENT')}
                    className={`rounded-xl border p-3 text-center text-sm font-medium transition-colors ${
                      uploadType === 'CONTENT'
                        ? 'border-brand bg-brand-soft/40 text-brand font-semibold'
                        : 'border-input hover:bg-muted'
                    }`}
                  >
                    Content (CONTENT)
                  </button>
                  <button
                    type="button"
                    onClick={() => setUploadType('LEARNING_OUTCOMES')}
                    className={`rounded-xl border p-3 text-center text-sm font-medium transition-colors ${
                      uploadType === 'LEARNING_OUTCOMES'
                        ? 'border-brand bg-brand-soft/40 text-brand font-semibold'
                        : 'border-input hover:bg-muted'
                    }`}
                  >
                    Learning outcomes (LEARNING_OUTCOMES)
                  </button>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                  Choose a Markdown file (.md)
                </label>
                <input
                  type="file"
                  accept=".md"
                  onChange={(e) => setUploadFile(e.target.files?.[0] ?? null)}
                  className="w-full text-sm file:mr-4 file:py-2.5 file:px-4 file:rounded-xl file:border-0 file:text-xs file:font-semibold file:bg-brand file:text-white hover:file:bg-brand-hover cursor-pointer"
                />
              </div>

              <div className="flex justify-end gap-3 border-t pt-5">
                <Button variant="ghost" type="button" onClick={() => setIsUploadOpen(false)}>
                  Cancel
                </Button>
                <Button type="submit" disabled={isUploading || !uploadFile} className="gap-2">
                  <Upload className="size-4" />
                  {isUploading ? 'Uploading…' : 'Upload'}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}
      {editingDoc && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
          <div className="relative w-full max-w-lg rounded-3xl border bg-card p-6 shadow-2xl sm:p-8">
            <header className="flex items-center justify-between border-b pb-4">
              <div>
                <h2 className="font-heading text-lg font-medium">Edit document</h2>
                <p className="text-xs text-muted-foreground mt-0.5 truncate max-w-xs">{editingDoc.fileName}</p>
              </div>
              <Button variant="ghost" size="icon" onClick={() => setEditingDoc(null)} className="rounded-full">
                <X className="size-5" />
              </Button>
            </header>

            <form onSubmit={handleEditSubmit} className="mt-6 space-y-5">
              {editError && (
                <Alert variant="destructive">
                  <AlertCircle className="size-4" />
                  <AlertDescription>{editError}</AlertDescription>
                </Alert>
              )}

              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                  Document type
                </label>
                <div className="grid grid-cols-2 gap-3">
                  <button
                    type="button"
                    onClick={() => setEditType('CONTENT')}
                    className={`rounded-xl border p-3 text-center text-sm font-medium transition-colors ${
                      editType === 'CONTENT'
                        ? 'border-brand bg-brand-soft/40 text-brand font-semibold'
                        : 'border-input hover:bg-muted'
                    }`}
                  >
                    Content (CONTENT)
                  </button>
                  <button
                    type="button"
                    onClick={() => setEditType('LEARNING_OUTCOMES')}
                    className={`rounded-xl border p-3 text-center text-sm font-medium transition-colors ${
                      editType === 'LEARNING_OUTCOMES'
                        ? 'border-brand bg-brand-soft/40 text-brand font-semibold'
                        : 'border-input hover:bg-muted'
                    }`}
                  >
                    Learning outcomes (LEARNING_OUTCOMES)
                  </button>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                  Display file name
                </label>
                <input
                  type="text"
                  value={editFileName}
                  onChange={(e) => setEditFileName(e.target.value)}
                  className="w-full rounded-xl border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand"
                  placeholder="ten_tai_lieu.md"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-1">
                  Upload a replacement file (.md) – optional
                </label>
                <p className="text-xs text-muted-foreground mb-2">
                  Leave empty if you only want to change the type or the file name.
                </p>
                <input
                  type="file"
                  accept=".md"
                  onChange={(e) => setEditFile(e.target.files?.[0] ?? null)}
                  className="w-full text-sm file:mr-4 file:py-2.5 file:px-4 file:rounded-xl file:border-0 file:text-xs file:font-semibold file:bg-muted hover:file:bg-muted/80 cursor-pointer"
                />
              </div>

              <div className="flex justify-end gap-3 border-t pt-5">
                <Button variant="ghost" type="button" onClick={() => setEditingDoc(null)}>
                  Cancel
                </Button>
                <Button type="submit" disabled={isEditing} className="gap-2">
                  {isEditing ? 'Saving…' : 'Save changes'}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}
      {deletingDoc && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
          <div className="relative w-full max-w-md rounded-3xl border bg-card p-6 shadow-2xl sm:p-8">
            <header className="flex items-center justify-between border-b pb-4">
              <div className="flex items-center gap-3 text-destructive">
                <div className="rounded-xl bg-destructive/10 p-2.5">
                  <Trash2 className="size-5" />
                </div>
                <h2 className="font-heading text-lg font-medium text-foreground">Delete document?</h2>
              </div>
              <Button variant="ghost" size="icon" onClick={() => setDeletingDoc(null)} className="rounded-full">
                <X className="size-5" />
              </Button>
            </header>

            <div className="mt-4 space-y-4">
              {deleteError && (
                <Alert variant="destructive">
                  <AlertCircle className="size-4" />
                  <AlertDescription>{deleteError}</AlertDescription>
                </Alert>
              )}

              <p className="text-sm text-foreground">
                Are you sure you want to remove this document from the course?
              </p>

              <div className="rounded-2xl border bg-muted/40 p-4">
                <div className="flex items-center gap-2">
                  <FileText className="size-4 text-brand" />
                  <span className="font-medium text-sm text-foreground break-all">{deletingDoc.fileName}</span>
                </div>
                <Badge variant="outline" className="mt-2 text-xs">
                  {deletingDoc.docType === 'CONTENT' ? 'Content (CONTENT)' : 'Learning outcomes (LEARNING_OUTCOMES)'}
                </Badge>
              </div>

              <p className="text-xs text-muted-foreground">
                This cannot be undone. Questions already generated in the question bank are kept.
              </p>

              <div className="flex justify-end gap-3 border-t pt-5">
                <Button variant="ghost" type="button" onClick={() => setDeletingDoc(null)}>
                  Cancel
                </Button>
                <Button
                  variant="destructive"
                  onClick={handleDeleteConfirm}
                  disabled={isDeleting}
                  className="gap-2"
                >
                  <Trash2 className="size-4" />
                  {isDeleting ? 'Deleting…' : 'Delete document'}
                </Button>
              </div>
            </div>
          </div>
        </div>
      )}
      {previewDoc && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
          <div className="relative flex max-h-[85vh] w-full max-w-2xl flex-col rounded-3xl border bg-card p-6 shadow-2xl sm:p-8">
            <header className="flex items-center justify-between border-b pb-4">
              <div className="min-w-0 pr-4">
                <h2 className="font-heading text-lg font-medium truncate">{previewDoc.fileName}</h2>
                <div className="flex items-center gap-2 mt-1">
                  <Badge variant="secondary" className="text-xs">
                    {previewDoc.docType === 'CONTENT' ? 'Course content' : 'Learning outcomes'}
                  </Badge>
                  <span className="text-xs text-muted-foreground">
                    {formatDateTime(parseApiDate(previewDoc.uploadedAt))}
                  </span>
                </div>
              </div>
              <div className="flex items-center gap-2 shrink-0">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={isLoadingPreview || previewError !== null}
                  onClick={() => void handleDownload(previewDoc)}
                  className="gap-1.5 rounded-xl text-xs"
                >
                  <Download className="size-3.5" />
                  Download
                </Button>
                <Button variant="ghost" size="icon" onClick={() => { previewRequest.current++; setPreviewDoc(null) }} className="rounded-full">
                  <X className="size-5" />
                </Button>
              </div>
            </header>

            <div className="mt-4 flex-1 overflow-auto rounded-2xl border bg-muted/30 p-4 font-mono text-xs text-foreground leading-relaxed whitespace-pre-wrap select-text max-h-[50vh]">
              {isLoadingPreview ? (
                <div className="py-12 text-center text-muted-foreground font-sans text-sm">Loading content…</div>
              ) : previewError ? <p role="alert" className="text-destructive">{previewError}</p> : previewContent ? (
                previewContent
              ) : (
                <div className="py-12 text-center text-muted-foreground font-sans text-sm">This document has no text content.</div>
              )}
            </div>

            <div className="mt-4 flex justify-end border-t pt-4">
              <Button onClick={() => { previewRequest.current++; setPreviewDoc(null) }}>Close</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
