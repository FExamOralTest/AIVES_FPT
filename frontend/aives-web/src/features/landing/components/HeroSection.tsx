import { ChevronRight } from 'lucide-react'
import { Link } from 'react-router-dom'
import robotImg from '@/assets/images/robot-hero-graduate.webp'
import { Button } from '@/components/ui/button'

const navyButton = 'bg-navy text-white hover:bg-navy-hover focus-visible:ring-navy/40'

export function HeroSection() {
  return (
    <section id="top" className="px-4 pt-10 sm:px-6">
      {/* Two columns at every width: on small screens the text and the robot shrink instead of stacking. */}
      <div className="mx-auto grid max-w-[1200px] grid-cols-[1.05fr_1fr] items-center gap-3 sm:gap-8 lg:gap-10">
        <div className="flex min-w-0 flex-col items-start gap-3 sm:gap-5 lg:gap-7">
          <h1 className="max-w-[13ch] font-heading text-[22px] leading-[1.08] font-semibold tracking-[-0.03em] text-foreground/90 [text-shadow:0_4px_12px_rgba(17,17,20,0.28)] min-[420px]:text-[28px] sm:text-[40px] sm:[text-shadow:0_6px_18px_rgba(17,17,20,0.28)] md:text-5xl lg:text-[64px]">
            Viva exams with an AI examiner
          </h1>
          <p className="max-w-[30rem] text-[11px] leading-snug text-foreground/80 min-[420px]:text-xs sm:text-sm sm:leading-relaxed md:text-[15px] lg:text-[17px]">
            Enter your join code, listen to the questions and answer by voice. The AI scores against the course content,
            and an examiner reviews before the score is published.
          </p>
          {/* Below 420px the column is too narrow for both; the round button only repeats the main link, so it hides. */}
          <div className="flex items-center gap-1.5 sm:gap-3">
            <Button
              asChild
              size="lg"
              className={`h-8 rounded-full px-3 text-[11px] sm:h-10 sm:px-5 sm:text-sm lg:h-12 lg:px-7 ${navyButton}`}
            >
              <Link to="/register">Create a student account</Link>
            </Button>
            <Button
              asChild
              size="icon"
              className={`hidden size-8 rounded-full min-[420px]:inline-flex sm:size-10 lg:size-12 ${navyButton}`}
              aria-label="Create a student account"
            >
              <Link to="/register">
                <ChevronRight className="size-4 lg:size-5" />
              </Link>
            </Button>
          </div>
        </div>

        {/* The design's robot, graduation cap, diploma and stars, cut from it as one transparent image. */}
        <img
          src={robotImg}
          alt=""
          width={1100}
          height={1045}
          className="h-auto w-full"
        />
      </div>
    </section>
  )
}
