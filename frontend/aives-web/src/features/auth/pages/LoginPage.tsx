import { CircleAlert } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { toApiError } from '@/lib/http/apiError'
import { AuthLayout } from '../components/AuthLayout'
import { LoginPanel } from '../components/AuthPanels'
import { FormField } from '../components/FormField'
import { SubmitButton } from '../components/SubmitButton'
import { useAuth } from '../hooks/useAuth'
import { ROLE_HOME } from '../lib/roles'
import { validateEmail } from '../lib/validation'

interface LoginLocationState {
  from?: string
  registeredEmail?: string
}

export default function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const state = (location.state ?? {}) as LoginLocationState

  const [email, setEmail] = useState(state.registeredEmail ?? '')
  const [password, setPassword] = useState('')
  const [errors, setErrors] = useState<{ email?: string; password?: string }>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const next = {
      email: validateEmail(email),
      password: password ? undefined : 'Nhập mật khẩu.',
    }
    setErrors(next)
    setFormError(null)
    if (next.email || next.password) return

    setSubmitting(true)
    try {
      const user = await login({ email: email.trim(), password })
      navigate(state.from ?? ROLE_HOME[user.role], { replace: true })
    } catch (err) {
      const apiError = toApiError(err)
      setFormError(apiError.status === 401 ? 'Email hoặc mật khẩu không đúng.' : apiError.message)
      setErrors(apiError.fieldErrors)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthLayout aside={<LoginPanel />}>
      <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-7">
        <div className="flex flex-col gap-2.5">
          <h1 className="font-heading text-[40px] leading-[1.1] font-medium tracking-tight">Chào mừng trở lại</h1>
          <p className="text-[15px] text-muted-foreground">Đăng nhập bằng email và mật khẩu của tài khoản AIVES.</p>
        </div>

        {state.registeredEmail && !formError && (
          <Alert className="rounded-[14px] border-0 bg-brand-soft px-4 py-3.5 text-foreground">
            <AlertDescription className="text-foreground">Đã tạo tài khoản. Nhập mật khẩu để đăng nhập.</AlertDescription>
          </Alert>
        )}
        {formError && (
          <Alert variant="destructive" className="rounded-[14px] border-0 bg-[#fef1f0] px-4 py-3.5">
            <CircleAlert />
            <AlertDescription>{formError}</AlertDescription>
          </Alert>
        )}

        <div className="flex flex-col gap-[18px]">
          <FormField
            label="Email"
            type="email"
            name="email"
            autoComplete="email"
            placeholder="ten@gmail.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            error={errors.email}
            autoFocus={!state.registeredEmail}
          />
          <FormField
            label="Mật khẩu"
            type="password"
            name="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={errors.password}
            autoFocus={Boolean(state.registeredEmail)}
          />
        </div>

        <SubmitButton pending={submitting}>{submitting ? 'Đang đăng nhập…' : 'Đăng nhập'}</SubmitButton>

        <p className="text-sm text-muted-foreground">
          Chưa có tài khoản?{' '}
          <Link to="/register" className="font-semibold text-brand hover:text-brand-hover">
            Tạo tài khoản sinh viên
          </Link>
        </p>
      </form>
    </AuthLayout>
  )
}
