// Backend codes are "ESS-" + 8 uppercase hex chars, matched exactly (case-sensitive) in the database.
export const SESSION_CODE_PREFIX = 'ESS-'
export const SESSION_CODE_LENGTH = 8

/**
 * Turns whatever the student typed or pasted ("ess-1a2b 3c4d", "ESS-1A2B3C4D") into the 8-char body.
 * Non-hex characters are dropped so a stray space or dash can't cause a "not found".
 */
export function normalizeCodeBody(raw: string): string {
  const upper = raw.toUpperCase().replace(/\s+/g, '')
  // "S" isn't hex, so "ES…" means the prefix is being typed or pasted: drop it. A lone "E" stays,
  // since a valid code body can start with E.
  const body = upper.replace(/^ESS?-?/, '')
  return body.replace(/[^0-9A-F]/g, '').slice(0, SESSION_CODE_LENGTH)
}

export function toSessionCode(body: string): string {
  return SESSION_CODE_PREFIX + body
}

export function validateCodeBody(body: string): string | undefined {
  if (!body) return 'Enter the join code.'
  if (body.length < SESSION_CODE_LENGTH) return `The join code has ${SESSION_CODE_LENGTH} characters after "ESS-".`
}
