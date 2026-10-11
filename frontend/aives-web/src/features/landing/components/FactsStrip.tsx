import { Fragment } from 'react'
import { FACTS } from '../content'
import { Reveal, RevealItem } from './Reveal'

export function FactsStrip() {
  return (
    <Reveal className="px-4 pt-12 sm:px-6">
      <RevealItem className="mx-auto flex max-w-[1200px] flex-wrap items-center justify-between gap-6 rounded-3xl bg-muted px-8 py-8 sm:px-10">
        {FACTS.map((fact, i) => (
          <Fragment key={fact.label}>
            {i > 0 && <span aria-hidden="true" className="hidden size-1.5 rounded-full bg-primary md:block" />}
            <div className="flex flex-col gap-1">
              <span className="font-heading text-[44px] leading-none font-medium tracking-tight">{fact.value}</span>
              <span className="text-sm text-muted-foreground">{fact.label}</span>
            </div>
          </Fragment>
        ))}
      </RevealItem>
    </Reveal>
  )
}
