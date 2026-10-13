import {
  AlertCircle,
  BookOpen,
  CheckCircle2,
  Plus,
  Trash2,
  UserCheck,
  X,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { usersApi } from '@/features/admin-users/api/usersApi'
import type { User } from '@/features/admin-users/types'
import { toApiError } from '@/lib/http/apiError'
import { adminCoursesApi } from '../api/adminCoursesApi'
import type { AdminCourse } from '../types'

export default function AdminCoursesPage() {
  const [courses, setCourses] = useState<AdminCourse[]>([])
  const [teachers, setTeachers] = useState<User[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  // Create course modal state
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [courseCode, setCourseCode] = useState('')
  const [courseName, setCourseName] = useState('')
  const [selectedTeacherId, setSelectedTeacherId] = useState<string>('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  // Assign teacher modal state
  const [assigningCourse, setAssigningCourse] = useState<AdminCourse | null>(null)
  const [assignTeacherId, setAssignTeacherId] = useState<string>('')

  useEffect(() => {
    loadData()
  }, [])

  async function loadData() {
    setIsLoading(true)
    setErrorMessage(null)
    try {
      const [coursesData, usersData] = await Promise.all([
        adminCoursesApi.list(),
        usersApi.list(),
      ])
      setCourses(coursesData)
      setTeachers(usersData.filter((u) => u.role === 'TEACHER'))
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
    } finally {
      setIsLoading(false)
    }
  }

  async function handleCreateSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!courseCode.trim() || !courseName.trim()) return

    setIsSubmitting(true)
    setErrorMessage(null)

    try {
      const newCourse = await adminCoursesApi.create({
        courseCode: courseCode.trim().toUpperCase(),
        courseName: courseName.trim(),
        teacherId: selectedTeacherId ? parseInt(selectedTeacherId) : null,
      })
      setCourses((prev) => [newCourse, ...prev])
      setIsCreateOpen(false)
      setCourseCode('')
      setCourseName('')
      setSelectedTeacherId('')
      setSuccessMessage(`Đã tạo môn học "${newCourse.courseName}" thành công!`)
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleAssignSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!assigningCourse || !assignTeacherId) return

    setIsSubmitting(true)
    setErrorMessage(null)

    try {
      const updated = await adminCoursesApi.assignTeacher(
        assigningCourse.courseId,
        parseInt(assignTeacherId),
      )
      setCourses((prev) =>
        prev.map((c) => (c.courseId === updated.courseId ? updated : c)),
      )
      setAssigningCourse(null)
      setAssignTeacherId('')
      setSuccessMessage('Đã cập nhật phân công giảng viên thành công!')
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleDeleteCourse(courseId: number, courseName: string) {
    if (!confirm(`Bạn có chắc chắn muốn xóa môn học "${courseName}"?`)) return

    try {
      await adminCoursesApi.delete(courseId)
      setCourses((prev) => prev.filter((c) => c.courseId !== courseId))
      setSuccessMessage(`Đã xóa môn học "${courseName}".`)
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
    }
  }

  return (
    <div className="mx-auto max-w-5xl py-6">
      <header className="mb-8 flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="font-heading text-3xl font-medium tracking-tight sm:text-4xl">
            Quản lý Môn học
          </h1>
          <p className="mt-2 text-muted-foreground">
            Danh sách tất cả các môn học trong hệ thống và phân công giảng viên phụ trách.
          </p>
        </div>

        <Button onClick={() => setIsCreateOpen(true)} className="gap-2 rounded-xl">
          <Plus className="size-4" />
          Thêm môn học mới
        </Button>
      </header>

      {errorMessage && (
        <Alert variant="destructive" className="mb-6 rounded-2xl">
          <AlertCircle className="size-4" />
          <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
      )}

      {successMessage && (
        <Alert className="mb-6 rounded-2xl border-emerald-300 bg-emerald-50 text-emerald-900 dark:border-emerald-900/60 dark:bg-emerald-950/40 dark:text-emerald-200">
          <CheckCircle2 className="size-4 text-emerald-600" />
          <AlertDescription className="font-medium">{successMessage}</AlertDescription>
        </Alert>
      )}

      {/* Courses Table */}
      {isLoading ? (
        <div className="py-12 text-center text-muted-foreground">Đang tải danh sách môn học…</div>
      ) : courses.length === 0 ? (
        <div className="rounded-3xl border border-dashed p-12 text-center text-muted-foreground">
          <BookOpen className="mx-auto size-12 opacity-40 mb-3" />
          <p>Chưa có môn học nào trong hệ thống.</p>
        </div>
      ) : (
        <div className="overflow-hidden rounded-3xl border bg-card shadow-sm">
          <div className="divide-y divide-border">
            {courses.map((course) => (
              <div
                key={course.courseId}
                className="flex flex-col gap-4 p-5 transition-colors hover:bg-muted/30 sm:flex-row sm:items-center sm:justify-between sm:p-6"
              >
                <div className="space-y-1">
                  <span className="font-mono text-xs font-semibold text-brand">
                    {course.courseCode}
                  </span>
                  <h3 className="font-heading text-base font-semibold text-foreground">
                    {course.courseName}
                  </h3>
                  <div className="flex items-center gap-2 pt-1">
                    <span className="text-xs text-muted-foreground">Giảng viên phụ trách:</span>
                    {course.teacherName ? (
                      <Badge variant="secondary" className="text-xs font-medium">
                        {course.teacherName}
                      </Badge>
                    ) : (
                      <span className="text-xs italic text-amber-600 dark:text-amber-400">
                        Chưa phân công
                      </span>
                    )}
                  </div>
                </div>

                <div className="flex items-center gap-2 sm:justify-end">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      setAssigningCourse(course)
                      setAssignTeacherId(course.teacherId?.toString() ?? '')
                    }}
                    className="gap-1.5 rounded-xl text-xs"
                  >
                    <UserCheck className="size-3.5" />
                    Phân công GV
                  </Button>

                  <Button
                    variant="ghost"
                    size="icon"
                    onClick={() => handleDeleteCourse(course.courseId, course.courseName)}
                    className="rounded-xl text-destructive hover:bg-destructive/10"
                  >
                    <Trash2 className="size-4" />
                  </Button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Create Course Modal */}
      {isCreateOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
          <div className="relative w-full max-w-md rounded-3xl border bg-card p-6 shadow-2xl sm:p-8">
            <header className="flex items-center justify-between border-b pb-4">
              <h2 className="font-heading text-lg font-medium">Thêm môn học mới</h2>
              <Button variant="ghost" size="icon" onClick={() => setIsCreateOpen(false)} className="rounded-full">
                <X className="size-5" />
              </Button>
            </header>

            <form onSubmit={handleCreateSubmit} className="mt-6 space-y-4">
              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-1.5">
                  Mã môn học (Course Code)
                </label>
                <input
                  type="text"
                  required
                  value={courseCode}
                  onChange={(e) => setCourseCode(e.target.value)}
                  placeholder="Ví dụ: SE201, CS101..."
                  className="h-11 w-full rounded-xl border border-input bg-background px-4 font-mono text-sm uppercase outline-none focus:border-brand"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-1.5">
                  Tên môn học (Course Name)
                </label>
                <input
                  type="text"
                  required
                  value={courseName}
                  onChange={(e) => setCourseName(e.target.value)}
                  placeholder="Ví dụ: Kỹ nghệ phần mềm"
                  className="h-11 w-full rounded-xl border border-input bg-background px-4 text-sm outline-none focus:border-brand"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-1.5">
                  Giảng viên phụ trách (tùy chọn)
                </label>
                <select
                  value={selectedTeacherId}
                  onChange={(e) => setSelectedTeacherId(e.target.value)}
                  className="h-11 w-full rounded-xl border border-input bg-background px-3 text-sm outline-none focus:border-brand"
                >
                  <option value="">-- Chưa phân công --</option>
                  {teachers.map((t) => (
                    <option key={t.userId} value={t.userId}>
                      {t.fullName} ({t.username})
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex justify-end gap-3 border-t pt-5">
                <Button variant="ghost" type="button" onClick={() => setIsCreateOpen(false)}>
                  Hủy
                </Button>
                <Button type="submit" disabled={isSubmitting}>
                  {isSubmitting ? 'Đang tạo…' : 'Tạo môn học'}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Assign Teacher Modal */}
      {assigningCourse && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
          <div className="relative w-full max-w-md rounded-3xl border bg-card p-6 shadow-2xl sm:p-8">
            <header className="flex items-center justify-between border-b pb-4">
              <h2 className="font-heading text-lg font-medium">Phân công giảng viên</h2>
              <Button variant="ghost" size="icon" onClick={() => setAssigningCourse(null)} className="rounded-full">
                <X className="size-5" />
              </Button>
            </header>

            <form onSubmit={handleAssignSubmit} className="mt-6 space-y-4">
              <p className="text-sm font-medium text-foreground">
                Môn học: <span className="font-semibold text-brand">{assigningCourse.courseCode} — {assigningCourse.courseName}</span>
              </p>

              <div>
                <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-1.5">
                  Chọn giảng viên
                </label>
                <select
                  required
                  value={assignTeacherId}
                  onChange={(e) => setAssignTeacherId(e.target.value)}
                  className="h-11 w-full rounded-xl border border-input bg-background px-3 text-sm outline-none focus:border-brand"
                >
                  <option value="">-- Chọn một giảng viên --</option>
                  {teachers.map((t) => (
                    <option key={t.userId} value={t.userId}>
                      {t.fullName} ({t.username})
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex justify-end gap-3 border-t pt-5">
                <Button variant="ghost" type="button" onClick={() => setAssigningCourse(null)}>
                  Hủy
                </Button>
                <Button type="submit" disabled={isSubmitting || !assignTeacherId}>
                  {isSubmitting ? 'Đang lưu…' : 'Cập nhật phân công'}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
