import { useCallback, useRef, useState } from 'react'
import {
  abortChunkedUpload,
  type ChunkedUploadProgress,
  uploadAudioChunked,
} from '@/lib/media/chunkedUploader'

export interface UseChunkedUploadState {
  /** Đang upload chunk — UI nên disable nút submit. */
  isUploading: boolean
  /** Tiến độ upload hiện tại (null khi chưa upload). */
  progress: ChunkedUploadProgress | null
  /** Đường dẫn file local sau khi server gộp xong (C:\audio_review\...). */
  localFilePath: string | null
  /** Cảnh báo lưu trữ: audio KHÔNG lên được R2, chỉ nằm trên đĩa local server. */
  storageWarning: { error: string; hint: string } | null
  /** Upload audio chunked, trả về uploadId của phiên upload (dùng khi submit answer). */
  upload: (args: {
    sessionId: number
    questionId: number
    file: Blob
    fileName: string
  }) => Promise<string>
  /** Hủy upload đang chạy (dùng khi học sinh bấm Cancel hoặc đổi câu hỏi). */
  cancel: () => void
}

/**
 * Hook wrapper quanh chunkedUploader — giữ state tiến độ và cho phép huỷ.
 *
 * Mục tiêu: chunk nhỏ tạo ở máy người dùng → server gộp file local → R2 chỉ còn
 * 1 file lớn → chunk tạm trên máy user bị xóa.
 */
export function useChunkedUpload(): UseChunkedUploadState {
  const [isUploading, setIsUploading] = useState(false)
  const [progress, setProgress] = useState<ChunkedUploadProgress | null>(null)
  const [localFilePath, setLocalFilePath] = useState<string | null>(null)
  const [storageWarning, setStorageWarning] = useState<{ error: string; hint: string } | null>(null)
  const abortRef = useRef<AbortController | null>(null)
  const uploadIdRef = useRef<string | null>(null)

  const upload = useCallback(
    async (args: { sessionId: number; questionId: number; file: Blob; fileName: string }) => {
      const controller = new AbortController()
      abortRef.current = controller
      setIsUploading(true)
      setProgress(null)
      setLocalFilePath(null)
      setStorageWarning(null)

      try {
        const result = await uploadAudioChunked({
          ...args,
          onProgress: setProgress,
          onUploadId: (uploadId) => {
            uploadIdRef.current = uploadId
          },
          signal: controller.signal,
        })
        uploadIdRef.current = null
        setLocalFilePath(result.localFilePath ?? null)
        if (result.storageFallback) {
          setStorageWarning({
            error: result.storageError ?? 'Upload lên Cloudflare R2 thất bại.',
            hint: result.storageHint ?? 'Kiểm tra cấu hình Cloudflare R2 của server.',
          })
        }
        return result.uploadId
      } finally {
        abortRef.current = null
        setIsUploading(false)
      }
    },
    [],
  )

  const cancel = useCallback(() => {
    abortRef.current?.abort()
    abortRef.current = null
    setIsUploading(false)
    setProgress(null)
    setLocalFilePath(null)
    setStorageWarning(null)
    if (uploadIdRef.current) {
      void abortChunkedUpload(uploadIdRef.current)
      uploadIdRef.current = null
    }
  }, [])

  return { isUploading, progress, localFilePath, storageWarning, upload, cancel }
}
