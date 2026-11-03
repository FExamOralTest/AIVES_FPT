import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { toApiError } from '@/lib/http/apiError'
import { teacherResultsApi } from '../api/teacherResultsApi'
import type { ResultForReviewDto, RetakeDto } from '../types'

function localInput(date: Date) {
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
}

export function RetakePanel({ result, onGranted }: { result: ResultForReviewDto; onGranted: () => void }) {
  const [history, setHistory] = useState<RetakeDto[]>([])
  const [loaded, setLoaded] = useState(false)
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const lock = useRef(false)
  const [reason, setReason] = useState('')
  const [start, setStart] = useState(() => localInput(new Date()))
  const [end, setEnd] = useState(() => localInput(new Date(Date.now() + 3600000)))
  const [duration, setDuration] = useState(30)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  useEffect(() => {
    let cancelled = false
    teacherResultsApi.retakeHistory(result.resultId).then(items => {
      if (!cancelled) { setHistory(items); setLoaded(true) }
    }).catch(err => { if (!cancelled) setError(toApiError(err).message) })
    return () => { cancelled = true }
  }, [result.resultId])
  const existing = history.find(item => item.originalResultId === result.resultId)
  const canGrant = loaded && !existing && result.status !== 'PUBLISHED' && result.status !== 'CANCELLED'
  const canSchedule = existing && !existing.consumedAt
  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (lock.current || (!canGrant && !canSchedule)) return
    const starts = new Date(start), ends = new Date(end)
    if (reason.trim().length < 5 || !Number.isFinite(starts.getTime()) || !Number.isFinite(ends.getTime())
      || ends.getTime() <= Date.now() || !Number.isInteger(duration) || duration < 1 || duration > 240 || ends.getTime() - starts.getTime() < duration * 60000) {
      setError('Nhập lý do ít nhất 5 ký tự và khung giờ đủ thời lượng làm bài (1–240 phút).'); return
    }
    lock.current = true; setBusy(true); setError(null)
    try {
      const body = { reason: reason.trim(), startTime: starts.toISOString(), endTime: ends.toISOString(), durationMinutes: duration }
      const grant = canSchedule ? await teacherResultsApi.rescheduleRetake(existing.retakeId, body) : await teacherResultsApi.grantRetake(result.resultId, body)
      setHistory(await teacherResultsApi.retakeHistory(result.resultId))
      setNotice(`Đã cấp lịch thi bù. Sinh viên xem ca #${grant.retakeSessionId} trong danh sách ca thi của mình.`)
      setOpen(false); onGranted()
    } catch (err) { setError(toApiError(err).message) }
    finally { lock.current = false; setBusy(false) }
  }
  function showForm() {
    if (existing) { setStart(localInput(new Date(existing.startTime))); setEnd(localInput(new Date(existing.endTime))); setDuration(existing.durationMinutes) }
    setOpen(value => !value); setError(null)
  }
  const input = 'mt-1 w-full rounded-xl border bg-background p-3 text-sm'
  return <section className="space-y-3 rounded-2xl border p-5">
    <h3 className="font-heading font-semibold">Thi lại do sự cố</h3>
    <p className="text-sm text-muted-foreground">Cấp một ca thi bù riêng cho sinh viên. Bài cũ được giữ làm lịch sử và không được tính điểm chính thức.</p>
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    {notice && <p role="status" className="text-sm text-success">{notice}</p>}
    {result.status === 'PUBLISHED' && <p className="text-sm">Bài đã công bố cần quy trình điều chỉnh riêng.</p>}
    {(canGrant || canSchedule) && <Button type="button" variant="outline" disabled={busy} onClick={showForm}>{canSchedule ? 'Điều chỉnh lịch chưa sử dụng' : 'Cho phép thi lại'}</Button>}
    {open && <form onSubmit={submit} className="space-y-3">
      <p className="text-sm">Đề thi: {result.examTitle}. Nếu vào muộn, thời gian làm bài bị giới hạn bởi giờ kết thúc cho phép.</p>
      <label className="block text-sm">Lý do sự cố<textarea aria-label="Lý do sự cố" required minLength={5} maxLength={1000} className={input} value={reason} onChange={e => setReason(e.target.value)} /></label>
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="text-sm">Được vào từ<input aria-label="Được vào từ" type="datetime-local" required className={input} value={start} onChange={e => setStart(e.target.value)} /></label>
        <label className="text-sm">Hạn kết thúc<input aria-label="Hạn kết thúc" type="datetime-local" required className={input} value={end} onChange={e => setEnd(e.target.value)} /></label>
      </div>
      <label className="block text-sm">Thời lượng (phút)<input aria-label="Thời lượng" type="number" min={1} max={240} required className={input} value={duration} onChange={e => setDuration(Number(e.target.value))} /></label>
      <p className="text-sm text-muted-foreground">Xác nhận sẽ hủy hiệu lực bài hiện tại; điểm, audio và nhận xét cũ vẫn được lưu.</p>
      <Button type="submit" disabled={busy}>{busy ? 'Đang lưu…' : canSchedule ? 'Lưu lịch mới' : 'Xác nhận cấp thi lại'}</Button>
    </form>}
    {history.length > 0 && <div className="space-y-3 border-t pt-3"><h4 className="text-sm font-semibold">Lịch sử thi bù</h4>{history.map((item, index) => <div key={item.retakeId} className="rounded-xl bg-muted p-3 text-sm">
      <p>Lượt thi bù {index + 1} · Bài cũ #{item.originalResultId} đã hủy hiệu lực · {item.status === 'GRANTED' ? 'Đã cấp phép' : item.status === 'IN_PROGRESS' ? 'Đang làm bài' : item.status === 'EXPIRED' ? 'Đã hết hạn' : item.status === 'INVALIDATED' ? 'Đã hủy hiệu lực' : 'Đã nộp bài'}</p>
      <p>{item.reason}</p><p>{new Date(item.startTime).toLocaleString()} – {new Date(item.endTime).toLocaleString()} · {item.durationMinutes} phút</p>
      <p>Người cấp phép #{item.grantedBy} · {new Date(item.grantedAt).toLocaleString()}</p>
      <Link className="font-medium text-brand underline" to={`/examiner/results?sessionId=${item.retakeSessionId}`}>Xem ca thi bù #{item.retakeSessionId}</Link>
      {item.resultStatus === 'PUBLISHED' && <p>Điểm chính thức của lượt này: {item.finalScore ?? 'Chưa có điểm'}</p>}
    </div>)}</div>}
  </section>
}
