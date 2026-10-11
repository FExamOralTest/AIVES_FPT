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

/**
 * - ready:   every endpoint the page needs works today.
 * - partial: the page can be built, but some endpoints are broken or missing.
 * - pending: the core endpoint is missing or broken, so the page waits for the backend.
 */
export type FeatureStatus = 'ready' | 'partial' | 'pending'

/** ok = callable now · issue = exists but broken/limited · missing = not implemented yet. */
export type EndpointState = 'ok' | 'issue' | 'missing'

export interface FeatureEndpoint {
  method: 'GET' | 'POST' | 'PUT' | 'DELETE'
  path: string
  state: EndpointState
  note: string
}

export interface FeatureDef {
  role: UserRole
  /** Path segment under the role's home, e.g. "users" → /admin/users. */
  path: string
  title: string
  icon: LucideIcon
  summary: string
  status: FeatureStatus
  endpoints: FeatureEndpoint[]
  /** What still blocks the page; shown on the placeholder. */
  blockers?: string[]
}

// Endpoint states reflect backend branch `dev` @ 0b7a3a5 (2026-10-11); see docs/backend-api-issues.md.
export const FEATURES: FeatureDef[] = [
  // ── Admin ──────────────────────────────────────────────────────────
  {
    role: 'ADMIN',
    path: 'users',
    title: 'Người dùng',
    icon: Users,
    summary: 'Xem, tạo tài khoản giảng viên và sinh viên, sửa thông tin và đổi vai trò.',
    status: 'partial',
    endpoints: [
      { method: 'GET', path: '/api/auth/admin/users', state: 'ok', note: 'Danh sách toàn bộ người dùng.' },
      { method: 'POST', path: '/api/auth/admin/create-user', state: 'ok', note: 'Tạo tài khoản kèm vai trò.' },
      { method: 'PUT', path: '/api/auth/admin/users/{id}', state: 'ok', note: 'Sửa thông tin, đổi vai trò. Không tự đổi vai trò của mình.' },
      { method: 'GET', path: '/api/admin/users/role/{role}', state: 'issue', note: 'Lỗi 500: backend chưa đăng ký IUserService.' },
      { method: 'DELETE', path: '/api/admin/users/{id}', state: 'issue', note: 'Lỗi 500: backend chưa đăng ký IUserService.' },
    ],
    blockers: ['Chức năng xoá tài khoản chờ backend sửa lỗi 500. Lọc theo vai trò tạm làm phía giao diện.'],
  },
  {
    role: 'ADMIN',
    path: 'courses',
    title: 'Môn học',
    icon: BookOpen,
    summary: 'Tạo môn học và phân công giảng viên phụ trách.',
    status: 'pending',
    endpoints: [
      { method: 'GET', path: '/api/admin/courses', state: 'issue', note: 'Lỗi 500: backend chưa đăng ký ICourseService.' },
      { method: 'POST', path: '/api/admin/courses', state: 'issue', note: 'Lỗi 500: cùng nguyên nhân.' },
      { method: 'PUT', path: '/api/admin/courses/{id}', state: 'issue', note: 'Lỗi 500: cùng nguyên nhân.' },
      { method: 'PUT', path: '/api/admin/courses/{id}/assign-teacher', state: 'issue', note: 'Lỗi 500: cùng nguyên nhân.' },
      { method: 'DELETE', path: '/api/admin/courses/{id}', state: 'issue', note: 'Lỗi 500: cùng nguyên nhân.' },
    ],
    blockers: ['Toàn bộ API môn học lỗi 500 vì thiếu 3 dòng đăng ký dịch vụ trong Program.cs.'],
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

  // ── Teacher ────────────────────────────────────────────────────────
  {
    role: 'TEACHER',
    path: 'courses',
    title: 'Môn học & tài liệu',
    icon: BookOpen,
    summary: 'Xem môn được phân công, tải lên 2 file .md: nội dung môn học và chuẩn đầu ra.',
    status: 'pending',
    endpoints: [
      { method: 'GET', path: '/api/teacher/courses', state: 'missing', note: 'Đề xuất: môn học được phân công cho giảng viên đang đăng nhập.' },
      { method: 'GET', path: '/api/courses/{courseId}/documents', state: 'ok', note: 'Danh sách file .md của môn.' },
      { method: 'POST', path: '/api/courses/{courseId}/documents', state: 'issue', note: 'Tải lên được nhưng chưa chặn trùng loại file và giới hạn 5MB.' },
      { method: 'DELETE', path: '/api/courses/{courseId}/documents/{id}', state: 'missing', note: 'Chưa có API xoá hoặc thay file.' },
    ],
    blockers: ['Giảng viên chưa có API lấy danh sách môn học, nên không biết courseId để tải tài liệu.'],
  },
  {
    role: 'TEACHER',
    path: 'questions',
    title: 'Ngân hàng câu hỏi',
    icon: LibraryBig,
    summary: 'Dùng AI sinh câu hỏi từ tài liệu môn học, xem và chỉnh sửa câu hỏi.',
    status: 'pending',
    endpoints: [
      { method: 'POST', path: '/api/courses/{courseId}/documents/generate-questions', state: 'issue', note: 'Luôn trả về 0 câu: backend gọi sai route AI và che mất lỗi.' },
      { method: 'GET', path: '/api/courses/{courseId}/questions', state: 'missing', note: 'Đề xuất: xem ngân hàng câu hỏi của môn.' },
      { method: 'PUT', path: '/api/courses/{courseId}/questions/{id}', state: 'missing', note: 'Đề xuất: sửa câu hỏi.' },
    ],
    blockers: ['AI chưa sinh được câu hỏi và chưa có API xem danh sách câu hỏi.'],
  },
  {
    role: 'TEACHER',
    path: 'exams',
    title: 'Đề thi',
    icon: FileText,
    summary: 'Tạo đề thi từ ngân hàng câu hỏi, đặt thời lượng và số câu.',
    status: 'partial',
    endpoints: [
      { method: 'GET', path: '/api/exams/my', state: 'ok', note: 'Đề thi do giảng viên đang đăng nhập tạo.' },
      { method: 'GET', path: '/api/exams/{examId}', state: 'ok', note: 'Chi tiết đề kèm câu hỏi.' },
      { method: 'POST', path: '/api/exams', state: 'ok', note: 'Tạo đề. Hệ thống tự sinh mã EX-XXXXXXXX.' },
      { method: 'POST', path: '/api/exams/ai-suggest-questions', state: 'issue', note: 'Chỉ lọc theo từ khoá, chưa dùng AI. Ngân hàng câu hỏi đang trống.' },
      { method: 'PUT', path: '/api/exams/{examId}', state: 'missing', note: 'Chưa sửa, xoá hay đổi trạng thái đề được (luôn là DRAFT).' },
    ],
    blockers: ['Chưa có API lấy courseId và ngân hàng câu hỏi đang trống, nên hiện chỉ tạo được đề chưa có câu hỏi.'],
  },
  {
    role: 'TEACHER',
    path: 'sessions',
    title: 'Ca thi',
    icon: CalendarClock,
    summary: 'Lên lịch ca thi, chia sẻ mã tham gia, mở hoặc đóng ca và theo dõi trực tiếp.',
    status: 'partial',
    endpoints: [
      { method: 'POST', path: '/api/exam-sessions', state: 'ok', note: 'Tạo ca thi, trả về mã tham gia ESS-XXXXXXXX.' },
      { method: 'GET', path: '/api/exam-sessions/teacher/{teacherId}', state: 'ok', note: 'Danh sách ca thi của giảng viên.' },
      { method: 'GET', path: '/api/exam-sessions/{sessionId}', state: 'ok', note: 'Chi tiết ca thi.' },
      { method: 'PUT', path: '/api/exam-sessions/{sessionId}/status', state: 'issue', note: 'Chỉ nhận số 0–3, không nhận chuỗi "IN_PROGRESS".' },
      { method: 'GET', path: '/api/exam-sessions/{sessionId}/monitor', state: 'issue', note: 'Gọi được, nhưng tiến độ làm bài luôn bằng 0%.' },
    ],
  },
  {
    role: 'TEACHER',
    path: 'results',
    title: 'Chấm & công bố',
    icon: ClipboardCheck,
    summary: 'Xem điểm AI đề xuất, nghe lại câu trả lời, chỉnh điểm và công bố kết quả.',
    status: 'partial',
    endpoints: [
      { method: 'GET', path: '/api/results/session/{sessionId}', state: 'ok', note: 'Kết quả theo ca thi.' },
      { method: 'GET', path: '/api/results/{resultId}', state: 'ok', note: 'Chi tiết một bài để chấm.' },
      { method: 'PUT', path: '/api/results/{resultId}/review', state: 'ok', note: 'Lưu điểm và nhận xét của giảng viên.' },
      { method: 'POST', path: '/api/results/{resultId}/publish', state: 'ok', note: 'Công bố kết quả chính thức.' },
      { method: 'POST', path: '/api/results/{resultId}/regrade', state: 'missing', note: 'Đề xuất: yêu cầu AI chấm lại.' },
    ],
    blockers: ['Chưa có luồng thi vấn đáp nên chưa có kết quả nào. Cần dữ liệu mẫu để dựng giao diện.'],
  },

  // ── Student ────────────────────────────────────────────────────────
  {
    role: 'STUDENT',
    path: 'enroll',
    title: 'Tham gia ca thi',
    icon: KeyRound,
    summary: 'Nhập mã tham gia do giảng viên cung cấp để đăng ký vào ca thi.',
    status: 'ready',
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
    status: 'pending',
    endpoints: [
      { method: 'GET', path: '/api/student/enrollments', state: 'missing', note: 'Đề xuất: danh sách ca thi đã đăng ký.' },
    ],
    blockers: ['Backend chưa có API cho sinh viên xem các ca thi đã đăng ký.'],
  },
  {
    role: 'STUDENT',
    path: 'viva',
    title: 'Phòng thi vấn đáp',
    icon: Mic,
    summary: 'Nghe câu hỏi của AI, trả lời bằng giọng nói, nhận câu hỏi phụ khi cần.',
    status: 'pending',
    endpoints: [
      { method: 'POST', path: '/api/viva/{enrollmentId}/start', state: 'missing', note: 'Đề xuất: bắt đầu bài, nhận câu hỏi đầu tiên và audio.' },
      { method: 'POST', path: '/api/viva/{enrollmentId}/answer', state: 'missing', note: 'Đề xuất: gửi audio hoặc chữ, nhận câu tiếp theo.' },
      { method: 'POST', path: '/api/viva/{enrollmentId}/finish', state: 'missing', note: 'Đề xuất: nộp bài.' },
      { method: 'GET', path: '/api/viva/{enrollmentId}/state', state: 'missing', note: 'Đề xuất: khôi phục bài khi tải lại trang.' },
    ],
    blockers: ['Chưa có hợp đồng API cho luồng thi: định dạng audio, câu hỏi phụ, trả lời bằng chữ khi lỗi giọng nói.'],
  },
  {
    role: 'STUDENT',
    path: 'results',
    title: 'Kết quả',
    icon: Trophy,
    summary: 'Xem điểm, nhận xét và transcript sau khi giảng viên công bố.',
    status: 'pending',
    endpoints: [
      { method: 'GET', path: '/api/student/results', state: 'missing', note: 'Đề xuất: danh sách kết quả của sinh viên.' },
      { method: 'GET', path: '/api/student/results/{id}', state: 'missing', note: 'Đề xuất: chi tiết, chỉ khi đã công bố.' },
    ],
    blockers: ['Backend chưa có API cho sinh viên xem kết quả.'],
  },
]

export function featuresFor(role: UserRole) {
  return FEATURES.filter((f) => f.role === role)
}

export const STATUS_LABEL: Record<FeatureStatus, string> = {
  ready: 'Sẵn sàng gọi API',
  partial: 'Gọi được một phần',
  pending: 'Đang hoàn thiện',
}

export const ENDPOINT_STATE_LABEL: Record<EndpointState, string> = {
  ok: 'Gọi được',
  issue: 'Có lỗi',
  missing: 'Chưa có',
}
