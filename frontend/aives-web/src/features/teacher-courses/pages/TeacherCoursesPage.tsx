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

  // Edit modal state
  const [editingDoc, setEditingDoc] = useState<CourseDocument | null>(null)
  const [editType, setEditType] = useState<DocType>('CONTENT')
  const [editFileName, setEditFileName] = useState('')
  const [editFile, setEditFile] = useState<File | null>(null)
  const [isEditing, setIsEditing] = useState(false)
  const [editError, setEditError] = useState<string | null>(null)

  // Delete modal state
  const [deletingDoc, setDeletingDoc] = useState<CourseDocument | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)
  const [deleteError, setDeleteError] = useState<string | null>(null)

  // Preview modal state
  const [previewDoc, setPreviewDoc] = useState<CourseDocument | null>(null)
  const [previewContent, setPreviewContent] = useState<string>('')
  const [isLoadingPreview, setIsLoadingPreview] = useState(false)

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

  async function refreshDocuments(courseId: number) {
    setIsLoadingDocs(true)
    try {
      const docs = await teacherCoursesApi.getDocuments(courseId)
      setDocuments(docs)
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
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
    setPreviewDoc(doc)
    setPreviewContent(doc.content || '')
    setIsLoadingPreview(true)

    if (selectedCourse) {
      try {
        const detail = await teacherCoursesApi.getDocument(selectedCourse.courseId, doc.documentId)
        setPreviewContent(detail.content || '')
      } catch {
        // Fallback to existing content if available
      } finally {
        setIsLoadingPreview(false)
      }
    } else {
      setIsLoadingPreview(false)
    }
  }

  function handleDownload(doc: CourseDocument) {
    const text = previewContent || doc.content || ''
    const blob = new Blob([text], { type: 'text/markdown;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = doc.fileName
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
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
                        <span className="font-heading font-medium text-sm">Nội dung môn học (CONTENT)</span>
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
                        ? 'Đã tải lên 1 file Markdown nội dung bài giảng.'
                        : contentCount > 1
                        ? 'Đang có nhiều hơn 1 file CONTENT. AI chỉ cần đúng 1 file CONTENT.'
                        : 'Chưa có file nội dung môn học.'}
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
                        <span className="font-heading font-medium text-sm">Chuẩn đầu ra (LEARNING_OUTCOMES)</span>
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
                        ? 'Đã tải lên 1 file chuẩn đầu ra của môn.'
                        : outcomesCount > 1
                        ? 'Đang có nhiều hơn 1 file LEARNING_OUTCOMES.'
                        : 'Chưa có file chuẩn đầu ra.'}
                    </p>
                  </div>
                </div>

                {/* Validation Note if counts != 1 */}
                {documents.length > 0 && (!isReadyForAi) && (
                  <div className="mt-4 flex items-start gap-3 rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-xs text-amber-800 dark:text-amber-300">
                    <AlertTriangle className="size-4 shrink-0 text-amber-600 dark:text-amber-400 mt-0.5" />
                    <div className="space-y-1">
                      <p className="font-medium">
                        Lưu ý yêu cầu cấu hình tài liệu để AI sinh đề:
                      </p>
                      <p className="text-muted-foreground">
                        Hệ thống yêu cầu <strong>đúng 1 file Nội dung (CONTENT)</strong> và <strong>1 file Chuẩn đầu ra (LEARNING_OUTCOMES)</strong>. Hiện tại có {contentCount} file Nội dung và {outcomesCount} file Chuẩn đầu ra. Bạn có thể sử dụng nút <strong>Sửa</strong> (chuyển loại) hoặc <strong>Xóa</strong> (bỏ file thừa) bên dưới.
                      </p>
                    </div>
                  </div>
                )}

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
                                  Tải lên lúc {formatDateTime(parseApiDate(doc.uploadedAt))}
                                </span>
                                <Badge
                                  variant={doc.docType === 'CONTENT' ? 'default' : 'secondary'}
                                  className="text-[10px] px-2 py-0"
                                >
                                  {doc.docType === 'CONTENT' ? 'Nội dung (CONTENT)' : 'Chuẩn đầu ra (LEARNING_OUTCOMES)'}
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
                              title="Xem nội dung tài liệu"
                            >
                              <Eye className="size-3.5" />
                              <span className="hidden sm:inline">Xem</span>
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => openEditModal(doc)}
                              className="h-8 gap-1.5 rounded-lg text-xs hover:text-brand"
                              title="Chỉnh sửa tài liệu"
                            >
                              <Edit3 className="size-3.5" />
                              <span className="hidden sm:inline">Sửa</span>
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => openDeleteModal(doc)}
                              className="h-8 gap-1.5 rounded-lg text-xs text-destructive hover:bg-destructive/10 hover:text-destructive"
                              title="Xóa tài liệu"
                            >
                              <Trash2 className="size-3.5" />
                              <span className="hidden sm:inline">Xóa</span>
                            </Button>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                {/* AI Question Generation Link Banner */}
                {isReadyForAi && (
                  <div className="mt-8 flex flex-col gap-3 rounded-2xl border border-brand/20 bg-brand-soft/30 p-5 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                      <h4 className="font-heading font-medium text-brand">Sẵn sàng sinh câu hỏi AI</h4>
                      <p className="text-xs text-muted-foreground mt-0.5">
                        Tài liệu môn học đã hợp lệ (1 CONTENT, 1 LEARNING_OUTCOMES). Bạn có thể sử dụng AI để tự động trích xuất ngân hàng câu hỏi.
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
                      uploadType === 'CONTENT'
                        ? 'border-brand bg-brand-soft/40 text-brand font-semibold'
                        : 'border-input hover:bg-muted'
                    }`}
                  >
                    Nội dung (CONTENT)
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

      {/* Edit Modal */}
      {editingDoc && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
          <div className="relative w-full max-w-lg rounded-3xl border bg-card p-6 shadow-2xl sm:p-8">
            <header className="flex items-center justify-between border-b pb-4">
              <div>
                <h2 className="font-heading text-lg font-medium">Chỉnh sửa tài liệu</h2>
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
                  Loại tài liệu
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
                    Nội dung (CONTENT)
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
                    Chuẩn đầu ra (LEARNING_OUTCOMES)
                  </button>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                  Tên file hiển thị
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
                  Tải lên file thay thế (.md) - Tùy chọn
                </label>
                <p className="text-xs text-muted-foreground mb-2">
                  Để trống nếu bạn chỉ muốn đổi loại tài liệu hoặc đổi tên file.
                </p>
                <input
                  type="file"
                  accept=".md,.markdown"
                  onChange={(e) => setEditFile(e.target.files?.[0] ?? null)}
                  className="w-full text-sm file:mr-4 file:py-2.5 file:px-4 file:rounded-xl file:border-0 file:text-xs file:font-semibold file:bg-muted hover:file:bg-muted/80 cursor-pointer"
                />
              </div>

              <div className="flex justify-end gap-3 border-t pt-5">
                <Button variant="ghost" type="button" onClick={() => setEditingDoc(null)}>
                  Hủy
                </Button>
                <Button type="submit" disabled={isEditing} className="gap-2">
                  {isEditing ? 'Đang lưu…' : 'Lưu thay đổi'}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Delete Confirmation Modal */}
      {deletingDoc && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
          <div className="relative w-full max-w-md rounded-3xl border bg-card p-6 shadow-2xl sm:p-8">
            <header className="flex items-center justify-between border-b pb-4">
              <div className="flex items-center gap-3 text-destructive">
                <div className="rounded-xl bg-destructive/10 p-2.5">
                  <Trash2 className="size-5" />
                </div>
                <h2 className="font-heading text-lg font-medium text-foreground">Xác nhận xóa tài liệu</h2>
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
                Bạn có chắc chắn muốn xóa file tài liệu sau khỏi môn học?
              </p>

              <div className="rounded-2xl border bg-muted/40 p-4">
                <div className="flex items-center gap-2">
                  <FileText className="size-4 text-brand" />
                  <span className="font-medium text-sm text-foreground break-all">{deletingDoc.fileName}</span>
                </div>
                <Badge variant="outline" className="mt-2 text-xs">
                  {deletingDoc.docType === 'CONTENT' ? 'Nội dung (CONTENT)' : 'Chuẩn đầu ra (LEARNING_OUTCOMES)'}
                </Badge>
              </div>

              <p className="text-xs text-muted-foreground">
                Hành động này không thể hoàn tác. Các câu hỏi đã sinh từ trước trong ngân hàng câu hỏi vẫn được bảo lưu.
              </p>

              <div className="flex justify-end gap-3 border-t pt-5">
                <Button variant="ghost" type="button" onClick={() => setDeletingDoc(null)}>
                  Hủy
                </Button>
                <Button
                  variant="destructive"
                  onClick={handleDeleteConfirm}
                  disabled={isDeleting}
                  className="gap-2"
                >
                  <Trash2 className="size-4" />
                  {isDeleting ? 'Đang xóa…' : 'Xóa tài liệu'}
                </Button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Preview Modal */}
      {previewDoc && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
          <div className="relative flex max-h-[85vh] w-full max-w-2xl flex-col rounded-3xl border bg-card p-6 shadow-2xl sm:p-8">
            <header className="flex items-center justify-between border-b pb-4">
              <div className="min-w-0 pr-4">
                <h2 className="font-heading text-lg font-medium truncate">{previewDoc.fileName}</h2>
                <div className="flex items-center gap-2 mt-1">
                  <Badge variant="secondary" className="text-xs">
                    {previewDoc.docType === 'CONTENT' ? 'Nội dung bài giảng' : 'Chuẩn đầu ra'}
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
                  onClick={() => handleDownload(previewDoc)}
                  className="gap-1.5 rounded-xl text-xs"
                >
                  <Download className="size-3.5" />
                  Tải về
                </Button>
                <Button variant="ghost" size="icon" onClick={() => setPreviewDoc(null)} className="rounded-full">
                  <X className="size-5" />
                </Button>
              </div>
            </header>

            <div className="mt-4 flex-1 overflow-auto rounded-2xl border bg-muted/30 p-4 font-mono text-xs text-foreground leading-relaxed whitespace-pre-wrap select-text max-h-[50vh]">
              {isLoadingPreview ? (
                <div className="py-12 text-center text-muted-foreground font-sans text-sm">Đang tải nội dung…</div>
              ) : previewContent ? (
                previewContent
              ) : (
                <div className="py-12 text-center text-muted-foreground font-sans text-sm">Tài liệu không có nội dung văn bản.</div>
              )}
            </div>

            <div className="mt-4 flex justify-end border-t pt-4">
              <Button onClick={() => setPreviewDoc(null)}>Đóng</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
