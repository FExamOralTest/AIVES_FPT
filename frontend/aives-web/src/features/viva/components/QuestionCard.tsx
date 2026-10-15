import { Sparkles, HelpCircle, Layers } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import type { VivaQuestionDto } from '../types'

interface QuestionCardProps {
  question: VivaQuestionDto
  questionNumber: number
  totalQuestions: number
}

const LEVEL_CONFIG: Record<number, { label: string; variant: 'default' | 'secondary' | 'outline' | 'destructive' }> = {
  1: { label: 'Cơ bản', variant: 'secondary' },
  2: { label: 'Trung bình', variant: 'default' },
  3: { label: 'Nâng cao', variant: 'destructive' },
}

export function QuestionCard({ question, questionNumber, totalQuestions }: QuestionCardProps) {
  const levelInfo = LEVEL_CONFIG[question.level] ?? { label: `Cấp độ ${question.level}`, variant: 'outline' }

  return (
    <article className="relative overflow-hidden rounded-3xl border bg-card p-6 shadow-sm transition-all sm:p-8">
      {/* Background ambient gradient glow if follow-up */}
      {question.isFollowUp && (
        <div className="absolute top-0 right-0 -mr-16 -mt-16 size-48 rounded-full bg-sun/15 blur-3xl pointer-events-none" />
      )}

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <span className="flex size-8 items-center justify-center rounded-xl bg-brand/10 text-sm font-semibold text-brand">
            {questionNumber}
          </span>
          <span className="text-sm font-medium text-muted-foreground">
            Câu hỏi {questionNumber} / {totalQuestions}
          </span>
        </div>

        <div className="flex items-center gap-2">
          {question.isFollowUp ? (
            <Badge variant="default" className="gap-1 bg-amber-500 text-white hover:bg-amber-600">
              <Sparkles className="size-3.5" />
              Câu hỏi phụ từ AI
            </Badge>
          ) : (
            <Badge variant={levelInfo.variant} className="gap-1">
              <Layers className="size-3" />
              {levelInfo.label}
            </Badge>
          )}

          {question.maxScore != null && (
            <span className="text-xs font-semibold px-2.5 py-1 rounded-full bg-muted text-muted-foreground">
              Tối đa: {question.maxScore}đ
            </span>
          )}
        </div>
      </div>

      <div className="mt-5">
        {question.isFollowUp && (
          <p className="mb-2 text-xs font-semibold tracking-wide uppercase text-amber-600 dark:text-amber-400">
            AI cần làm rõ thêm câu trả lời của bạn:
          </p>
        )}
        <h2 className="font-heading text-xl font-medium leading-relaxed tracking-tight text-foreground sm:text-2xl">
          {question.questionText}
        </h2>
      </div>

      <div className="mt-6 flex items-center gap-2 text-xs text-muted-foreground">
        <HelpCircle className="size-4 shrink-0 text-brand" />
        <span>Bật micro và phát biểu câu trả lời, hoặc nhập bằng văn bản nếu bạn gặp vấn đề âm thanh.</span>
      </div>
    </article>
  )
}
