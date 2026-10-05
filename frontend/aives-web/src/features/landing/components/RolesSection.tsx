import { Check } from 'lucide-react'
import { cn } from '@/lib/utils'
import { ROLES, type RoleKey } from '../content'
import { Reveal, RevealItem } from './Reveal'
import { RoleIllustration } from './RoleIllustration'
import { SectionHeading } from './SectionHeading'

const CARD_STYLE: Record<RoleKey, { card: string; art: string }> = {
  student: { card: 'bg-brand text-white', art: 'bg-white/12' },
  teacher: { card: 'bg-muted text-foreground', art: 'bg-[#e8eaf0]' },
  admin: { card: 'bg-primary text-primary-foreground', art: 'bg-[#1e1e24]' },
}

export function RolesSection() {
  return (
    <section id="roles" className="scroll-mt-6 px-4 pt-28 sm:px-6">
      <Reveal className="mx-auto flex max-w-[1200px] flex-col items-center gap-12">
        <RevealItem index={-1}>
          <SectionHeading label="Vai trò" title="Mỗi người thấy đúng phần việc của mình" />
        </RevealItem>
        <div className="grid w-full gap-4 md:grid-cols-3">
          {ROLES.map((role, i) => (
            <RevealItem key={role.key} index={i} className={cn('flex flex-col gap-[18px] rounded-3xl p-8', CARD_STYLE[role.key].card)}>
              <div className={cn('flex h-[132px] items-center justify-center overflow-hidden rounded-[18px]', CARD_STYLE[role.key].art)}>
                <RoleIllustration role={role.key} />
              </div>
              <h3 className="font-heading text-2xl font-medium">{role.name}</h3>
              <ul className="flex flex-col gap-3">
                {role.items.map((item) => (
                  <li key={item} className="flex gap-2.5 text-[15px] leading-normal opacity-90">
                    <Check className="mt-0.5 size-[18px] shrink-0" strokeWidth={2.4} />
                    {item}
                  </li>
                ))}
              </ul>
            </RevealItem>
          ))}
        </div>
      </Reveal>
    </section>
  )
}
