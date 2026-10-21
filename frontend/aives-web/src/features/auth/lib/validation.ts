const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
// Same character set as the backend's StrongPasswordAttribute.
const SPECIAL_RE = /[!@#$%^&*()_+\-=[\]{};':"\\|,.<>/?]/

export const PASSWORD_HINT = 'At least 8 characters, with an uppercase letter, a lowercase letter, a digit and a special character.'

export function validateEmail(value: string): string | undefined {
  if (!value.trim()) return 'Enter your email.'
  if (!EMAIL_RE.test(value.trim())) return 'Enter a valid email, for example name@gmail.com.'
  if (value.length > 100) return 'Email can be at most 100 characters.'
}

/** Mirrors the backend rule for new passwords (register, admin-created accounts). */
export function validatePassword(value: string): string | undefined {
  if (!value) return 'Enter a password.'
  if (value.length < 8) return 'Password must be at least 8 characters.'
  if (!/[A-Z]/.test(value)) return 'Password needs at least one uppercase letter.'
  if (!/[a-z]/.test(value)) return 'Password needs at least one lowercase letter.'
  if (!/\d/.test(value)) return 'Password needs at least one digit.'
  if (!SPECIAL_RE.test(value)) return 'Password needs at least one special character, for example @ # ! ?'
}
