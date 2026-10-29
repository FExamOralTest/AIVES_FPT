import { http } from '@/lib/http/client'
import { clearChunks, isOpfsSupported, readChunk, writeChunk } from './opfsStore'

/**
 * Chunked audio uploader — chia file ghi âm thành nhiều chunk nhỏ ở máy người dùng,
 * upload từng chunk lên R2 (S3 multipart), rồi gọi complete để R2 gộp thành
 * DUY NHẤT 1 file lớn giống hệt file ghi âm gốc.
 *
 * Luồng:
 *   1. init(sessionId, questionId, file) → server tạo multipart session trên R2, trả uploadId
 *   2. chia blob thành chunks 5 MB → ghi tạm vào OPFS → PUT từng chunk (tuần tự)
 *   3. complete(uploadId) → R2 gộp parts thành 1 object → xóa toàn bộ chunk tạm
 *
 * R2 yêu cầu mỗi part ≥ 5 MB (trừ part cuối) — nên CHUNK_SIZE phải ≥ 5 MB.
 * Backend cũng giới hạn mỗi request chunk ≤ 10 MB.
 */
export const CHUNK_SIZE = 5 * 1024 * 1024

const MAX_CHUNK_SIZE = 10 * 1024 * 1024

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
  jobId?: string | null
  status: string
  completedAt: string
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
      contentType: file.type || 'audio/webm',
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
