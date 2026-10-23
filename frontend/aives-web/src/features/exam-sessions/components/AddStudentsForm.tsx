import { CheckCircle2, CircleAlert, GraduationCap, Loader2, UserPlus } from 'lucide-react'
import { useId, useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { toApiError } from '@/lib/http/apiError'
import { cn } from '@/lib/utils'
import { sessionsApi } from '../api/sessionsApi'
import type { BulkEnrollResult, EnrollOutcome } from '../types'

const SKIP_LABEL: Record<Exclude<EnrollOutcome, 'added'>, string> = {
  already_enrolled: 'Đã có trong ca thi',
  not_student: 'Tài khoản không phải sinh viên',
  not_found: 'Không tìm thấy tài khoản (sai MSSV/Email/ID)',
}

/** Max IDs/Identifiers per request, matching backend limit. */
const MAX_IDENTIFIERS = 500

interface AddStudentsFormProps {
  sessionId: number
  onAdded: () => void
}

export function AddStudentsForm({ sessionId, onAdded }: AddStudentsFormProps) {
  const inputId = useId()
  const [text, setText] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<BulkEnrollResult | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [importingCourse, setImportingCourse] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setResult(null)
    const tokens = text.split(/[\s,;]+/).map((t) => t.trim()).filter(Boolean)
    if (tokens.length === 0) {
      return setError('Vui lòng nhập ít nhất một Mã SV (MSSV), Email hoặc ID số.')
    }
    if (tokens.length > MAX_IDENTIFIERS) {
      return setError(`Chỉ thêm tối đa ${MAX_IDENTIFIERS} sinh viên một lần.`)
    }

    setError(null)
    setSubmitting(true)
    try {
      const res = await sessionsApi.addStudents(sessionId, { identifiers: tokens })
      setResult(res)
      if (res.addedCount > 0) {
        setText('')
        onAdded()
      }
    } catch (err) {
      setError(toApiError(err, 'Không thể thêm sinh viên vào ca thi.').message)
    } finally {
      setSubmitting(false)
    }
  }

  async function handleImportCourseStudents() {
    setResult(null)
    setError(null)
    setImportingCourse(true)
    try {
      const res = await sessionsApi.enrollCourseStudents(sessionId)
      setResult(res)
      if (res.addedCount > 0) {
        onAdded()
      }
    } catch (err) {
      setError(toApiError(err, 'Không thể nhập sinh viên từ môn học.').message)
    } finally {
      setImportingCourse(false)
    }
  }

  const skipped = result?.results.filter((r) => r.status !== 'added') ?? []

  return (
    <div className="flex flex-col gap-3 rounded-2xl border bg-muted/30 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <label htmlFor={inputId} className="text-sm font-semibold text-foreground">
          Thêm sinh viên vào ca thi
        </label>
        <Button
          type="button"
          size="sm"
          variant="outline"
          disabled={importingCourse || submitting}
          onClick={handleImportCourseStudents}
          className="h-8 gap-1.5 border-brand/40 text-brand hover:bg-brand-soft text-xs"
          title="Tự động thêm tất cả sinh viên đã đăng ký vào môn học của ca thi"
        >
          {importingCourse ? (
            <Loader2 className="size-3.5 animate-spin" />
          ) : (
            <GraduationCap className="size-3.5" />
          )}
          {importingCourse ? 'Đang nhập…' : 'Nhập từ môn học'}
        </Button>
      </div>

      <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-2">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-start">
          <textarea
            id={inputId}
            rows={2}
            value={text}
            onChange={(e) => {
              setText(e.target.value)
              if (error) setError(null)
            }}
            placeholder="Dán hoặc gõ MSSV (SE160001), Email (sv@fpt.edu.vn) hoặc ID... Phân cách bằng dấu phẩy, khoảng trắng hoặc xuống dòng"
            aria-invalid={Boolean(error)}
            aria-describedby={`${inputId}-hint`}
            className={cn(
              'w-full min-w-0 flex-1 rounded-xl border border-input bg-background p-2.5 text-xs outline-none resize-y min-h-[46px] focus:border-brand focus:ring-3 focus:ring-brand/15',
              error && 'border-destructive ring-2 ring-destructive/20',
            )}
          />
          <Button
            type="submit"
            size="sm"
            disabled={submitting || importingCourse || !text.trim()}
            className="h-10 sm:h-[46px] shrink-0 gap-1.5 px-4"
          >
            {submitting ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <UserPlus className="size-4" />
            )}
            {submitting ? 'Đang thêm…' : 'Thêm sinh viên'}
          </Button>
        </div>

        <p id={`${inputId}-hint`} className="text-[12px] text-muted-foreground">
          Hỗ trợ nhập hỗn hợp Mã SV, Email trường hoặc ID. Hệ thống tự động đối soát, kiểm tra trùng lặp và phân bổ đề thi.
        </p>

        {error && (
          <p role="alert" className="flex items-center gap-2 rounded-lg bg-destructive/10 px-3 py-2 text-xs text-destructive">
            <CircleAlert className="size-4 shrink-0" aria-hidden="true" />
            {error}
          </p>
        )}

        {result && (
          <div role="status" className="rounded-xl border bg-background p-3 text-xs space-y-1.5">
            <div className="flex items-center gap-1.5 font-medium">
              <CheckCircle2 className="size-4 text-emerald-600" />
              <span className="text-emerald-700">
                Đã thêm thành công <strong>{result.addedCount}</strong> sinh viên.
              </span>
              {skipped.length > 0 && (
                <span className="text-muted-foreground">
                  (Bỏ qua {skipped.length} mục)
                </span>
              )}
            </div>

            {skipped.length > 0 && (
              <ul className="mt-1 list-disc pl-5 text-[11px] text-muted-foreground space-y-0.5">
                {skipped.slice(0, 10).map((r, idx) => (
                  <li key={idx}>
                    <span className="font-mono font-semibold text-foreground">
                      {r.identifier || r.studentCode || `#${r.studentId}`}
                    </span>
                    {r.studentName && ` (${r.studentName})`}:{' '}
                    <span className="text-amber-600 dark:text-amber-400">
                      {SKIP_LABEL[r.status as Exclude<EnrollOutcome, 'added'>] ?? r.reason ?? r.status}
                    </span>
                  </li>
                ))}
                {skipped.length > 10 && (
                  <li className="italic">… và {skipped.length - 10} sinh viên khác.</li>
                )}
              </ul>
            )}
          </div>
        )}
      </form>
    </div>
  )
}
