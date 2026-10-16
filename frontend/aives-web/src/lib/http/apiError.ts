import axios from 'axios'

export interface ApiError {
  status: number | null
  message: string
  fieldErrors: Record<string, string>
  rawMessage: string | null
}
const KNOWN_MESSAGES: Record<string, string> = {
  'You cannot delete your own account.': 'Bạn không thể tự xóa tài khoản đang đăng nhập.',
  'Cannot delete the last admin user.': 'Không thể xóa quản trị viên cuối cùng.',
  'This user has related courses, exams, documents, enrollments or results and cannot be deleted.': 'Tài khoản có môn học, bài thi, tài liệu hoặc kết quả liên quan nên không thể xóa.',
  'This course has documents, questions, exams or enrollments and cannot be deleted.': 'Môn học có tài liệu, câu hỏi, bài thi hoặc đăng ký liên quan nên không thể xóa.',
  'Only draft exams without sessions can be changed or deleted.': 'Chỉ có thể sửa hoặc xóa đề nháp chưa có ca thi.',
  'This question is already in use. Create a new question instead.': 'Câu hỏi đã được sử dụng. Hãy tạo câu hỏi mới để thay đổi nội dung.',
  'Course code already exists.': 'Mã môn học đã tồn tại.',
  'This course already has a document of this type.': 'Môn học đã có tài liệu thuộc loại này.',
  'This course already has a document of this type. Replace the existing document instead.': 'Môn học đã có tài liệu thuộc loại này. Hãy thay thế tài liệu hiện có.',
  'Add questions to the exam before creating a session.': 'Thêm câu hỏi vào đề trước khi tạo ca thi.',

  'Username already exists.': 'Tên đăng nhập này đã được sử dụng.',
  'Email already exists.': 'Email này đã được đăng ký. Đăng nhập hoặc dùng email khác.',
  'Invalid username or password.': 'Email hoặc mật khẩu không đúng.',
  'Email already in use.': 'Email này đã được tài khoản khác sử dụng.',
  'Cannot change your own role.': 'Bạn không thể tự đổi vai trò của chính mình.',
  'Cannot demote the last admin user.': 'Không thể hạ quyền quản trị viên cuối cùng.',
  'Password must be at least 8 characters long.': 'Mật khẩu cần ít nhất 8 ký tự.',
  'Password must contain at least one uppercase letter.': 'Mật khẩu cần ít nhất một chữ hoa.',
  'Password must contain at least one lowercase letter.': 'Mật khẩu cần ít nhất một chữ thường.',
  'Password must contain at least one digit.': 'Mật khẩu cần ít nhất một chữ số.',
  'Password must contain at least one special character.': 'Mật khẩu cần ít nhất một ký tự đặc biệt.',
  'Invalid or expired Enroll Code.': 'Mã tham gia không đúng. Kiểm tra lại mã giảng viên cung cấp.',
  'This session is not open for enrollment.': 'Ca thi này đã kết thúc hoặc đã bị huỷ, không thể tham gia.',
  'Session has reached maximum capacity.': 'Ca thi đã đủ số lượng sinh viên. Liên hệ giảng viên để được hỗ trợ.',
  'You are already enrolled in this session.': 'Bạn đã tham gia ca thi này rồi.',
  'StartTime must be earlier than EndTime.': 'Giờ bắt đầu phải trước giờ kết thúc.',
  'StartTime cannot be in the past.': 'Giờ bắt đầu không được ở quá khứ.',
  'You do not have permission to create a session for this exam.': 'Bạn không phải người tạo đề thi này.',
}
const STATUS_MESSAGES: Record<number, string> = {
  403: 'Tài khoản của bạn không có quyền thực hiện thao tác này.',
  429: 'Bạn đã thử quá nhiều lần. Vui lòng đợi khoảng 15 phút rồi thử lại.',
}

export function toApiError(error: unknown, fallback = 'Đã có lỗi xảy ra. Vui lòng thử lại.'): ApiError {
  if (!axios.isAxiosError(error)) {
    return { status: null, message: fallback, fieldErrors: {}, rawMessage: null }
  }
  if (!error.response || [502, 503, 504].includes(error.response.status)) {
    return {
      status: null,
      message: 'Không kết nối được máy chủ. Kiểm tra mạng hoặc thử lại sau.',
      fieldErrors: {},
      rawMessage: null,
    }
  }

  const { status, data } = error.response
  const fieldErrors: Record<string, string> = {}

  if (data && typeof data === 'object' && 'errors' in data && data.errors && typeof data.errors === 'object') {
    for (const [key, messages] of Object.entries(data.errors as Record<string, string[]>)) {
      const field = key.charAt(0).toLowerCase() + key.slice(1)
      if (Array.isArray(messages) && messages.length) fieldErrors[field] = KNOWN_MESSAGES[messages[0]] ?? messages[0]
    }
  }

  const rawMessage =
    data && typeof data === 'object' && 'message' in data && typeof data.message === 'string'
      ? data.message
      : null

  if (status === 429) return { status, message: STATUS_MESSAGES[status], fieldErrors, rawMessage }

  const message =
    (rawMessage && KNOWN_MESSAGES[rawMessage]) ??
    (status === 400 && Object.keys(fieldErrors).length > 0
      ? 'Thông tin chưa hợp lệ. Kiểm tra lại các trường bên dưới.'
      : rawMessage) ??
    STATUS_MESSAGES[status] ??
    (status === 400 ? 'Thông tin chưa hợp lệ. Kiểm tra lại các trường bên dưới.' : fallback)

  return { status, message, fieldErrors, rawMessage }
}
