import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '@/components/ui/accordion'
import { FAQS } from '../content'
import { Reveal, RevealItem } from './Reveal'
import { SectionHeading } from './SectionHeading'

export function FaqSection() {
  return (
    <section id="faq" className="scroll-mt-6 px-4 pt-28 sm:px-6">
      <Reveal className="mx-auto flex max-w-[800px] flex-col items-center gap-10">
        <RevealItem index={-1}>
          <SectionHeading label="Hỏi đáp" title="Câu hỏi thường gặp" />
        </RevealItem>
        <Accordion type="single" collapsible className="gap-3">
          {FAQS.map((faq, i) => (
            <RevealItem key={faq.q} index={i}>
              <AccordionItem value={faq.q} className="rounded-[20px] border-0 bg-muted px-6">
                <AccordionTrigger className="py-5 text-base font-semibold hover:no-underline">{faq.q}</AccordionTrigger>
                <AccordionContent className="pb-5 text-[15px] leading-relaxed text-muted-foreground">{faq.a}</AccordionContent>
              </AccordionItem>
            </RevealItem>
          ))}
        </Accordion>
      </Reveal>
    </section>
  )
}
