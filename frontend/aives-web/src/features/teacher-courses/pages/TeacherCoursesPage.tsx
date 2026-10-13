import {
  AlertCircle,
  ArrowRight,
  BookOpen,
  CheckCircle2,
  FileText,
  Upload,
  X,
} from 'lucide-react'
import { useEffect, useState } from 'react'
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

  // Upload modal state
  const [isUploadOpen, setIsUploadOpen] = useState(false)
  const [uploadType, setUploadType] = useState<DocType>('CONTENT')
  const [uploadFile, setUploadFile] = useState<File | null>(null)
  const [isUploading, setIsUploading] = useState(false)
  const [uploadError, setUploadError] = useState<string | null>(null)

  useEffect(() => {
    loadCourses()
  }, [])

  async function loadCourses() {
    setIsLoadingCourses(true)
    setErrorMessage(null)
    try {
      const data = await teacherCoursesApi.listMine()
      setCourses(data)
      if (data.length > 0) {
        handleSelectCourse(data[0])
      }
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
    } finally {
      setIsLoadingCourses(false)
    }
  }

  async function handleSelectCourse(course: TeacherCourse) {
    setSelectedCourse(course)
    setIsLoadingDocs(true)
    setErrorMessage(null)
    try {
      const docs = await teacherCoursesApi.getDocuments(course.courseId)
      setDocuments(docs)
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
      setDocuments([])
    } finally {
      setIsLoadingDocs(false)
    }
  }

  async function handleUploadSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedCourse || !uploadFile) {
      setUploadError('Vui lòng chọn một file .md')
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
      // Refresh documents
      const docs = await teacherCoursesApi.getDocuments(selectedCourse.courseId)
      setDocuments(docs)
    } catch (err) {
      const apiErr = toApiError(err)
      setUploadError(apiErr.message)
    } finally {
      setIsUploading(false)
    }
  }

  const hasContent = documents.some((d) => d.docType === 'CONTENT')
  const hasLearningOutcomes = documents.some((d) => d.docType === 'LEARNING_OUTCOMES')

  return (
    <div className="mx-auto max-w-5xl py-6">
      <header className="mb-8">
        <h1 className="font-heading text-3xl font-medium tracking-tight sm:text-4xl">
          Môn học & Tài liệu chuẩn
        </h1>
        <p className="mt-2 text-muted-foreground">
          Quản lý các môn học được phân công và tài liệu Markdown (.md) làm cơ sở cho AI sinh đề và câu hỏi vấn đáp.
        </p>
      </header>

      {errorMessage && (
        <Alert variant="destructive" className="mb-6 rounded-2xl">
          <AlertCircle className="size-4" />
          <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
      )}

      {isLoadingCourses ? (
        <div className="py-12 text-center text-muted-foreground">Đang tải danh sách môn học…</div>
      ) : courses.length === 0 ? (
        <div className="rounded-3xl border border-dashed p-12 text-center text-muted-foreground">
          <BookOpen className="mx-auto size-12 opacity-40 mb-3" />
          <p className="text-base font-medium">Bạn chưa được phân công phụ trách môn học nào.</p>
          <p className="mt-1 text-sm">Vui lòng liên hệ Quản trị viên (Admin) để được gán môn học.</p>
        </div>
      ) : (
        <div className="grid gap-6 lg:grid-cols-[18rem_1fr]">
          {/* Courses List Sidebar */}
          <aside className="space-y-2">
            <h2 className="font-heading text-sm font-semibold text-muted-foreground uppercase tracking-wider px-1">
              Môn học của bạn ({courses.length})
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

          {/* Main Content: Course Documents */}
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
                      Tải lên tài liệu (.md)
                    </Button>
                  </div>
                </div>

                {/* Document Readiness Indicators */}
                <div className="mt-6 grid gap-4 sm:grid-cols-2">
                  <div className={`rounded-2xl border p-4 transition-colors ${hasContent ? 'bg-emerald-50/50 border-emerald-200 dark:bg-emerald-950/20 dark:border-emerald-900/40' : 'bg-muted/40'}`}>
                    <div className="flex items-center gap-2">
                      {hasContent ? (
                        <CheckCircle2 className="size-5 text-emerald-600" />
                      ) : (
                        <AlertCircle className="size-5 text-muted-foreground" />
                      )}
                      <span className="font-heading font-medium text-sm">Nội dung môn học (CONTENT)</span>
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {hasContent ? 'Đã tải lên file Markdown nội dung bài giảng.' : 'Chưa có file nội dung môn học.'}
                    </p>
                  </div>

                  <div className={`rounded-2xl border p-4 transition-colors ${hasLearningOutcomes ? 'bg-emerald-50/50 border-emerald-200 dark:bg-emerald-950/20 dark:border-emerald-900/40' : 'bg-muted/40'}`}>
                    <div className="flex items-center gap-2">
                      {hasLearningOutcomes ? (
                        <CheckCircle2 className="size-5 text-emerald-600" />
                      ) : (
                        <AlertCircle className="size-5 text-muted-foreground" />
                      )}
                      <span className="font-heading font-medium text-sm">Chuẩn đầu ra (LEARNING_OUTCOMES)</span>
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {hasLearningOutcomes ? 'Đã tải lên chuẩn đầu ra của môn.' : 'Chưa có file chuẩn đầu ra.'}
                    </p>
                  </div>
                </div>

                {/* Uploaded Documents List */}
                <div className="mt-8 space-y-3">
                  <h3 className="font-heading text-sm font-semibold text-muted-foreground uppercase tracking-wider">
                    Các tài liệu đã tải lên ({documents.length})
                  </h3>

                  {isLoadingDocs ? (
                    <div className="py-6 text-center text-sm text-muted-foreground">Đang tải tài liệu…</div>
                  ) : documents.length === 0 ? (
                    <p className="text-sm text-muted-foreground italic">Chưa có tài liệu nào.</p>
                  ) : (
                    <div className="divide-y rounded-2xl border bg-muted/20">
                      {documents.map((doc) => (
                        <div key={doc.documentId} className="flex items-center justify-between p-4">
                          <div className="flex items-center gap-3">
                            <FileText className="size-5 text-brand" />
                            <div>
                              <p className="font-medium text-sm text-foreground">{doc.fileName}</p>
                              <span className="text-xs text-muted-foreground">
                                Tải lên lúc {formatDateTime(parseApiDate(doc.uploadedAt))}
                              </span>
                            </div>
                          </div>
                          <Badge variant="secondary" className="text-xs">
                            {doc.docType}
                          </Badge>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                {/* AI Question Generation Link Banner */}
                {hasContent && (
                  <div className="mt-8 flex flex-col gap-3 rounded-2xl border border-brand/20 bg-brand-soft/30 p-5 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                      <h4 className="font-heading font-medium text-brand">Sẵn sàng sinh câu hỏi AI</h4>
                      <p className="text-xs text-muted-foreground mt-0.5">
                        Tài liệu môn học đã sẵn sàng. Bạn có thể sử dụng AI để tự động trích xuất ngân hàng câu hỏi.
                      </p>
                    </div>
                    <Button asChild size="sm" className="shrink-0 gap-1 rounded-xl">
                      <Link to={`/teacher/questions?courseId=${selectedCourse.courseId}`}>
                        Đến ngân hàng câu hỏi
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

      {/* Upload Modal */}
      {isUploadOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
          <div className="relative w-full max-w-lg rounded-3xl border bg-card p-6 shadow-2xl sm:p-8">
            <header className="flex items-center justify-between border-b pb-4">
              <h2 className="font-heading text-lg font-medium">Tải lên tài liệu Markdown</h2>
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
                  Loại tài liệu
                </label>
                <div className="grid grid-cols-2 gap-3">
                  <button
                    type="button"
                    onClick={() => setUploadType('CONTENT')}
                    className={`rounded-xl border p-3 text-center text-sm font-medium transition-colors ${
                      uploadType === 'CONTENT' ? 'border-brand bg-brand-soft/40 text-brand font-semibold' : 'border-input hover:bg-muted'
                    }`}
                  >
                    Nội dung (CONTENT)
                  </button>
                  <button
                    type="button"
                    onClick={() => setUploadType('LEARNING_OUTCOMES')}
                    className={`rounded-xl border p-3 text-center text-sm font-medium transition-colors ${
                      uploadType === 'LEARNING_OUTCOMES' ? 'border-brand bg-brand-soft/40 text-brand font-semibold' : 'border-input hover:bg-muted'
                    }`}
                  >
                    Chuẩn đầu ra (LEARNING_OUTCOMES)
                  </button>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                  Chọn file Markdown (.md)
                </label>
                <input
                  type="file"
                  accept=".md,.markdown"
                  onChange={(e) => setUploadFile(e.target.files?.[0] ?? null)}
                  className="w-full text-sm file:mr-4 file:py-2.5 file:px-4 file:rounded-xl file:border-0 file:text-xs file:font-semibold file:bg-brand file:text-white hover:file:bg-brand-hover cursor-pointer"
                />
              </div>

              <div className="flex justify-end gap-3 border-t pt-5">
                <Button variant="ghost" type="button" onClick={() => setIsUploadOpen(false)}>
                  Hủy
                </Button>
                <Button type="submit" disabled={isUploading || !uploadFile} className="gap-2">
                  <Upload className="size-4" />
                  {isUploading ? 'Đang tải lên…' : 'Tải lên ngay'}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
