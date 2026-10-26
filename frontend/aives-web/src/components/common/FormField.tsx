import { useId, useState, type ComponentProps, type ReactNode } from 'react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

interface FormFieldProps extends Omit<ComponentProps<'input'>, 'id'> {
  label: string
  error?: string
  hint?: string
  children?: ReactNode
}

/** shadcn Label + Input with hint/error text; password fields get a show/hide toggle. Supports children for custom form controls. */
export function FormField({ label, error, hint, type = 'text', children, required, ...inputProps }: FormFieldProps) {
  const id = useId()
  const [revealed, setRevealed] = useState(false)
  const isPassword = type === 'password'
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined

  return (
    <div className="flex min-w-0 flex-col gap-2">
      <Label htmlFor={id} className="text-sm font-semibold">
        {label}
        {required && <span className="ml-1 text-destructive">*</span>}
      </Label>
      {children ? (
        children
      ) : (
        <div className="relative">
          <Input
            id={id}
            type={isPassword && revealed ? 'text' : type}
            required={required}
            aria-invalid={Boolean(error)}
            aria-describedby={describedBy}
            className={isPassword ? 'pr-20' : undefined}
            {...inputProps}
          />
          {isPassword && (
            <button
              type="button"
              onClick={() => setRevealed((v) => !v)}
              aria-pressed={revealed}
              className="absolute top-1/2 right-1.5 h-9 -translate-y-1/2 rounded-full bg-muted px-3 text-[13px] font-semibold text-foreground/75 hover:bg-[#eaebef] hover:text-foreground"
            >
              {revealed ? 'Hide' : 'Show'}
            </button>
          )}
        </div>
      )}
      {error ? (
        <p id={`${id}-error`} className="text-[13px] text-destructive">
          {error}
        </p>
      ) : hint ? (
        <p id={`${id}-hint`} className="text-[13px] text-muted-foreground">
          {hint}
        </p>
      ) : null}
    </div>
  )
}
