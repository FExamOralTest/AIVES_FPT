import { CircleAlert, Wand2 } from 'lucide-react'
import { useId, useState, type ChangeEvent, type FormEvent } from 'react'
import { FormField } from '@/components/common/FormField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { PASSWORD_HINT, ROLE_LABEL, validateEmail, validatePassword } from '@/features/auth'
import { toApiError } from '@/lib/http/apiError'
import { cn } from '@/lib/utils'
import { usersApi } from '../api/usersApi'
import { generatePassword } from '../lib/password'
import type { UpdateUserInput, User, UserRole } from '../types'

export type UserFormMode = { type: 'create' } | { type: 'edit'; user: User }

interface UserFormDialogProps {
  mode: UserFormMode | null
  /** Signed-in admin's id: they can't change their own role (backend rejects it). */
  currentUserId: number
  onClose: () => void
  onSaved: (user: User, created: boolean) => void
}

/** Create or edit a user. Remounts per open so each form starts from fresh values. */
export function UserFormDialog({ mode, currentUserId, onClose, onSaved }: UserFormDialogProps) {
  return (
    <Dialog open={mode !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        {mode && (
          <UserForm
            key={mode.type === 'edit' ? mode.user.userId : 'create'}
            mode={mode}
            isSelf={mode.type === 'edit' && mode.user.userId === currentUserId}
            onSaved={onSaved}
          />
        )}
      </DialogContent>
    </Dialog>
  )
}

type Field = 'fullName' | 'username' | 'email' | 'password' | 'role'
type Values = Record<Exclude<Field, 'role'>, string> & { role: UserRole }

// Academic staff & examiner accounts are what admins create most; students can self-register.
const ROLE_OPTIONS: UserRole[] = ['STAFF', 'EXAMINER', 'STUDENT', 'ADMIN']

// Backend conflict messages → the field they belong to.
const CONFLICT_FIELD: Record<string, Field> = {
  'Username already exists.': 'username',
  'Email already exists.': 'email',
  'Email already in use.': 'email',
  'Cannot change your own role.': 'role',
  'Cannot demote the last admin user.': 'role',
}

function UserForm({ mode, isSelf, onSaved }: { mode: UserFormMode; isSelf: boolean; onSaved: UserFormDialogProps['onSaved'] }) {
  const isCreate = mode.type === 'create'
  const original = mode.type === 'edit' ? mode.user : null
  const roleId = useId()

  const [values, setValues] = useState<Values>({
    fullName: original?.fullName ?? '',
    username: original?.username ?? '',
    email: original?.email ?? '',
    password: '',
    role: original?.role ?? 'STAFF',
  })

  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const set = (field: Field, value: string) => {
    setValues((prev) => ({ ...prev, [field]: value }))
    if (errors[field]) setErrors((prev) => ({ ...prev, [field]: undefined }))
  }
  const bind = (field: Exclude<Field, 'role'>) => ({
    name: field,
    value: values[field],
    error: errors[field],
    onChange: (e: ChangeEvent<HTMLInputElement>) => set(field, e.target.value),
  })

  function validate(): Partial<Record<Field, string>> {
    const v = values
    const next: Partial<Record<Field, string>> = {}
    if (!v.fullName.trim()) next.fullName = 'Enter a full name.'
    else if (v.fullName.trim().length > 100) next.fullName = 'Full name can be at most 100 characters.'
    if (isCreate) {
      const username = v.username.trim()
      if (!username) next.username = 'Enter a username.'
      else if (/\s/.test(username)) next.username = 'Username cannot contain spaces.'
      else if (username.length < 3) next.username = 'Username must be at least 3 characters.'
      else if (username.length > 50) next.username = 'Username can be at most 50 characters.'
    }
    next.email = validateEmail(v.email)
    // Editing: an empty password means "keep the current one".
    if (isCreate || v.password) next.password = validatePassword(v.password)
    return Object.fromEntries(Object.entries(next).filter(([, msg]) => msg))
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const next = validate()
    setErrors(next)
    setFormError(null)
    if (Object.keys(next).length) return

    setSubmitting(true)
    try {
      if (isCreate) {
        const created = await usersApi.create({
          username: values.username.trim(),
          fullName: values.fullName.trim(),
          email: values.email.trim(),
          password: values.password,
          role: values.role,
        })
        onSaved(created, true)
      } else if (original) {
        const changes = diff(original, values)
        // Nothing changed: just close, no request.
        onSaved(Object.keys(changes).length ? await usersApi.update(original.userId, changes) : original, false)
      }
    } catch (err) {
      const apiError = toApiError(err)
      const field = apiError.rawMessage ? CONFLICT_FIELD[apiError.rawMessage] : undefined
      if (field) setErrors({ [field]: apiError.message })
      else {
        setErrors(apiError.fieldErrors)
        setFormError(apiError.message)
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="contents">
      <DialogHeader>
        <DialogTitle>{isCreate ? 'Add user' : 'Edit user'}</DialogTitle>
        <DialogDescription>
          {isCreate
            ? 'Create the account, then send the username and password to the user.'
            : `Username: ${original?.username}. Leave the password empty to keep it unchanged.`}
        </DialogDescription>
      </DialogHeader>

      {formError && (
        <Alert variant="destructive" className="rounded-[14px] border-0 bg-[#fef1f0] px-4 py-3.5">
          <CircleAlert />
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      )}

      <div className="flex flex-col gap-4">
        <FormField label="Full name" autoComplete="off" placeholder="Nguyen Van A" autoFocus {...bind('fullName')} />
        {isCreate && (
          <FormField
            label="Username"
            autoComplete="off"
            placeholder="teacher01"
            hint="One word, no spaces. Cannot be changed later."
            {...bind('username')}
          />
        )}
        <FormField label="Email" type="email" autoComplete="off" placeholder="name@aives.edu.vn" {...bind('email')} />

        <div className="flex flex-col gap-2">
          <Label htmlFor={roleId} className="text-sm font-semibold">
            Role
          </Label>
          <select
            id={roleId}
            value={values.role}
            onChange={(e) => set('role', e.target.value)}
            disabled={isSelf}
            aria-invalid={Boolean(errors.role)}
            aria-describedby={`${roleId}-note`}
            className={cn(
              'h-12 w-full rounded-[14px] border border-input bg-background px-4 text-[15px] outline-none',
              'focus-visible:border-brand focus-visible:ring-4 focus-visible:ring-brand/15 disabled:opacity-60',
              errors.role && 'border-destructive ring-3 ring-destructive/20',
            )}
          >
            {ROLE_OPTIONS.map((role) => (
              <option key={role} value={role}>
                {ROLE_LABEL[role]}
              </option>
            ))}
          </select>
          <p id={`${roleId}-note`} className={cn('text-[13px]', errors.role ? 'text-destructive' : 'text-muted-foreground')}>
            {errors.role ?? (isSelf ? 'You cannot change your own role.' : 'Decides which features the user can access.')}
          </p>
        </div>

        <div className="flex flex-col gap-1.5">
          <FormField
            label={isCreate ? 'Password' : 'New password'}
            type="password"
            autoComplete="new-password"
            hint={PASSWORD_HINT}
            {...bind('password')}
          />
          <button
            type="button"
            onClick={() => set('password', generatePassword())}
            className="inline-flex w-fit items-center gap-1.5 rounded-full text-[13px] font-semibold text-brand hover:text-brand-hover"
          >
            <Wand2 className="size-3.5" aria-hidden="true" />
            Generate a random password
          </button>
        </div>
      </div>

      <DialogFooter>
        <DialogClose asChild>
          <Button type="button" variant="secondary">
            Cancel
          </Button>
        </DialogClose>
        <Button type="submit" disabled={submitting}>
          {submitting ? 'Saving…' : isCreate ? 'Create account' : 'Save changes'}
        </Button>
      </DialogFooter>
    </form>
  )
}

function diff(original: User, v: Values): UpdateUserInput {
  const changes: UpdateUserInput = {}
  if (v.fullName.trim() !== original.fullName) changes.fullName = v.fullName.trim()
  if (v.email.trim().toLowerCase() !== (original.email ?? '').toLowerCase()) changes.email = v.email.trim()
  if (v.password) changes.password = v.password
  if (v.role !== original.role) changes.role = v.role
  return changes
}
