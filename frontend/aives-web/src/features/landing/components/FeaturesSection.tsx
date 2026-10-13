import { Check, CircleCheck, FileText } from 'lucide-react'
import { COURSE_FILES, FEATURES } from '../content'
import { Reveal, RevealItem } from './Reveal'
import { SectionHeading } from './SectionHeading'

export function FeaturesSection() {
  return (
    <section id="features" className="scroll-mt-6 px-4 pt-28 sm:px-6">
      <Reveal className="mx-auto grid max-w-[1200px] items-center gap-14 lg:grid-cols-2">
        <RevealItem className="relative flex flex-col gap-3 overflow-hidden rounded-[32px] bg-muted px-8 py-12 sm:px-10">
          <div aria-hidden="true" className="absolute -top-[70px] -left-[70px] size-[220px] rounded-full bg-sun" />
          <span className="relative text-[13px] font-semibold text-muted-foreground">Tài liệu môn học</span>
          {COURSE_FILES.map((file) => (
            <div key={file.name} className="relative flex items-center gap-3.5 rounded-2xl bg-white px-[18px] py-4">
              <span className="inline-flex size-10 items-center justify-center rounded-xl bg-brand-soft text-brand">
                <FileText className="size-[18px]" />
              </span>
              <div className="flex min-w-0 flex-1 flex-col gap-0.5">
                <span className="text-sm font-semibold">{file.name}</span>
                <span className="text-xs text-muted-foreground">{file.kind}</span>
              </div>
              <CircleCheck className="size-5 text-green-600" aria-label="Đã tải lên" />
            </div>
          ))}
          <div className="relative mt-2 self-end rounded-2xl bg-brand px-4 py-3 text-[13px] font-semibold text-white">
            AI chỉ hỏi trong phạm vi 2 file này
          </div>
        </RevealItem>

        <div className="flex flex-col gap-7">
          <RevealItem index={1}>
            <SectionHeading align="start" label="Tính năng" title="Chấm công bằng, giảng viên vẫn là người quyết" />
          </RevealItem>
          <ul className="flex flex-col gap-5">
            {FEATURES.map((feature, i) => (
              <RevealItem as="li" key={feature.title} index={i + 2} className="flex gap-3.5">
                <span className="inline-flex size-7 shrink-0 items-center justify-center rounded-full bg-brand text-white">
                  <Check className="size-3.5" strokeWidth={3} />
                </span>
                <div className="flex flex-col gap-1">
                  <span className="text-base font-semibold">{feature.title}</span>
                  <span className="text-[15px] leading-relaxed text-muted-foreground">{feature.body}</span>
                </div>
              </RevealItem>
            ))}
          </ul>
        </div>
      </Reveal>
    </section>
  )
}
