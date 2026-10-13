import type { RoleKey } from '../content'

/** Small flat illustrations for the role cards, drawn in the brand palette. */
export function RoleIllustration({ role }: { role: RoleKey }) {
  if (role === 'student') {
    return (
      <svg viewBox="0 0 200 120" className="h-[120px] w-[200px]" fill="none" aria-hidden="true">
        <circle cx="100" cy="60" r="44" fill="#fff" fillOpacity="0.16" />
        <rect x="88" y="26" width="24" height="44" rx="12" fill="#fff" />
        <path d="M76 58c0 14 11 24 24 24s24-10 24-24" stroke="#fff" strokeWidth="5" strokeLinecap="round" />
        <path d="M100 82v14M88 96h24" stroke="#fff" strokeWidth="5" strokeLinecap="round" />
        <path
          d="M50 46c-6 9-6 21 0 30M38 38c-11 14-11 32 0 46M150 46c6 9 6 21 0 30M162 38c11 14 11 32 0 46"
          stroke="var(--sun)"
          strokeWidth="4"
          strokeLinecap="round"
        />
      </svg>
    )
  }

  if (role === 'teacher') {
    return (
      <svg viewBox="0 0 200 120" className="h-[120px] w-[200px]" fill="none" aria-hidden="true">
        <rect x="62" y="14" width="76" height="96" rx="12" fill="#fff" />
        <rect x="84" y="8" width="32" height="14" rx="7" fill="#111114" />
        <path d="M76 44l6 6 10-12M76 70l6 6 10-12" stroke="var(--brand)" strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" />
        <path d="M100 46h26M100 72h20M100 94h14" stroke="#d5d7de" strokeWidth="5" strokeLinecap="round" />
        <circle cx="146" cy="88" r="20" fill="var(--sun)" />
        <path d="M138 88l6 6 10-12" stroke="#111114" strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    )
  }

  return (
    <svg viewBox="0 0 200 120" className="h-[120px] w-[200px]" fill="none" aria-hidden="true">
      <rect x="56" y="18" width="40" height="40" rx="10" fill="var(--brand)" />
      <rect x="104" y="18" width="40" height="40" rx="10" fill="#fff" fillOpacity="0.16" />
      <rect x="56" y="66" width="40" height="40" rx="10" fill="#fff" fillOpacity="0.16" />
      <rect x="104" y="66" width="40" height="40" rx="10" fill="var(--sun)" />
      <path d="M68 38h16M116 38h16M68 86h16" stroke="#fff" strokeWidth="4" strokeLinecap="round" />
      <path d="M124 76v20M114 86h20" stroke="#111114" strokeWidth="4" strokeLinecap="round" />
    </svg>
  )
}
