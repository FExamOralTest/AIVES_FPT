import type { CSSProperties } from 'react'
import panelBg from '@/assets/images/login-panel-bg.webp'
import robotWave from '@/assets/images/robot-login-wave.webp'
import star from '@/assets/images/star.webp'
import { cn } from '@/lib/utils'

/*
 * Login aside artwork, laid out in the design's own coordinates: a 638.5 × 740 frame.
 * Every length goes through `u()` into container-width units, so the composition scales
 * as one piece and keeps the design's proportions at any panel width.
 */
const FRAME_W = 638.5
const u = (px: number) => `${((px * 100) / FRAME_W).toFixed(3)}cqw`
const box = (x: number, y: number, w: number, h?: number): CSSProperties => ({
  left: u(x),
  top: u(y),
  width: u(w),
  height: h === undefined ? undefined : u(h),
})

// Bar heights (design px) of the voice waveform; it repeats this phrase two and a half times.
const PHRASE = [6, 20, 17.6, 22.2, 39.4, 32.8, 30, 20, 11.6, 7.6, 7.6, 28.6, 35.8, 31.4, 24.8, 38, 18.4, 11.2]
const BARS = [...PHRASE, ...PHRASE, ...PHRASE.slice(0, 9)]

const STARS = [
  { x: 488, y: 255.2, w: 134.7 },
  { x: 471.6, y: 250.4, w: 59.3 },
  { x: 73.3, y: 473, w: 134.8 },
]

function Chip({ x, y, padLeft, children }: { x: number; y: number; padLeft: number; children: string }) {
  return (
    <span
      className="absolute flex items-center rounded-full bg-[#4570ad] font-[Poppins] leading-none whitespace-nowrap text-white"
      style={{ ...box(x, y, 218.1, 68.5), paddingLeft: u(padLeft), fontSize: u(18.3) }}
    >
      {children}
    </span>
  )
}

/** Login aside: a waving robot beside an AI examiner card and floating chips. */
export function LoginPanel() {
  return (
    <div className="absolute inset-0 @container-[size]">
      <img src={panelBg} alt="" aria-hidden="true" className="absolute inset-0 size-full object-cover" />
      {/* Fit the frame inside the panel, pinned bottom-right where the robot is cropped by the panel edge. */}
      <div className="absolute right-0 bottom-0 @container aspect-[638.5/740] w-[min(100cqw,calc(100cqh*638.5/740))]">
        <img src={robotWave} alt="" aria-hidden="true" className="absolute max-w-none" style={box(209.8, 348.8, 498.2)} />

        <Chip x={379.1} y={40.2} padLeft={36}>
          Answer by voice
        </Chip>

        <div className="absolute bg-white" style={{ ...box(121.6, 151.5, 276.3, 155.9), borderRadius: u(36) }}>
          <span
            className="absolute font-[Poppins] leading-none font-bold whitespace-nowrap text-[#2a2929]"
            style={{ left: u(17.2), top: u(18.4), fontSize: u(14.1) }}
          >
            AI examiner, question 2 of 5
          </span>
          <div aria-hidden="true" className="absolute flex items-center" style={box(17.2, 98.9, 250, 40.6)}>
            {BARS.map((h, i) => (
              <span
                key={i}
                className="shrink-0 rounded-full bg-[#1737b2]"
                style={{ width: u(2.2), height: u(h), marginLeft: i ? u(3.23) : 0 }}
              />
            ))}
          </div>
        </div>

        <Chip x={22.7} y={372.6} padLeft={28.6}>
          Examiner Review
        </Chip>

        {STARS.map((s, i) => (
          <img key={i} src={star} alt="" aria-hidden="true" className="absolute" style={box(s.x, s.y, s.w)} />
        ))}
      </div>
    </div>
  )
}

const NEXT_STEPS = [
  { title: 'Sign in with your email', body: 'Use the email and password you just created.' },
  { title: 'Enter the join code', body: 'Your lecturer gives you a code for each viva session.' },
  { title: 'Answer by voice', body: 'Get your microphone ready: the AI asks, you answer.' },
]

/** Register aside: what happens after the account is created. */
export function RegisterPanel() {
  return (
    <div className="relative flex w-full flex-col justify-center gap-7 px-14 py-16">
      <div aria-hidden="true" className="absolute -top-36 -right-32 size-[380px] rounded-full bg-sun" />
      <h2 className="relative max-w-[18ch] font-heading text-[32px] leading-tight font-medium tracking-tight">
        After you sign up
      </h2>
      <ol className="relative flex max-w-[460px] flex-col gap-3">
        {NEXT_STEPS.map((step, i) => {
          const highlighted = i === 1
          return (
            <li
              key={step.title}
              className={cn(
                'flex items-center gap-4 rounded-[20px] px-5 py-[18px]',
                highlighted ? 'bg-brand text-white' : 'bg-white',
              )}
            >
              <span
                className={cn(
                  'inline-flex size-10 shrink-0 items-center justify-center rounded-full font-heading text-[15px] font-semibold',
                  highlighted ? 'bg-white text-foreground' : 'bg-primary text-primary-foreground',
                )}
              >
                {i + 1}
              </span>
              <div className="flex flex-col gap-0.5">
                <span className="text-[15px] font-semibold">{step.title}</span>
                <span className="text-[13px] leading-normal opacity-80">{step.body}</span>
              </div>
            </li>
          )
        })}
      </ol>
      <p className="relative text-[13px] text-muted-foreground">Personal data is processed in accordance with Decree 13/2023/ND-CP.</p>
    </div>
  )
}
