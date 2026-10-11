import { ArrowUpRight, Mic } from 'lucide-react'
import { Link } from 'react-router-dom'
import studentImg from '@/assets/images/student.webp'
import { DotGrid } from '@/components/common/DotGrid'
import { Waveform } from '@/components/common/Waveform'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'

const chipShadow = 'shadow-[0_12px_24px_-12px_rgba(15,108,230,0.6)]'

export function HeroSection() {
  return (
    <section id="top" className="px-4 pt-10 sm:px-6">
      <div className="mx-auto grid max-w-[1200px] items-center gap-12 lg:grid-cols-2">
        <div className="flex flex-col items-start gap-7">
          <Badge variant="secondary" className="h-[30px] px-3.5 text-[13px] text-foreground/75">
            #ThiVanDapAI
          </Badge>
          <h1 className="max-w-[13ch] font-heading text-5xl leading-[1.08] font-medium tracking-[-0.03em] sm:text-[64px]">
            Thi vấn đáp cùng giám khảo AI
          </h1>
          <p className="max-w-[30rem] text-[17px] leading-relaxed text-muted-foreground">
            Nhập mã kỳ thi, nghe câu hỏi và trả lời bằng giọng nói. AI chấm theo đúng nội dung môn học, giảng viên duyệt
            rồi mới công bố điểm.
          </p>
          <div className="flex items-center gap-1.5">
            <Button asChild>
              <Link to="/register">Tạo tài khoản sinh viên</Link>
            </Button>
            <Button asChild size="icon" aria-label="Tạo tài khoản sinh viên">
              <Link to="/register">
                <ArrowUpRight />
              </Link>
            </Button>
          </div>
        </div>

        {/* Visual panel */}
        <div className="relative h-[520px] overflow-hidden rounded-[32px] bg-muted sm:h-[580px]">
          <div aria-hidden="true" className="absolute -bottom-30 left-1/2 size-[420px] -translate-x-1/2 rounded-full bg-sun" />
          <DotGrid className="absolute top-10 left-9" />
          <svg
            aria-hidden="true"
            viewBox="0 0 120 90"
            className="absolute right-[70px] bottom-[150px] hidden h-[90px] w-[120px] text-brand sm:block"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
          >
            <path d="M6 70c18-30 40-48 62-40 18 7 10 30-6 26-14-4-4-30 22-40 10-4 22-4 30 0" />
            <path d="M104 10l10 6-10 6" />
          </svg>

          <img
            src={studentImg}
            alt="Sinh viên cầm sách và balo, mỉm cười"
            className="absolute bottom-0 left-1/2 h-[92%] w-auto max-w-none -translate-x-[46%]"
            width={460}
            height={551}
          />

          <div className={`absolute top-10 right-6 flex items-center gap-2.5 rounded-2xl bg-brand px-4 py-3 text-white ${chipShadow}`}>
            <span className="inline-flex size-8 items-center justify-center rounded-full bg-white/20">
              <Mic className="size-4" />
            </span>
            <div className="flex flex-col">
              <span className="text-[13px] font-semibold">Trả lời bằng giọng nói</span>
              <span className="text-[11px] opacity-85">Tự chuyển thành transcript</span>
            </div>
          </div>

          <div className="absolute top-[210px] right-6 hidden w-[220px] flex-col gap-2.5 rounded-[18px] bg-white px-4 py-3.5 shadow-[0_20px_40px_-20px_rgba(17,17,20,0.35)] sm:flex">
            <div className="flex items-center justify-between">
              <span className="text-[11px] font-semibold text-muted-foreground">Giám khảo AI, câu 2/5</span>
              <span className="size-[7px] rounded-full bg-brand" />
            </div>
            <span className="text-[13px] leading-snug font-medium">Vì sao em dùng interface thay vì kế thừa?</span>
            <Waveform className="h-[26px] gap-0.5" barClassName="w-[3px]" />
          </div>

          <div className={`absolute bottom-12 left-6 flex flex-col gap-0.5 rounded-2xl bg-brand px-[18px] py-3.5 text-white ${chipShadow}`}>
            <span className="self-start rounded-full bg-primary px-2 py-0.5 text-[10px] font-semibold">Kết quả</span>
            <span className="font-heading text-[26px] leading-tight font-semibold">8.5 / 10</span>
            <span className="text-[11px] opacity-85">Chờ giảng viên duyệt</span>
          </div>
        </div>
      </div>
    </section>
  )
}
