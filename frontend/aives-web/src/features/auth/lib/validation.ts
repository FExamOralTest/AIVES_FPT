const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
// Same character set as the backend's StrongPasswordAttribute.
const SPECIAL_RE = /[!@#$%^&*()_+\-=[\]{};':"\\|,.<>/?]/

export const PASSWORD_HINT = 'Ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.'

export function validateEmail(value: string): string | undefined {
  if (!value.trim()) return 'Nhập email.'
  if (!EMAIL_RE.test(value.trim())) return 'Email chưa đúng định dạng, ví dụ: ten@gmail.com.'
  if (value.length > 100) return 'Email tối đa 100 ký tự.'
}

/** Mirrors the backend rule for new passwords (register, admin-created accounts). */
export function validatePassword(value: string): string | undefined {
  if (!value) return 'Nhập mật khẩu.'
  if (value.length < 8) return 'Mật khẩu cần ít nhất 8 ký tự.'
  if (!/[A-Z]/.test(value)) return 'Mật khẩu cần ít nhất một chữ hoa.'
  if (!/[a-z]/.test(value)) return 'Mật khẩu cần ít nhất một chữ thường.'
  if (!/\d/.test(value)) return 'Mật khẩu cần ít nhất một chữ số.'
  if (!SPECIAL_RE.test(value)) return 'Mật khẩu cần ít nhất một ký tự đặc biệt, ví dụ: @ # ! ?'
}
