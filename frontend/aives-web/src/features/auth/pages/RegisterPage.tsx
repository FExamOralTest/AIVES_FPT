import { CircleAlert } from 'lucide-react'
import { useState, type ChangeEvent, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { toApiError } from '@/lib/http/apiError'
import { authApi } from '../api/authApi'
import { AuthLayout } from '../components/AuthLayout'
import { RegisterPanel } from '../components/AuthPanels'
import { FormField } from '@/components/common/FormField'
import { SubmitButton } from '../components/SubmitButton'
import { PASSWORD_HINT, validateEmail, validatePassword } from '../lib/validation'

type Field = 'username' | 'fullName' | 'email' | 'password' | 'confirmPassword'
type Values = Record<Field, string>

const EMPTY: Values = { username: '', fullName: '', email: '', password: '', confirmPassword: '' }

function validate(v: Values): Partial<Record<Field, string>> {
  const errors: Partial<Record<Field, string>> = {}

  if (!v.username.trim()) errors.username = 'Enter a username.'
  else if (/\s/.test(v.username.trim())) errors.username = 'Username cannot contain spaces.'
  else if (v.username.trim().length < 3) errors.username = 'Username must be at least 3 characters.'
  else if (v.username.trim().length > 50) errors.username = 'Username can be at most 50 characters.'

  if (!v.fullName.trim()) errors.fullName = 'Enter your full name.'
  else if (v.fullName.trim().length > 100) errors.fullName = 'Full name can be at most 100 characters.'

  errors.email = validateEmail(v.email)
  errors.password = validatePassword(v.password)

  if (!v.confirmPassword) errors.confirmPassword = 'Re-enter your password.'
  else if (v.confirmPassword !== v.password) errors.confirmPassword = 'Passwords do not match.'

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
      if (apiError.rawMessage === 'Username already exists.') fieldErrors.username = apiError.message
      else if (apiError.rawMessage === 'Email already exists.') fieldErrors.email = apiError.message
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
          <h1 className="font-heading text-[40px] leading-[1.1] font-medium tracking-tight">Create a student account</h1>
          <p className="text-[15px] leading-relaxed text-muted-foreground">
            Staff, examiner and admin accounts are issued by the school and cannot be registered here.
          </p>
        </div>

        {formError && (
          <Alert variant="destructive" className="rounded-[14px] border-0 bg-[#fef1f0] px-4 py-3.5">
            <CircleAlert />
            <AlertDescription>{formError}</AlertDescription>
          </Alert>
        )}

        <div className="flex flex-col gap-4">
          <FormField label="Full name" autoComplete="name" placeholder="Nguyen Van A" autoFocus {...bind('fullName')} />
          <FormField
            label="Username"
            autoComplete="username"
            placeholder="student01"
            hint="One word, no spaces."
            {...bind('username')}
          />
          <FormField label="Email" type="email" autoComplete="email" placeholder="name@gmail.com" {...bind('email')} />
          <FormField
            label="Password"
            type="password"
            autoComplete="new-password"
            hint={PASSWORD_HINT}
            {...bind('password')}
          />
          <FormField label="Confirm password" type="password" autoComplete="new-password" {...bind('confirmPassword')} />
        </div>

        <SubmitButton pending={submitting}>{submitting ? 'Creating account…' : 'Create account'}</SubmitButton>

        <p className="text-sm text-muted-foreground">
          Already have an account?{' '}
          <Link to="/login" className="font-semibold text-brand hover:text-brand-hover">
            Sign in
          </Link>
        </p>
      </form>
    </AuthLayout>
  )
}
