import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Reveal, RevealItem } from './Reveal'

export function CtaSection() {
  return (
    <section className="px-4 py-28 sm:px-6">
      <Reveal className="mx-auto max-w-[1200px]">
        <RevealItem className="relative flex flex-wrap items-center justify-between gap-8 overflow-hidden rounded-[32px] bg-brand px-8 py-14 text-white sm:px-14 sm:py-16">
          <div aria-hidden="true" className="absolute -top-30 -right-20 size-[260px] rounded-full bg-sun" />
          <div className="relative flex max-w-xl flex-col gap-3">
            <h2 className="font-heading text-3xl leading-tight font-medium tracking-tight sm:text-[40px]">
              Có mã kỳ thi rồi? Tạo tài khoản để vào thi.
            </h2>
            <p className="text-base opacity-90">Chỉ cần email và mật khẩu.</p>
          </div>
          <div className="relative flex flex-wrap items-center gap-1.5">
            <Button asChild>
              <Link to="/register">Tạo tài khoản sinh viên</Link>
            </Button>
            <Button asChild variant="light">
              <Link to="/login">Đăng nhập</Link>
            </Button>
          </div>
        </RevealItem>
      </Reveal>
    </section>
  )
}
