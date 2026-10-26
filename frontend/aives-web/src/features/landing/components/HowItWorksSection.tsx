import robotStep1 from '@/assets/images/robot-step-1.webp'
import robotStep2 from '@/assets/images/robot-step-2.webp'
import robotStep3 from '@/assets/images/robot-step-3.webp'
import robotStep4 from '@/assets/images/robot-step-4.webp'
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

// One robot per step, in STEPS order: laptop (join code), clipboard (answering), stars (scoring), graduation cap (publishing).
const ROBOTS = [robotStep1, robotStep2, robotStep3, robotStep4]

export function HowItWorksSection() {
  return (
    <section id="how" className="scroll-mt-6 px-4 pt-28 sm:px-6">
      <Reveal className="mx-auto flex max-w-[1200px] flex-col items-center gap-12">
        <RevealItem index={-1}>
          <SectionHeading
            label="How it works"
            title="One viva exam, four steps"
            description="From entering your join code to receiving your official score."
          />
        </RevealItem>
        <ol className="grid w-full gap-x-4 gap-y-6 sm:grid-cols-2 lg:grid-cols-4">
          {STEPS.map((step, i) => (
            <RevealItem as="li" key={step.title} index={i} className="relative flex flex-col pt-24">
              {/* The robot sits on the card's top edge; the card overlaps its lower half. */}
              <img
                src={ROBOTS[i]}
                alt=""
                width={290}
                height={260}
                loading="lazy"
                className="absolute top-0 left-6 h-36 w-auto"
              />
              <div className="relative flex flex-1 flex-col gap-3.5 rounded-[28px] bg-[#e7e8ec]/90 p-7 backdrop-blur-sm">
                <span
                  className={cn(
                    'inline-flex size-11 items-center justify-center rounded-full font-heading text-base font-semibold shadow-sm',
                    TONE[step.tone],
                  )}
                >
                  {i + 1}
                </span>
                <h3 className="font-heading text-[19px] font-medium">{step.title}</h3>
                <p className="text-[15px] leading-relaxed text-muted-foreground">{step.body}</p>
              </div>
            </RevealItem>
          ))}
        </ol>
      </Reveal>
    </section>
  )
}
