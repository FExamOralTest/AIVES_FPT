import { LogoMark } from '@/components/common/Logo'

export function SiteFooter() {
  return (
    <footer className="bg-primary px-4 py-12 text-[#c7c9d1] sm:px-6">
      <div className="mx-auto flex max-w-[1200px] flex-wrap items-center justify-between gap-5 text-sm">
        <span className="inline-flex items-center gap-2.5 font-heading text-lg font-semibold tracking-tight text-white">
          <LogoMark />
          aives
        </span>
        <span>Hệ thống thi vấn đáp với AI. Dữ liệu cá nhân xử lý theo Nghị định 13/2023/NĐ-CP.</span>
      </div>
    </footer>
  )
}
