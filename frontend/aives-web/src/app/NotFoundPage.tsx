import { Link } from 'react-router-dom'
import { LogoMark } from '@/components/common/Logo'
import { Button } from '@/components/ui/button'

export default function NotFoundPage() {
  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-5 px-4 text-center">
      <LogoMark className="size-12" />
      <h1 className="font-heading text-3xl font-medium">Không tìm thấy trang này</h1>
      <p className="text-muted-foreground">Đường dẫn có thể đã thay đổi hoặc không tồn tại.</p>
      <Button asChild>
        <Link to="/">Về trang chủ</Link>
      </Button>
    </main>
  )
}
