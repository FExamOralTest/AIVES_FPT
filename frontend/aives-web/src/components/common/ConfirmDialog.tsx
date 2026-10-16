import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'

interface ConfirmDialogProps {
  open: boolean
  title: string
  description: string
  pending: boolean
  error: string | null
  onClose: () => void
  onConfirm: () => void
}

export function ConfirmDialog({ open, title, description, pending, error, onClose, onConfirm }: ConfirmDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(value) => !value && !pending && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={onClose}>Hủy</Button>
          <Button variant="destructive" disabled={pending} onClick={onConfirm}>{pending ? 'Đang xóa…' : 'Xác nhận xóa'}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
