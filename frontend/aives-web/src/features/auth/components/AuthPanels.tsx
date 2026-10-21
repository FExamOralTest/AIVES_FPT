import { DotGrid } from '@/components/common/DotGrid'
import { Waveform } from '@/components/common/Waveform'
import { cn } from '@/lib/utils'

/** Login aside: an AI examiner question card with floating chips. */
export function LoginPanel() {
  return (
    <div className="relative flex w-full items-center justify-center px-10 py-16">
      <div aria-hidden="true" className="absolute -right-28 -bottom-32 size-[380px] rounded-full bg-sun" />
      <DotGrid className="absolute top-11 left-10" />

      <div className="relative flex w-full max-w-[380px] flex-col gap-[18px] rounded-3xl bg-white p-6 shadow-[0_24px_48px_-24px_rgba(17,17,20,0.25)]">
        <div className="flex items-center justify-between">
          <span className="text-[13px] font-semibold text-muted-foreground">AI examiner, question 2 of 5</span>
          <span className="size-2 rounded-full bg-brand" />
        </div>
        <p className="font-heading text-[21px] leading-snug font-medium">
          Explain why you would choose a hash table over a binary search tree for this lookup problem.
        </p>
        <Waveform className="h-12" />
      </div>

      <div className="absolute top-10 right-9 flex flex-col rounded-2xl bg-brand px-4 py-3 text-white shadow-[0_12px_24px_-12px_rgba(15,108,230,0.6)]">
        <span className="text-[13px] font-semibold">Answer by voice</span>
        <span className="text-[11px] opacity-85">Automatically transcribed</span>
      </div>
      <div className="absolute bottom-11 left-9 flex flex-col gap-0.5 rounded-2xl bg-brand px-[18px] py-3.5 text-white shadow-[0_12px_24px_-12px_rgba(15,108,230,0.6)]">
        <span className="self-start rounded-full bg-primary px-2 py-0.5 text-[10px] font-semibold">Examiner</span>
        <span className="font-heading text-lg font-semibold">Reviewed before published</span>
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
