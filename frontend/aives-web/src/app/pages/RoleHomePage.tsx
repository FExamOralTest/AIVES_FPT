import { ArrowRight } from 'lucide-react'
import { Link } from 'react-router-dom'
import { ROLE_HOME, ROLE_LABEL, useAuth, type UserRole } from '@/features/auth'
import { featuresFor } from '../features'
import { StatusBadge } from '../layout/StatusBadge'

const ROLE_INTRO: Record<UserRole, string> = {
  ADMIN: 'Tạo và quản lý môn học, phân quyền tài khoản trong hệ thống.',
  STAFF: 'Quản lý học liệu môn học, ngân hàng câu hỏi, tạo đề và giám sát ca thi.',
  EXAMINER: 'Thẩm định điểm số, nghe lại ghi âm giọng nói của sinh viên, duyệt và công bố bảng điểm chính thức.',
  STUDENT: 'Nhập mã tham gia để vào buổi vấn đáp và xem kết quả đã được công bố.',
}


export default function RoleHomePage({ role }: { role: UserRole }) {
  const { user } = useAuth()
  if (!user) return null

  const features = featuresFor(role)
  const profile = [
    { label: 'Tên đăng nhập', value: user.username },
    { label: 'Email', value: user.email ?? 'Chưa có' },
    { label: 'Vai trò', value: ROLE_LABEL[user.role] },
  ]

  return (
    <>
      <h1 className="font-heading text-3xl font-medium tracking-tight sm:text-4xl">Xin chào, {user.fullName}</h1>
      <p className="mt-3 max-w-xl text-[17px] text-muted-foreground">{ROLE_INTRO[role]}</p>

      <section aria-labelledby="features-heading" className="mt-10">
        <h2 id="features-heading" className="font-heading text-lg font-medium">
          Chức năng
        </h2>
        <ul className="mt-4 grid gap-4 sm:grid-cols-2">
          {features.map(({ path, title, summary, status, icon: Icon }) => (
            <li key={path}>
              <Link
                to={`${ROLE_HOME[role]}/${path}`}
                className="group flex h-full flex-col gap-3 rounded-2xl border p-5 transition-colors hover:border-brand/40 hover:bg-brand-soft/30"
              >
                <div className="flex items-start justify-between gap-3">
                  <span className="flex size-10 items-center justify-center rounded-xl bg-muted">
                    <Icon className="size-5" aria-hidden="true" />
                  </span>
                  <StatusBadge status={status} />
                </div>
                <div className="flex-1">
                  <h3 className="font-heading font-medium">{title}</h3>
                  <p className="mt-1 text-sm text-muted-foreground">{summary}</p>
                </div>
                <span className="inline-flex items-center gap-1 text-sm font-medium text-brand">
                  Mở trang
                  <ArrowRight className="size-4 transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </section>

      <section aria-labelledby="profile-heading" className="mt-10 max-w-xl rounded-3xl bg-muted p-6 sm:p-8">
        <h2 id="profile-heading" className="font-heading text-lg font-medium">
          Thông tin tài khoản
        </h2>
        <dl className="mt-4 divide-y divide-border">
          {profile.map((row) => (
            <div key={row.label} className="grid gap-1 py-3.5 sm:grid-cols-[10rem_1fr] sm:gap-4">
              <dt className="text-muted-foreground">{row.label}</dt>
              <dd className="font-medium break-all">{row.value}</dd>
            </div>
          ))}
        </dl>
      </section>
    </>
  )
}
