import { useId, useState, type ComponentProps } from 'react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

interface FormFieldProps extends Omit<ComponentProps<'input'>, 'id'> {
  label: string
  error?: string
  hint?: string
}

/** shadcn Label + Input with hint/error text; password fields get a show/hide toggle. */
export function FormField({ label, error, hint, type = 'text', ...inputProps }: FormFieldProps) {
  const id = useId()
  const [revealed, setRevealed] = useState(false)
  const isPassword = type === 'password'
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined

  return (
    <div className="flex min-w-0 flex-col gap-2">
      <Label htmlFor={id} className="text-sm font-semibold">
        {label}
      </Label>
      <div className="relative">
        <Input
          id={id}
          type={isPassword && revealed ? 'text' : type}
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
            {revealed ? 'Ẩn' : 'Hiện'}
          </button>
        )}
      </div>
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
