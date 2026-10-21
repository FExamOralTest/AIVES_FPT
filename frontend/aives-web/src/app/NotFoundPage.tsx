import { Link } from 'react-router-dom'
import { LogoMark } from '@/components/common/Logo'
import { Button } from '@/components/ui/button'

export default function NotFoundPage() {
  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-5 px-4 text-center">
      <LogoMark className="size-12" />
      <h1 className="font-heading text-3xl font-medium">Page not found</h1>
      <p className="text-muted-foreground">The link may have changed or no longer exists.</p>
      <Button asChild>
        <Link to="/">Back to home</Link>
      </Button>
    </main>
  )
}
