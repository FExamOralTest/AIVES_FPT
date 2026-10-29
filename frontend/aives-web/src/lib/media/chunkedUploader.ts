import { http } from '@/lib/http/client'
import { clearChunks, isOpfsSupported, readChunk, writeChunk } from './opfsStore'

/**
 * Chunked audio uploader — chia file ghi âm thành nhiều chunk nhỏ ở máy người dùng,
 * upload từng chunk lên server. Server gộp chunks thành 1 file local hoàn chỉnh
 * tại C:\audio_review\{studentId}\{courseName}\{questionId}\q_{questionId}.webm,
 * rồi upload CHÍNH FILE LOCAL đó lên Cloudflare R2 (1 object duy nhất).
 *
 * Luồng:
 *   1. init(sessionId, questionId, file) → server tạo phiên upload + thư mục local
 *   2. chia blob thành chunks 5 MB → ghi tạm vào OPFS → POST từng chunk lên server
 *   3. complete(uploadId) → server gộp local → upload file local lên R2 → xóa chunk tạm
 *   4. xóa toàn bộ chunk tạm trên máy người dùng (giữ file gốc trong RAM để nghe lại)
 *
 * Chunk ở máy user chỉ tồn tại tạm thời trong lúc upload. R2 chỉ giữ 1 file lớn
 * giống hệt file ghi âm gốc.
 */
export const CHUNK_SIZE = 5 * 1024 * 1024

const MAX_CHUNK_SIZE = 10 * 1024 * 1024

/**
 * Chuẩn hóa media type — bỏ toàn bộ tham số.
 *
 * MediaRecorder trả về file.type = "audio/webm;codecs=opus". Gửi nguyên xi
 * header "Content-Type: audio/webm;codecs=opus" lên Cloudflare R2 sẽ bị R2 từ chối
 * ("The request signature we calculated does not match the signature you provided")
 * vì chữ ký SigV4 tính trên header gốc còn R2 canonicalize lại header.
 * → Chỉ giữ "type/subtype".
 */
function normalizeContentType(raw: string | undefined | null): string {
  if (!raw) return 'audio/webm'
  const mainType = raw.split(';')[0]?.trim().toLowerCase()
  return mainType || 'audio/webm'
}

export interface ChunkedUploadInitResponse {
  uploadId: string
  receivedChunks: number[]
  status: string
  localFilePath?: string | null
}

export interface ChunkedUploadChunkResponse {
  uploadId: string
  chunkIndex: number
  receivedChunks: number[]
  totalChunks: number
  isComplete: boolean
}

export interface ChunkedUploadCompleteResponse {
  uploadId: string
  answerId: number
  audioKey: string
  /** Đường dẫn file local đã gộp trên server (C:\audio_review\...). */
  localFilePath?: string | null
  jobId?: string | null
  status: string
  completedAt: string
  /** True nếu upload R2 thất bại — audio chỉ nằm trên đĩa local server. */
  storageFallback?: boolean
  storageError?: string | null
  storageHint?: string | null
}
export interface ChunkedUploadProgress {
  /** Số chunk đã upload thành công. */
  uploaded: number
  /** Tổng số chunk cần upload. */
  total: number
  /** Phase hiện tại để hiển thị trên UI. */
  phase: 'preparing' | 'uploading' | 'completing' | 'done'
}

/** Sinh uploadId ngẫu nhiên (≤ 64 ký tự — giới hạn cột upload_id trong DB). */
function generateUploadId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID().replace(/-/g, '')
  }
  return `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 12)}`
}

/** Cắt blob thành mảng chunk (không copy dữ liệu — Blob.slice là view). */
function splitIntoChunks(blob: Blob, chunkSize: number): Blob[] {
  const chunks: Blob[] = []
  const total = Math.max(1, Math.ceil(blob.size / chunkSize))
  for (let i = 0; i < total; i++) {
    chunks.push(blob.slice(i * chunkSize, Math.min((i + 1) * chunkSize, blob.size)))
  }
  return chunks
}

/** Bắt lỗi HTTP thành message thân thiện. */
function errorMessage(err: unknown): string {
  if (axiosIsError(err)) {
    const data = err.response?.data as { message?: string } | undefined
    return data?.message ?? `Upload failed (HTTP ${err.response?.status ?? 'network'}).`
  }
  return err instanceof Error ? err.message : 'Upload failed.'
}

function axiosIsError(err: unknown): err is { response?: { status?: number; data?: unknown } } {
  return typeof err === 'object' && err !== null && 'response' in err
}

export interface ChunkedUploadArgs {
  sessionId: number
  questionId: number
  file: Blob
  fileName: string
  onProgress?: (progress: ChunkedUploadProgress) => void
  /** Nhận uploadId ngay khi server tạo phiên — dùng để abort khi huỷ. */
  onUploadId?: (uploadId: string) => void
  signal?: AbortSignal
}

/**
 * Upload audio theo chunks và trả về object key trên R2 sau khi gộp.
 *
 * @throws Error với message tiếng Việt nếu upload thất bại.
 */
export async function uploadAudioChunked(args: ChunkedUploadArgs): Promise<ChunkedUploadCompleteResponse> {
  const { sessionId, questionId, file, fileName, onProgress, onUploadId, signal } = args

  if (file.size === 0) {
    throw new Error('Audio file is empty.')
  }

  const chunkSize = Math.min(CHUNK_SIZE, MAX_CHUNK_SIZE)
  const chunks = splitIntoChunks(file, chunkSize)
  const totalChunks = chunks.length

  onProgress?.({ uploaded: 0, total: totalChunks, phase: 'preparing' })

  // 1. Init — server mở multipart session trên R2.
  const init = await http.post<ChunkedUploadInitResponse>(
    '/api/audio/chunked/init',
    {
      uploadId: generateUploadId(),
      sessionId,
      questionId,
      fileName,
      // Bỏ tham số của media type (vd "audio/webm;codecs=opus" → "audio/webm").
      // Backend cũng chuẩn hóa lại, nhưng gửi sạch từ client giúp log/diagnostics rõ ràng.
      contentType: normalizeContentType(file.type),
      totalSize: file.size,
      chunkSize,
      totalChunks,
    },
    { signal },
  )

  const uploadId = init.data.uploadId
  onUploadId?.(uploadId)
  // Server có thể trả về chunk đã nhận từ lần upload trước (resume) — bỏ qua chúng.
  const alreadyReceived = new Set(init.data.receivedChunks ?? [])

  let uploaded = 0

  try {
    for (let i = 0; i < chunks.length; i++) {
      if (alreadyReceived.has(i)) {
        uploaded++
        onProgress?.({ uploaded, total: totalChunks, phase: 'uploading' })
        continue
      }

      // Ghi chunk tạm xuống OPFS (máy user) — nếu browser hỗ trợ.
      // Mục đích: file chunk thật sự tồn tại ở máy user trong lúc upload,
      // và được xóa sạch khi R2 gộp xong.
        if (isOpfsSupported()) {
          await writeChunk(i, chunks[i])
          // Đọc lại từ OPFS để đảm bảo dữ liệu gửi đi đúng là file đã ghi.
          const fromDisk = await readChunk(i)
          if (fromDisk) {
            await uploadChunk(uploadId, i, fromDisk, signal)
          } else {
            await uploadChunk(uploadId, i, chunks[i], signal)
          }
        } else {
          await uploadChunk(uploadId, i, chunks[i], signal)
        }

      uploaded++
      onProgress?.({ uploaded, total: totalChunks, phase: 'uploading' })
    }

    // 2. Complete — R2 gộp toàn bộ parts thành 1 object duy nhất.
    onProgress?.({ uploaded, total: totalChunks, phase: 'completing' })
    const complete = await http.post<ChunkedUploadCompleteResponse>(
      '/api/audio/chunked/complete',
      { uploadId },
      { signal },
    )

    // 3. Dọn chunk tạm trên máy người dùng — R2 đã có 1 file lớn hoàn chỉnh.
    await clearChunks()

    onProgress?.({ uploaded, total: totalChunks, phase: 'done' })
    return complete.data
  } catch (err) {
    // Upload lỗi → dọn chunk tạm để không rác ổ đĩa người dùng.
    await clearChunks()
    throw new Error(errorMessage(err))
  }
}

/** PUT một chunk lên server (server forward sang R2 UploadPart). */
async function uploadChunk(
  uploadId: string,
  chunkIndex: number,
  chunk: Blob,
  signal?: AbortSignal,
): Promise<void> {
  const form = new FormData()
  form.append('uploadId', uploadId)
  form.append('chunkIndex', String(chunkIndex))
  form.append('chunkData', chunk, `chunk_${chunkIndex}.bin`)

  await http.post<ChunkedUploadChunkResponse>('/api/audio/chunked/chunk', form, {
    signal,
    headers: { 'Content-Type': 'multipart/form-data' },
  })
}

/** Hủy phiên upload đang dở (dùng khi học sinh bấm Cancel). */
export async function abortChunkedUpload(uploadId: string): Promise<void> {
  try {
    await http.delete(`/api/audio/chunked/${uploadId}`)
  } catch {
    // Abort là best-effort — lỗi không chặn luồng chính.
  }
  await clearChunks()
}
