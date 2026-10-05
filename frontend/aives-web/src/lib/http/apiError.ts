import axios from 'axios'

export interface ApiError {
  status: number | null
  message: string
  /** Field-level messages from ASP.NET model validation, keyed by camelCase field name. */
  fieldErrors: Record<string, string>
}

// Backend returns English messages; map the known ones to Vietnamese for the UI.
const KNOWN_MESSAGES: Record<string, string> = {
  'Username already exists.': 'Tên đăng nhập này đã được sử dụng.',
  'Email already exists.': 'Email này đã được đăng ký. Đăng nhập hoặc dùng email khác.',
  'Invalid username or password.': 'Email hoặc mật khẩu không đúng.',
}

export function toApiError(error: unknown, fallback = 'Đã có lỗi xảy ra. Vui lòng thử lại.'): ApiError {
  if (!axios.isAxiosError(error)) {
    return { status: null, message: fallback, fieldErrors: {} }
  }

  // No response, or the dev proxy couldn't reach the backend.
  if (!error.response || [502, 503, 504].includes(error.response.status)) {
    return {
      status: null,
      message: 'Không kết nối được máy chủ. Kiểm tra mạng hoặc thử lại sau.',
      fieldErrors: {},
    }
  }

  const { status, data } = error.response
  const fieldErrors: Record<string, string> = {}

  if (data && typeof data === 'object' && 'errors' in data && data.errors && typeof data.errors === 'object') {
    for (const [key, messages] of Object.entries(data.errors as Record<string, string[]>)) {
      const field = key.charAt(0).toLowerCase() + key.slice(1)
      if (Array.isArray(messages) && messages.length) fieldErrors[field] = messages[0]
    }
  }

  const rawMessage =
    data && typeof data === 'object' && 'message' in data && typeof data.message === 'string'
      ? data.message
      : null

  const message =
    (rawMessage && KNOWN_MESSAGES[rawMessage]) ??
    (status === 400 ? 'Thông tin chưa hợp lệ. Kiểm tra lại các trường bên dưới.' : rawMessage ?? fallback)

  return { status, message, fieldErrors }
}
