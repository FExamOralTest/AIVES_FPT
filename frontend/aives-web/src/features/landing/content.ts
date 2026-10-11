// Landing copy, taken from the AIVES SRS (business rules BR-01…BR-13, user stories US-01…US-12).

export const NAV_LINKS = [
  { href: '#how', label: 'Cách hoạt động' },
  { href: '#features', label: 'Tính năng' },
  { href: '#roles', label: 'Vai trò' },
  { href: '#faq', label: 'Hỏi đáp' },
]

export const FACTS = [
  { value: '3', label: 'Vai trò trong hệ thống' },
  { value: '4', label: 'Bước cho một buổi thi' },
  { value: '2', label: 'File .md cho mỗi môn' },
  { value: '100%', label: 'Điểm do giảng viên duyệt' },
]

export const STEPS = [
  { title: 'Nhập mã kỳ thi', body: 'Mã giảng viên cung cấp mở đúng buổi vấn đáp của bạn.', tone: 'dark' },
  { title: 'Trả lời bằng giọng nói', body: 'AI đọc câu hỏi thành tiếng, bạn trả lời qua micro, hệ thống ghi transcript.', tone: 'brand' },
  { title: 'AI chấm và nhận xét', body: 'AI chấm từng câu theo nội dung môn và viết nhận xét gợi ý.', tone: 'sun' },
  { title: 'Giảng viên công bố', body: 'Giảng viên xem lại, điều chỉnh nếu cần, rồi công bố điểm chính thức.', tone: 'light' },
] as const

export const COURSE_FILES = [
  { name: 'noi-dung-mon-hoc.md', kind: 'Nội dung bài học' },
  { name: 'chuan-dau-ra.md', kind: 'Chuẩn đầu ra (learning outcomes)' },
]

export const FEATURES = [
  { title: 'Câu hỏi bám sát nội dung môn', body: 'Giảng viên tải lên nội dung và chuẩn đầu ra, AI chỉ hỏi trong phạm vi đó.' },
  { title: 'Hỏi thêm khi trả lời chưa rõ', body: 'AI đặt câu hỏi phụ để bạn giải thích kỹ hơn, trong giới hạn số câu.' },
  { title: 'Có transcript cho từng câu', body: 'Mọi câu trả lời được ghi lại để giảng viên và sinh viên xem lại.' },
  { title: 'Giảng viên duyệt trước khi công bố', body: 'Điểm AI chỉ là gợi ý cho tới khi giảng viên phê duyệt.' },
]

export const ROLES = [
  {
    key: 'student',
    name: 'Sinh viên',
    items: ['Vào thi bằng mã kỳ thi', 'Trả lời bằng giọng nói', 'Xem điểm, nhận xét và transcript'],
  },
  {
    key: 'teacher',
    name: 'Giảng viên',
    items: ['Tải nội dung môn và chuẩn đầu ra', 'Tạo kỳ thi, lên lịch phòng thi', 'Duyệt kết quả AI và công bố điểm'],
  },
  {
    key: 'admin',
    name: 'Quản trị viên',
    items: ['Tạo và quản lý môn học', 'Cấp tài khoản giảng viên', 'Phân quyền theo vai trò'],
  },
] as const

export type RoleKey = (typeof ROLES)[number]['key']

export const FAQS = [
  {
    q: 'Ai được tạo tài khoản?',
    a: 'Sinh viên tự đăng ký bằng email. Tài khoản giảng viên và quản trị viên do nhà trường cấp.',
  },
  {
    q: 'Điểm AI chấm có phải điểm chính thức không?',
    a: 'Không. AI chỉ đưa ra điểm gợi ý. Điểm chính thức là điểm giảng viên đã duyệt và công bố.',
  },
  {
    q: 'Tôi cần chuẩn bị gì để vào thi?',
    a: 'Một máy tính có micro, trình duyệt và mã kỳ thi do giảng viên cung cấp.',
  },
  {
    q: 'Ai xem được bài làm của tôi?',
    a: 'Chỉ bạn và giảng viên phụ trách kỳ thi. Dữ liệu được bảo vệ theo Nghị định 13/2023/NĐ-CP.',
  },
]
