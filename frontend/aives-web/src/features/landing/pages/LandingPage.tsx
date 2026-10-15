import { CtaSection } from '../components/CtaSection'
import { FactsStrip } from '../components/FactsStrip'
import { FaqSection } from '../components/FaqSection'
import { FeaturesSection } from '../components/FeaturesSection'
import { HeroSection } from '../components/HeroSection'
import { HowItWorksSection } from '../components/HowItWorksSection'
import { RolesSection } from '../components/RolesSection'
import { SiteFooter } from '../components/SiteFooter'
import { SiteHeader } from '../components/SiteHeader'

export default function LandingPage() {
  return (
    <>
      <SiteHeader />
      <main>
        <HeroSection />
        <FactsStrip />
        <HowItWorksSection />
        <FeaturesSection />
        <RolesSection />
        <FaqSection />
        <CtaSection />
      </main>
      <SiteFooter />
    </>
  )
}
