import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Logo } from '@/components/common/Logo'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { ROLE_LABEL, useAuth, type UserRole } from '@/features/auth'

const ROLE_INTRO: Record<UserRole, string> = {
  ADMIN: 'Tạo và quản lý môn học, phân quyền tài khoản trong hệ thống.',
  TEACHER: 'Tạo kỳ thi, lên lịch buổi vấn đáp, duyệt và công bố kết quả AI chấm.',
  STUDENT: 'Nhập mã kỳ thi để vào buổi vấn đáp và xem kết quả đã được công bố.',
}

export default function DashboardPage() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [loggingOut, setLoggingOut] = useState(false)

  if (!user) return null

  async function handleLogout() {
    setLoggingOut(true)
    await logout()
    navigate('/login', { replace: true })
  }

  const profile = [
    { label: 'Tên đăng nhập', value: user.username },
    { label: 'Email', value: user.email ?? 'Chưa có' },
    { label: 'Vai trò', value: ROLE_LABEL[user.role] },
    { label: 'Mã người dùng', value: String(user.userId) },
  ]

  return (
    <div className="min-h-svh">
      <header className="border-b px-4 sm:px-6">
        <div className="mx-auto flex h-20 max-w-[1200px] items-center justify-between gap-4">
          <Logo />
          <div className="flex min-w-0 items-center gap-3">
            <Badge className="h-7 bg-brand-soft px-3 text-[13px] text-brand">{ROLE_LABEL[user.role]}</Badge>
            <span className="hidden truncate font-medium sm:inline">{user.fullName}</span>
            <Button variant="secondary" size="sm" onClick={handleLogout} disabled={loggingOut}>
              {loggingOut ? 'Đang đăng xuất…' : 'Đăng xuất'}
            </Button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-[1200px] px-4 py-14 sm:px-6">
        <h1 className="font-heading text-4xl font-medium tracking-tight sm:text-5xl">Xin chào, {user.fullName}</h1>
        <p className="mt-3 max-w-xl text-[17px] text-muted-foreground">{ROLE_INTRO[user.role]}</p>

        <section aria-labelledby="profile-heading" className="mt-12 max-w-xl rounded-3xl bg-muted p-8">
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
      </main>
    </div>
  )
}
