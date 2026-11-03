import { Check, FileText } from 'lucide-react'
import robotArmsOpen from '@/assets/images/robot-mini-arms-open.webp'
import robotMiniWave from '@/assets/images/robot-mini-wave.webp'
import { designFrame } from '@/lib/designFrame'
import { COURSE_FILES, FEATURES } from '../content'
import { Reveal, RevealItem } from './Reveal'
import { SectionHeading } from './SectionHeading'

// Course documents artwork, in the design's 584.2 × 520.3 frame.
const { u, box } = designFrame(584.2)

/*
 * File card position and the robot beside it. A card's row is the regular file row blown
 * up by `scale`; the design draws the two rows at slightly different sizes and offsets.
 */
const FILE_CARDS = [
  {
    x: 162.5,
    y: 104.4,
    padLeft: 27.5,
    scale: 1.5946,
    dy: -0.6,
    robot: { src: robotMiniWave, x: 10.5, y: 88.7, w: 147.75, h: 150.75 },
  },
  {
    x: 11.1,
    y: 358,
    padLeft: 18,
    scale: 1.554,
    dy: -3.3,
    robot: { src: robotArmsOpen, x: 405.1, y: 344.7, w: 165.75, h: 144 },
  },
]

export function FeaturesSection() {
  return (
    <section id="features" className="scroll-mt-6 px-4 pt-28 sm:px-6">
      <Reveal className="mx-auto grid max-w-[1200px] items-center gap-14 lg:grid-cols-2">
        <RevealItem className="@container">
          <div className="relative aspect-[584.2/520.3] bg-[#fbbb14]" style={{ borderRadius: u(64) }}>
            {COURSE_FILES.map((file, i) => {
              const { x, y, padLeft, scale, dy, robot } = FILE_CARDS[i]
              const s = (px: number) => u(px * scale)
              return (
                <div key={file.name} className="contents">
                  <img src={robot.src} alt="" aria-hidden="true" className="absolute" style={box(robot.x, robot.y, robot.w, robot.h)} />
                  <div
                    className="absolute flex items-center bg-white"
                    style={{ ...box(x, y, 394, 117.7), borderRadius: u(37), paddingLeft: u(padLeft) }}
                  >
                    <div className="flex items-center" style={{ gap: s(14), translate: `0 ${u(dy)}` }}>
                      <span
                        className="inline-flex shrink-0 items-center justify-center rounded-full bg-brand-soft text-brand"
                        style={{ width: s(40), height: s(40) }}
                      >
                        <FileText style={{ width: s(18), height: s(18) }} />
                      </span>
                      <div className="flex flex-col" style={{ gap: s(2) }}>
                        <span className="leading-tight font-semibold" style={{ fontSize: s(14) }}>
                          {file.name}
                        </span>
                        <span className="leading-tight text-muted-foreground" style={{ fontSize: s(12) }}>
                          {file.kind}
                        </span>
                      </div>
                    </div>
                  </div>
                </div>
              )
            })}
          </div>
        </RevealItem>

        <div className="flex flex-col gap-7">
          <RevealItem index={1}>
            <SectionHeading align="start" label="Features" title="Fair scoring, with people making the final call" />
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
