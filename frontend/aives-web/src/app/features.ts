import {
  BookOpen,
  CalendarClock,
  ClipboardCheck,
  FileText,
  KeyRound,
  LibraryBig,
  ListChecks,
  Mic,
  Trophy,
  Users,
  type LucideIcon,
} from 'lucide-react'
import type { UserRole } from '@/features/auth'
export type FeatureStatus = 'done' | 'ready' | 'partial' | 'pending'
export type EndpointState = 'ok' | 'issue' | 'missing'

export interface FeatureEndpoint {
  method: 'GET' | 'POST' | 'PUT' | 'DELETE'
  path: string
  state: EndpointState
  note: string
}

export interface FeatureDef {
  role: UserRole
  path: string
  title: string
  icon: LucideIcon
  summary: string
  status: FeatureStatus
  endpoints: FeatureEndpoint[]
  blockers?: string[]
}
export const FEATURES: FeatureDef[] = [
  {
    role: 'ADMIN',
    path: 'users',
    title: 'Người dùng',
    icon: Users,
    summary: 'Xem, tạo tài khoản giảng viên và sinh viên, sửa thông tin và đổi vai trò.',
    status: 'done',
    endpoints: [
      { method: 'GET', path: '/api/auth/admin/users', state: 'ok', note: 'Danh sách toàn bộ người dùng.' },
      { method: 'POST', path: '/api/auth/admin/create-user', state: 'ok', note: 'Tạo tài khoản kèm vai trò. Role phải gửi dạng số (0 ADMIN, 1 STUDENT, 2 TEACHER).' },
      { method: 'PUT', path: '/api/auth/admin/users/{id}', state: 'ok', note: 'Sửa thông tin, đổi vai trò (gửi dạng số). Không tự đổi vai trò của mình.' },
      { method: 'GET', path: '/api/admin/users/role/{role}', state: 'ok', note: 'Chỉ quản trị viên được phép thao tác.' },
      { method: 'DELETE', path: '/api/admin/users/{id}', state: 'ok', note: 'Chỉ quản trị viên được phép thao tác.' },
    ],
  },
  {
    role: 'ADMIN',
    path: 'courses',
    title: 'Môn học',
    icon: BookOpen,
    summary: 'Tạo môn học và phân công giảng viên phụ trách.',
    status: 'done',
    endpoints: [
      { method: 'GET', path: '/api/admin/courses', state: 'ok', note: 'Danh sách môn học.' },
      { method: 'POST', path: '/api/admin/courses', state: 'ok', note: 'Tạo môn học mới.' },
      { method: 'PUT', path: '/api/admin/courses/{id}/assign-teacher', state: 'ok', note: 'Phân công giảng viên phụ trách.' },
      { method: 'DELETE', path: '/api/admin/courses/{id}', state: 'ok', note: 'Xóa môn học.' },
    ],
  },
  {
    role: 'ADMIN',
    path: 'reports',
    title: 'Bài thi & kết quả',
    icon: ListChecks,
    summary: 'Theo dõi các bài thi và kết quả sinh viên trên toàn hệ thống.',
    status: 'pending',
    endpoints: [
      { method: 'GET', path: '/api/admin/exams', state: 'missing', note: 'Đề xuất: danh sách bài thi toàn hệ thống.' },
      { method: 'GET', path: '/api/admin/results', state: 'missing', note: 'Đề xuất: kết quả sinh viên toàn hệ thống.' },
    ],
    blockers: ['Backend chưa có API cho quản trị viên xem bài thi và kết quả.'],
  },
  {
    role: 'STAFF',
    path: 'courses',
    title: 'Môn học & tài liệu',
    icon: BookOpen,
    summary: 'Xem môn được phân công, tải lên 2 file .md: nội dung môn học và chuẩn đầu ra.',
    status: 'done',
    endpoints: [
      { method: 'GET', path: '/api/teacher/courses', state: 'ok', note: 'Môn học được phân công cho cán bộ chuyên môn.' },
      { method: 'GET', path: '/api/courses/{courseId}/documents', state: 'ok', note: 'Danh sách file .md của môn.' },
      { method: 'POST', path: '/api/courses/{courseId}/documents', state: 'ok', note: 'Tải lên tài liệu CONTENT hoặc LEARNING_OUTCOMES.' },
    ],
  },
  {
    role: 'STAFF',
    path: 'questions',
    title: 'Ngân hàng câu hỏi',
    icon: LibraryBig,
    summary: 'Dùng AI sinh câu hỏi từ tài liệu môn học, xem và chỉnh sửa câu hỏi.',
    status: 'done',
    endpoints: [
      { method: 'POST', path: '/api/exams/suggest-questions', state: 'ok', note: 'Gợi ý danh sách câu hỏi theo môn học.' },
      { method: 'POST', path: '/api/courses/{courseId}/documents/generate-questions', state: 'ok', note: 'Sinh câu hỏi từ tài liệu và chuẩn đầu ra.' },
      { method: 'GET', path: '/api/courses/{courseId}/questions', state: 'ok', note: 'Danh sách câu hỏi theo môn, tìm kiếm và phân trang.' },
      { method: 'POST', path: '/api/courses/{courseId}/questions', state: 'ok', note: 'Tạo câu hỏi thủ công cho môn được phân công.' },
      { method: 'PUT', path: '/api/courses/{courseId}/questions/{questionId}', state: 'ok', note: 'Sửa câu hỏi chưa dùng trong đề hoặc câu trả lời.' },
      { method: 'DELETE', path: '/api/courses/{courseId}/questions/{questionId}', state: 'ok', note: 'Xoá câu hỏi chưa dùng trong đề hoặc câu trả lời.' },
    ],
  },
  {
    role: 'STAFF',
    path: 'exams',
    title: 'Đề thi',
    icon: FileText,
    summary: 'Tạo đề thi từ ngân hàng câu hỏi, đặt thời lượng và số câu.',
    status: 'done',
    endpoints: [
      { method: 'GET', path: '/api/exams/my', state: 'ok', note: 'Đề thi do cán bộ chuyên môn đang đăng nhập tạo.' },
      { method: 'GET', path: '/api/exams/{examId}', state: 'ok', note: 'Chi tiết đề kèm câu hỏi.' },
      { method: 'POST', path: '/api/exams', state: 'ok', note: 'Tạo đề. Hệ thống tự sinh mã EX-XXXXXXXX.' },
      { method: 'POST', path: '/api/exams/suggest-questions', state: 'ok', note: 'Xếp hạng câu hỏi theo từ khoá trong môn được phân công; selectionMethod là keyword.' },
      { method: 'PUT', path: '/api/exams/{examId}', state: 'ok', note: 'Sửa đề nháp chưa có ca thi.' },
      { method: 'DELETE', path: '/api/exams/{examId}', state: 'ok', note: 'Xóa đề nháp chưa có ca thi.' },
    ],
  },
  {
    role: 'STAFF',
    path: 'sessions',
    title: 'Ca thi',
    icon: CalendarClock,
    summary: 'Lên lịch ca thi, chia sẻ mã tham gia, mở hoặc đóng ca và theo dõi trực tiếp.',
    status: 'done',
    endpoints: [
      { method: 'POST', path: '/api/exam-sessions', state: 'ok', note: 'Tạo ca thi, trả về mã tham gia ESS-XXXXXXXX.' },
      { method: 'GET', path: '/api/exam-sessions/teacher/{teacherId}', state: 'ok', note: 'Danh sách ca thi của cán bộ chuyên môn.' },
      { method: 'GET', path: '/api/exam-sessions/{sessionId}', state: 'ok', note: 'Chi tiết ca thi.' },
      { method: 'PUT', path: '/api/exam-sessions/{sessionId}/status', state: 'issue', note: 'Chỉ nhận số 0–3, không nhận chuỗi "IN_PROGRESS". FE đã gửi dạng số.' },
      { method: 'GET', path: '/api/exam-sessions/{sessionId}/monitor', state: 'issue', note: 'Gọi được, nhưng tiến độ làm bài luôn bằng 0%.' },
    ],
  },
  {
    role: 'EXAMINER',
    path: 'results',
    title: 'Thẩm định & công bố',
    icon: ClipboardCheck,
    summary: 'Xem điểm AI đề xuất, nghe lại câu trả lời từ R2, chỉnh điểm và công bố kết quả.',
    status: 'done',
    endpoints: [
      { method: 'GET', path: '/api/results/pending', state: 'ok', note: 'Danh sách bài thi chờ khảo thí thẩm định.' },
      { method: 'GET', path: '/api/results/session/{sessionId}', state: 'ok', note: 'Danh sách bài làm theo ca thi.' },
      { method: 'GET', path: '/api/results/{resultId}', state: 'ok', note: 'Chi tiết một bài để chấm kèm audio và transcript.' },
      { method: 'PUT', path: '/api/results/{resultId}/review', state: 'ok', note: 'Lưu điểm và nhận xét của khảo thí.' },
      { method: 'POST', path: '/api/results/{resultId}/publish', state: 'ok', note: 'Công bố kết quả chính thức.' },
    ],
  },

  {
    role: 'STUDENT',
    path: 'enroll',
    title: 'Tham gia ca thi',
    icon: KeyRound,
    summary: 'Nhập mã tham gia do giảng viên cung cấp để đăng ký vào ca thi.',
    status: 'done',
    endpoints: [
      { method: 'POST', path: '/api/enroll/session', state: 'ok', note: 'Đăng ký bằng mã ESS-XXXXXXXX.' },
    ],
  },
  {
    role: 'STUDENT',
    path: 'sessions',
    title: 'Ca thi của tôi',
    icon: CalendarClock,
    summary: 'Các ca thi đã đăng ký, thời gian bắt đầu và trạng thái.',
    status: 'done',
    endpoints: [
      { method: 'GET', path: '/api/student/enrollments', state: 'ok', note: 'Danh sách ca thi đã đăng ký của sinh viên đang đăng nhập.' },
    ],
  },
  {
    role: 'STUDENT',
    path: 'viva',
    title: 'Phòng thi vấn đáp',
    icon: Mic,
    summary: 'Nghe câu hỏi của AI, trả lời bằng giọng nói, nhận câu hỏi phụ khi cần.',
    status: 'done',
    endpoints: [
      { method: 'POST', path: '/api/student/viva-sessions/{sessionId}/start', state: 'ok', note: 'Bắt đầu bài thi vấn đáp, nhận câu hỏi đầu tiên.' },
      { method: 'GET', path: '/api/student/viva-sessions/{sessionId}/current-question', state: 'ok', note: 'Lấy câu hỏi hiện tại.' },
      { method: 'POST', path: '/api/student/viva-sessions/{sessionId}/submit-answer', state: 'ok', note: 'Nộp câu trả lời bằng giọng nói / audio / text và nhận phản hồi follow-up.' },
      { method: 'POST', path: '/api/student/viva-sessions/{sessionId}/finish', state: 'ok', note: 'Kết thúc ca thi và AI chấm sơ bộ.' },
    ],
  },
  {
    role: 'STUDENT',
    path: 'results',
    title: 'Kết quả',
    icon: Trophy,
    summary: 'Xem điểm, nhận xét và transcript sau khi giảng viên công bố.',
    status: 'done',
    endpoints: [
      { method: 'GET', path: '/api/student/results/{sessionId}', state: 'ok', note: 'Xem kết quả chi tiết sau khi giảng viên công bố.' },
    ],
  },
]

export function featuresFor(role: UserRole) {
  return FEATURES.filter((f) => f.role === role)
}

export const STATUS_LABEL: Record<FeatureStatus, string> = {
  done: 'Đã hoạt động',
  ready: 'Sẵn sàng gọi API',
  partial: 'Gọi được một phần',
  pending: 'Đang hoàn thiện',
}

export const ENDPOINT_STATE_LABEL: Record<EndpointState, string> = {
  ok: 'Gọi được',
  issue: 'Có lỗi',
  missing: 'Chưa có',
}
