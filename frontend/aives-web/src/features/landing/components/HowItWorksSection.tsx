import { cn } from '@/lib/utils'
import { STEPS } from '../content'
import { Reveal, RevealItem } from './Reveal'
import { SectionHeading } from './SectionHeading'

const TONE: Record<(typeof STEPS)[number]['tone'], string> = {
  dark: 'bg-primary text-primary-foreground',
  brand: 'bg-brand text-white',
  sun: 'bg-sun text-foreground',
  light: 'bg-white text-foreground',
}

export function HowItWorksSection() {
  return (
    <section id="how" className="scroll-mt-6 px-4 pt-28 sm:px-6">
      <Reveal className="mx-auto flex max-w-[1200px] flex-col items-center gap-12">
        <RevealItem index={-1}>
          <SectionHeading
            label="Cách hoạt động"
            title="Một buổi vấn đáp, bốn bước"
            description="Từ lúc nhập mã kỳ thi đến khi điểm chính thức được công bố."
          />
        </RevealItem>
        <ol className="grid w-full gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {STEPS.map((step, i) => (
            <RevealItem as="li" key={step.title} index={i} className="flex flex-col gap-3.5 rounded-3xl bg-muted p-7">
              <span
                className={cn(
                  'inline-flex size-11 items-center justify-center rounded-full font-heading text-base font-semibold',
                  TONE[step.tone],
                )}
              >
                {i + 1}
              </span>
              <h3 className="font-heading text-[19px] font-medium">{step.title}</h3>
              <p className="text-[15px] leading-relaxed text-muted-foreground">{step.body}</p>
            </RevealItem>
          ))}
        </ol>
      </Reveal>
    </section>
  )
}
