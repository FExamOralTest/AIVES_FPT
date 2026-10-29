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
  /** Upload audio chunked, trả về object key trên R2 sau khi gộp. */
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
 * Mục tiêu: chunk nhỏ tạo ở máy người dùng → R2 chỉ còn 1 file lớn → chunk tạm bị xóa.
 */
export function useChunkedUpload(): UseChunkedUploadState {
  const [isUploading, setIsUploading] = useState(false)
  const [progress, setProgress] = useState<ChunkedUploadProgress | null>(null)
  const abortRef = useRef<AbortController | null>(null)
  const uploadIdRef = useRef<string | null>(null)

  const upload = useCallback(
    async (args: { sessionId: number; questionId: number; file: Blob; fileName: string }) => {
      const controller = new AbortController()
      abortRef.current = controller
      setIsUploading(true)
      setProgress(null)

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
        return result.audioKey
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
    if (uploadIdRef.current) {
      void abortChunkedUpload(uploadIdRef.current)
      uploadIdRef.current = null
    }
  }, [])

  return { isUploading, progress, upload, cancel }
}
