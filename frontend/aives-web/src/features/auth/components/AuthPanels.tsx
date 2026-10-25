import type { ReactNode } from 'react'
import panelBg from '@/assets/images/auth-panel-bg.webp'
import robotHappy from '@/assets/images/robot-mini-happy.webp'
import robotPencil from '@/assets/images/robot-mini-pencil.webp'
import robotMiniWave from '@/assets/images/robot-mini-wave.webp'
import robotWave from '@/assets/images/robot-login-wave.webp'
import star from '@/assets/images/star.webp'
import { designFrame } from '@/lib/designFrame'
import { cn } from '@/lib/utils'

interface PanelFrameProps {
  width: number
  height: number
  /** `corner` pins the frame bottom-right, where the artwork is cropped by the panel edge. */
  pin: 'corner' | 'center'
  children: ReactNode
}

/** Gradient panel holding a design frame of `width` × `height` design px, fitted inside it. */
function PanelFrame({ width, height, pin, children }: PanelFrameProps) {
  const ratio = `${width}/${height}`
  return (
    <div className="absolute inset-0 @container-[size]">
      {/* One background serves every panel: its right edge lines up with each design. */}
      <img src={panelBg} alt="" aria-hidden="true" className="absolute inset-0 size-full object-cover object-right" />
      <div
        className={cn(
          'absolute @container',
          pin === 'corner' ? 'right-0 bottom-0' : 'top-1/2 left-1/2 -translate-1/2',
        )}
        style={{ aspectRatio: ratio, width: `min(100cqw, calc(100cqh * ${ratio}))` }}
      >
        {children}
      </div>
    </div>
  )
}

const login = designFrame(638.5)

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
      style={{ ...login.box(x, y, 218.1, 68.5), paddingLeft: login.u(padLeft), fontSize: login.u(18.3) }}
    >
      {children}
    </span>
  )
}

/** Login aside: a waving robot beside an AI examiner card and floating chips. */
export function LoginPanel() {
  const { u, box } = login
  return (
    <PanelFrame width={638.5} height={740} pin="corner">
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
    </PanelFrame>
  )
}

const register = designFrame(722.3)

type Rect = [x: number, y: number, w: number, h: number]

interface Step {
  title: string
  body: string[]
  card: Rect
  titleAt: [x: number, y: number]
  bodyAt: [x: number, y: number]
  robot: { src: string; x: number; y: number; w: number; rotate?: number }
  /** Paint order in the design: each robot peeks out from behind its card. */
  layer: number
}

// Text offsets are the design's baselines minus the font's ascent at line-height 1.
const STEPS: Step[] = [
  {
    title: 'Sign in with your email',
    body: ['Use the email and password you just created.'],
    card: [119.1, 189.8, 485.1, 88.3],
    titleAt: [28.7, 19.1],
    bodyAt: [28.7, 54.7],
    robot: { src: robotPencil, x: 478.4, y: 112.2, w: 92.25 },
    layer: 1,
  },
  {
    title: 'Enter the join code',
    body: ['Your lecturer gives you a code for each viva', 'session.'],
    card: [119.1, 395.2, 485.1, 109.9],
    titleAt: [27.7, 19],
    bodyAt: [27.7, 55.1],
    // Drawn slightly tilted in the design.
    robot: { src: robotMiniWave, x: 148.95, y: 316.2, w: 105, rotate: -2.37 },
    layer: 2,
  },
  {
    title: 'Answer by voice',
    body: ['Get your microphone ready: the AI asks, you', 'answer.'],
    card: [118.4, 606.5, 485.1, 109.9],
    titleAt: [29.4, 24.8],
    bodyAt: [30, 58.9],
    robot: { src: robotHappy, x: 459.4, y: 539.6, w: 129.75 },
    layer: 0,
  },
]

/** Register aside: what happens after the account is created. */
export function RegisterPanel() {
  const { u, box } = register
  return (
    <PanelFrame width={722.3} height={740.3} pin="center">
      <h2
        className="absolute -translate-x-1/2 font-heading leading-none font-medium tracking-tight whitespace-nowrap text-[#2a2929]"
        // Centred over the cards (the design has it 15px to the right).
        style={{ left: u(361.65), top: u(29.98), fontSize: u(46.8) }}
      >
        After you sign up
      </h2>

      <ol className="contents">
        {STEPS.map(({ title, body, card, titleAt, bodyAt, robot, layer }) => (
          <li key={title} className="contents">
            <img
              src={robot.src}
              alt=""
              aria-hidden="true"
              className="absolute"
              style={{
                ...box(robot.x, robot.y, robot.w),
                rotate: robot.rotate === undefined ? undefined : `${robot.rotate}deg`,
                zIndex: layer * 2,
              }}
            />
            <div
              className="absolute bg-white text-[#2a2929]"
              style={{ ...box(...card), borderRadius: u(37), zIndex: layer * 2 + 1 }}
            >
              <span
                className="absolute leading-none font-semibold whitespace-nowrap"
                style={{ left: u(titleAt[0]), top: u(titleAt[1]), fontSize: u(22.8) }}
              >
                {title}
              </span>
              <span
                className="absolute whitespace-nowrap"
                style={{ left: u(bodyAt[0]), top: u(bodyAt[1]), fontSize: u(18.8), lineHeight: u(19.2) }}
              >
                {body.map((line) => (
                  <span key={line} className="block">
                    {line}
                  </span>
                ))}
              </span>
            </div>
          </li>
        ))}
      </ol>
    </PanelFrame>
  )
}
