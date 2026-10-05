import { CircleAlert } from 'lucide-react'
import { useState, type ChangeEvent, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { toApiError } from '@/lib/http/apiError'
import { authApi } from '../api/authApi'
import { AuthLayout } from '../components/AuthLayout'
import { RegisterPanel } from '../components/AuthPanels'
import { FormField } from '../components/FormField'
import { SubmitButton } from '../components/SubmitButton'
import { validateEmail, validatePassword } from '../lib/validation'

type Field = 'username' | 'fullName' | 'email' | 'password' | 'confirmPassword'
type Values = Record<Field, string>

const EMPTY: Values = { username: '', fullName: '', email: '', password: '', confirmPassword: '' }

function validate(v: Values): Partial<Record<Field, string>> {
  const errors: Partial<Record<Field, string>> = {}

  if (!v.username.trim()) errors.username = 'Nhập tên đăng nhập.'
  else if (/\s/.test(v.username.trim())) errors.username = 'Tên đăng nhập không chứa khoảng trắng.'
  else if (v.username.trim().length > 50) errors.username = 'Tên đăng nhập tối đa 50 ký tự.'

  if (!v.fullName.trim()) errors.fullName = 'Nhập họ và tên.'
  else if (v.fullName.trim().length > 100) errors.fullName = 'Họ và tên tối đa 100 ký tự.'

  errors.email = validateEmail(v.email)
  errors.password = validatePassword(v.password)

  if (!v.confirmPassword) errors.confirmPassword = 'Nhập lại mật khẩu.'
  else if (v.confirmPassword !== v.password) errors.confirmPassword = 'Mật khẩu nhập lại không khớp.'

  return Object.fromEntries(Object.entries(errors).filter(([, msg]) => msg)) as Partial<Record<Field, string>>
}

export default function RegisterPage() {
  const navigate = useNavigate()
  const [values, setValues] = useState<Values>(EMPTY)
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const bind = (field: Field) => ({
    name: field,
    value: values[field],
    error: errors[field],
    onChange: (e: ChangeEvent<HTMLInputElement>) => {
      setValues((prev) => ({ ...prev, [field]: e.target.value }))
      if (errors[field]) setErrors((prev) => ({ ...prev, [field]: undefined }))
    },
  })

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const next = validate(values)
    setErrors(next)
    setFormError(null)
    if (Object.keys(next).length) return

    setSubmitting(true)
    try {
      const user = await authApi.register({
        username: values.username.trim(),
        fullName: values.fullName.trim(),
        email: values.email.trim(),
        password: values.password,
      })
      navigate('/login', { replace: true, state: { registeredEmail: user.email ?? values.email.trim() } })
    } catch (err) {
      const apiError = toApiError(err)
      const fieldErrors: Partial<Record<Field, string>> = { ...apiError.fieldErrors }
      // A 409 names the conflicting field; show it there instead of in a banner.
      if (apiError.status === 409 && apiError.message.includes('Tên đăng nhập')) fieldErrors.username = apiError.message
      else if (apiError.status === 409 && apiError.message.includes('Email')) fieldErrors.email = apiError.message
      else setFormError(apiError.message)
      setErrors(fieldErrors)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthLayout aside={<RegisterPanel />}>
      <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-[26px]">
        <div className="flex flex-col gap-2.5">
          <h1 className="font-heading text-[40px] leading-[1.1] font-medium tracking-tight">Tạo tài khoản sinh viên</h1>
          <p className="text-[15px] leading-relaxed text-muted-foreground">
            Tài khoản giảng viên và quản trị viên do nhà trường cấp, không đăng ký tại đây.
          </p>
        </div>

        {formError && (
          <Alert variant="destructive" className="rounded-[14px] border-0 bg-[#fef1f0] px-4 py-3.5">
            <CircleAlert />
            <AlertDescription>{formError}</AlertDescription>
          </Alert>
        )}

        <div className="flex flex-col gap-4">
          <FormField label="Họ và tên" autoComplete="name" placeholder="Nguyễn Văn A" autoFocus {...bind('fullName')} />
          <FormField
            label="Tên đăng nhập"
            autoComplete="username"
            placeholder="student01"
            hint="Viết liền, không dấu cách."
            {...bind('username')}
          />
          <FormField label="Email" type="email" autoComplete="email" placeholder="ten@gmail.com" {...bind('email')} />
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField
              label="Mật khẩu"
              type="password"
              autoComplete="new-password"
              hint="Ít nhất 6 ký tự."
              {...bind('password')}
            />
            <FormField label="Nhập lại mật khẩu" type="password" autoComplete="new-password" {...bind('confirmPassword')} />
          </div>
        </div>

        <SubmitButton pending={submitting}>{submitting ? 'Đang tạo tài khoản…' : 'Tạo tài khoản'}</SubmitButton>

        <p className="text-sm text-muted-foreground">
          Đã có tài khoản?{' '}
          <Link to="/login" className="font-semibold text-brand hover:text-brand-hover">
            Đăng nhập
          </Link>
        </p>
      </form>
    </AuthLayout>
  )
}
