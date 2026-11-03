/**
 * OPFS (Origin Private File System) store — lưu các chunk audio tạm ở máy người dùng.
 *
 * Mục đích:
 *   1. File ghi âm gốc (blob trong RAM) được chia nhỏ thành các chunk 5 MB.
 *   2. Mỗi chunk được ghi ra file tạm trong OPFS → upload lên R2 từng phần (multipart).
 *   3. Sau khi R2 gộp xong (1 file lớn duy nhất), toàn bộ chunk tạm bị XÓA.
 *      Chỉ giữ lại file gốc trong RAM để phát lại cho học sinh nghe.
 *
 * OPFS là filesystem riêng của origin (không phải thư mục hệ thống) → không lọt vào
 * bất kỳ thư mục download nào của người dùng, và tự động biến mất khi tab đóng.
 *
 * Fallback: nếu trình duyệt không hỗ trợ OPFS (Safari < 17, Firefox cũ), uploader
 * tự động chuyển sang chế độ in-memory — chunk vẫn được upload đúng, chỉ không có
 * file tạm trên đĩa.
 */

const ROOT_DIR = 'aives-audio-chunks'

/** Thư mục gốc chứa chunk — tạo lazily, xoá khi upload hoàn tất. */
async function getRootDir(): Promise<FileSystemDirectoryHandle> {
  const opfs = await navigator.storage.getDirectory()
  return opfs.getDirectoryHandle(ROOT_DIR, { create: true })
}

/** Kiểm tra trình duyệt có hỗ trợ OPFS hay không. */
export function isOpfsSupported(): boolean {
  return (
    typeof navigator !== 'undefined' &&
    typeof navigator.storage?.getDirectory === 'function' &&
    typeof FileSystemFileHandle !== 'undefined'
  )
}

/** Tên file chunk: `chunk_0.bin`, `chunk_1.bin`, ... */
function chunkFileName(index: number): string {
  return `chunk_${index}.bin`
}

/**
 * Ghi một chunk xuống OPFS.
 * Trả về true nếu ghi thành công, false nếu OPFS không khả dụng.
 */
export async function writeChunk(index: number, data: Blob): Promise<boolean> {
  if (!isOpfsSupported()) return false

  try {
    const dir = await getRootDir()
    const handle = await dir.getFileHandle(chunkFileName(index), { create: true })
    const writable = await handle.createWritable()
    await writable.write(data)
    await writable.close()
    return true
  } catch {
    // OPFS hỏng (hết quota, permission denied) → coi như không hỗ trợ.
    return false
  }
}

/** Đọc lại một chunk từ OPFS. */
export async function readChunk(index: number): Promise<Blob | null> {
  if (!isOpfsSupported()) return null

  try {
    const dir = await getRootDir()
    const handle = await dir.getFileHandle(chunkFileName(index))
    return await handle.getFile()
  } catch {
    return null
  }
}

/**
 * Xóa toàn bộ chunk tạm.
 * Gọi sau khi R2 đã gộp xong — đảm bảo máy người dùng không còn file chunk nào.
 */
export async function clearChunks(): Promise<void> {
  if (!isOpfsSupported()) return

  try {
    const opfs = await navigator.storage.getDirectory()
    await opfs.removeEntry(ROOT_DIR, { recursive: true })
  } catch {
    // Đã bị xóa sạch hoặc không tồn tại → coi như thành công.
  }
}

/** Đếm số chunk hiện còn trong OPFS — dùng để debug/kiểm tra dọn dẹp. */
export async function countChunks(): Promise<number> {
  if (!isOpfsSupported()) return 0

  try {
    const dir = await getRootDir()
    let count = 0
    for await (const _ of dir.entries()) count++
    return count
  } catch {
    return 0
  }
}
