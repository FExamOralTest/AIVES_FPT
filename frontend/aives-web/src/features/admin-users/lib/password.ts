const SETS = ['ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnpqrstuvwxyz', '23456789', '@#$%&*!?']

/** 12-char password satisfying the backend rule (upper, lower, digit, special). Skips look-alikes like O/0, l/1. */
export function generatePassword(length = 12): string {
  const all = SETS.join('')
  const pick = (chars: string) => chars[randomInt(chars.length)]
  const chars = [...SETS.map(pick), ...Array.from({ length: length - SETS.length }, () => pick(all))]
  // Fisher–Yates so the guaranteed characters aren't always first.
  for (let i = chars.length - 1; i > 0; i--) {
    const j = randomInt(i + 1)
    ;[chars[i], chars[j]] = [chars[j], chars[i]]
  }
  return chars.join('')
}

function randomInt(max: number): number {
  return crypto.getRandomValues(new Uint32Array(1))[0] % max
}
