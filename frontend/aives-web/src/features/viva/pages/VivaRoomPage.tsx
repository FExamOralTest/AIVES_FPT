import {
  AlertCircle,
  ArrowRight,
  CheckCircle2,
  Clock,
  Mic,
  MicOff,
  RotateCcw,
  Send,
  Sparkles,
  Volume2,
} from 'lucide-react'
import { useEffect, useId, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { toApiError } from '@/lib/http/apiError'
import { vivaApi } from '../api/vivaApi'
import { QuestionCard } from '../components/QuestionCard'
import { VivaWaveform } from '../components/VivaWaveform'
import { useAudioRecorder } from '../hooks/useAudioRecorder'
import type { StartVivaSessionResponse, VivaQuestionDto } from '../types'

export default function VivaRoomPage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const sessionParam = searchParams.get('sessionId')

  const [inputSessionId, setInputSessionId] = useState(sessionParam ?? '')
  const [session, setSession] = useState<StartVivaSessionResponse | null>(null)
  const [currentQuestion, setCurrentQuestion] = useState<VivaQuestionDto | null>(null)
  const [textAnswer, setTextAnswer] = useState('')
  const [transcript, setTranscript] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isFinishing, setIsFinishing] = useState(false)
  const [isCompleted, setIsCompleted] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [followUpAlert, setFollowUpAlert] = useState<string | null>(null)

  const recorder = useAudioRecorder()
  const textInputId = useId()

  // Format recording seconds MM:SS
  const formatTime = (secs: number) => {
    const mins = Math.floor(secs / 60)
    const rem = secs % 60
    return `${mins.toString().padStart(2, '0')}:${rem.toString().padStart(2, '0')}`
  }

  // Auto-start if sessionId provided in URL query
  useEffect(() => {
    if (sessionParam && !session) {
      handleStartSession(Number(sessionParam))
    }
  }, [sessionParam])

  async function handleStartSession(id: number) {
    if (!id || isNaN(id)) {
      setErrorMessage('Vui lòng nhập ID ca thi hợp lệ.')
      return
    }
    setIsLoading(true)
    setErrorMessage(null)
    try {
      const data = await vivaApi.start(id)
      setSession(data)
      setCurrentQuestion(data.currentQuestion)
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
    } finally {
      setIsLoading(false)
    }
  }

  async function handleSubmitAnswer() {
    if (!session || !currentQuestion) return

    const hasAudio = Boolean(recorder.audioBlob)
    const hasText = Boolean(textAnswer.trim())

    if (!hasAudio && !hasText) {
      setErrorMessage('Vui lòng ghi âm câu trả lời hoặc nhập văn bản trước khi gửi.')
      return
    }

    setIsSubmitting(true)
    setErrorMessage(null)
    setFollowUpAlert(null)

    try {
      const formData = new FormData()
      formData.append('questionId', currentQuestion.questionId.toString())
      formData.append('isFollowUp', currentQuestion.isFollowUp.toString())

      if (recorder.audioBlob) {
        formData.append('audioFile', recorder.audioBlob, 'answer.wav')
      }
      if (hasText) {
        formData.append('textAnswer', textAnswer.trim())
      }

      const res = await vivaApi.submitAnswer(session.sessionId, formData)

      if (res.transcript) {
        setTranscript(res.transcript)
      }

      // Reset input state for next interaction
      recorder.resetRecording()
      setTextAnswer('')

      if (res.shouldFollowUp && res.nextQuestion) {
        setFollowUpAlert('AI có một câu hỏi phụ để làm rõ nội dung bạn vừa trả lời!')
        setCurrentQuestion(res.nextQuestion)
      } else if (res.isCompleted || !res.nextQuestion) {
        setIsCompleted(true)
      } else {
        setCurrentQuestion(res.nextQuestion)
      }
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleFinishExam() {
    if (!session) return
    setIsFinishing(true)
    setErrorMessage(null)
    try {
      await vivaApi.finish(session.sessionId)
      setIsCompleted(true)
    } catch (err) {
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
    } finally {
      setIsFinishing(false)
    }
  }

  // 1. Initial Screen: Enter session ID if not started
  if (!session) {
    return (
      <div className="mx-auto max-w-xl py-6">
        <header className="text-center sm:text-left">
          <h1 className="font-heading text-3xl font-medium tracking-tight">Phòng thi vấn đáp AI</h1>
          <p className="mt-2 text-muted-foreground">
            Vào phòng thi vấn đáp trực tuyến bằng giọng nói. Chuẩn bị micro và không gian yên tĩnh.
          </p>
        </header>

        <div className="mt-8 rounded-3xl border bg-card p-6 shadow-sm sm:p-8">
          {errorMessage && (
            <Alert variant="destructive" className="mb-6 rounded-2xl">
              <AlertCircle className="size-4" />
              <AlertDescription>{errorMessage}</AlertDescription>
            </Alert>
          )}

          <div className="flex flex-col gap-4">
            <label htmlFor="session-id-input" className="text-sm font-semibold">
              Mã số ca thi (Session ID)
            </label>
            <div className="flex gap-3">
              <input
                id="session-id-input"
                type="number"
                value={inputSessionId}
                onChange={(e) => setInputSessionId(e.target.value)}
                placeholder="Ví dụ: 1"
                className="h-12 w-full rounded-xl border border-input bg-background px-4 font-mono text-base outline-none focus:border-brand focus:ring-4 focus:ring-brand/15"
              />
              <Button
                onClick={() => handleStartSession(Number(inputSessionId))}
                disabled={isLoading || !inputSessionId}
                className="h-12 px-6 shrink-0"
              >
                {isLoading ? 'Đang vào…' : 'Vào phòng thi'}
              </Button>
            </div>
          </div>

          <div className="mt-6 flex items-center justify-between border-t pt-5 text-sm text-muted-foreground">
            <span>Chưa có mã ca thi?</span>
            <Link to="/student/enroll" className="font-semibold text-brand hover:text-brand-hover">
              Đăng ký tham gia ca thi ngay
            </Link>
          </div>
        </div>
      </div>
    )
  }

  // 2. Exam Finished View
  if (isCompleted) {
    return (
      <div className="mx-auto max-w-2xl py-8 text-center">
        <div className="mx-auto flex size-20 items-center justify-center rounded-full bg-emerald-100 text-emerald-600 dark:bg-emerald-950/60 dark:text-emerald-400">
          <CheckCircle2 className="size-10" />
        </div>

        <h1 className="mt-6 font-heading text-3xl font-medium tracking-tight sm:text-4xl">
          Hoàn thành bài thi vấn đáp!
        </h1>
        <p className="mt-3 text-lg text-muted-foreground">
          Bạn đã hoàn thành các câu hỏi trong ca thi <span className="font-semibold text-foreground">{session.sessionName}</span>.
        </p>

        <div className="mt-8 rounded-3xl border bg-card p-6 text-left shadow-sm sm:p-8">
          <h2 className="font-heading text-lg font-medium">Trạng thái chấm thi</h2>
          <p className="mt-2 text-sm text-muted-foreground">
            AI đã hoàn thành đánh giá sơ bộ toàn bộ câu trả lời của bạn. Bài thi đang được gửi tới giảng viên phụ trách để duyệt và công bố điểm chính thức.
          </p>

          <div className="mt-6 flex flex-col gap-3 sm:flex-row">
            <Button asChild className="flex-1">
              <Link to={`/student/results?sessionId=${session.sessionId}`}>
                Xem kết quả thi
                <ArrowRight className="size-4" />
              </Link>
            </Button>
            <Button variant="secondary" onClick={() => navigate('/student')}>
              Về trang chủ
            </Button>
          </div>
        </div>
      </div>
    )
  }

  // 3. Live Examination Room View
  const progressPercent = session.totalQuestions > 0
    ? Math.round((((currentQuestion?.questionIndex ?? 0) + 1) / session.totalQuestions) * 100)
    : 0

  return (
    <div className="mx-auto max-w-3xl py-4 sm:py-6">
      {/* Header bar: Info & Timer */}
      <header className="flex flex-wrap items-center justify-between gap-4 rounded-3xl border bg-card/60 p-4 backdrop-blur-md sm:p-6">
        <div>
          <span className="text-xs font-semibold uppercase tracking-wider text-brand">
            {session.examTitle}
          </span>
          <h1 className="mt-0.5 font-heading text-xl font-medium text-foreground sm:text-2xl">
            {session.sessionName}
          </h1>
        </div>

        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2 rounded-2xl bg-muted px-4 py-2 text-sm font-medium">
            <Clock className="size-4 text-brand" />
            <span>Thời lượng: {session.durationMinutes} phút</span>
          </div>

          <Button
            variant="destructive"
            size="sm"
            onClick={handleFinishExam}
            disabled={isFinishing}
            className="rounded-xl font-semibold"
          >
            {isFinishing ? 'Đang nộp…' : 'Nộp bài sớm'}
          </Button>
        </div>
      </header>

      {/* Progress Bar */}
      <div className="mt-4 flex items-center gap-3 px-1">
        <div className="h-2 flex-1 overflow-hidden rounded-full bg-muted">
          <div
            className="h-full rounded-full bg-gradient-to-r from-brand to-logo transition-all duration-300"
            style={{ width: `${progressPercent}%` }}
          />
        </div>
        <span className="text-xs font-semibold text-muted-foreground">
          {progressPercent}%
        </span>
      </div>

      {/* Follow-up alert badge */}
      {followUpAlert && (
        <Alert className="mt-4 border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-900/60 dark:bg-amber-950/40 dark:text-amber-200">
          <Sparkles className="size-4 text-amber-600" />
          <AlertDescription className="font-medium">{followUpAlert}</AlertDescription>
        </Alert>
      )}

      {/* Error message */}
      {errorMessage && (
        <Alert variant="destructive" className="mt-4">
          <AlertCircle className="size-4" />
          <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
      )}

      {/* Question Card */}
      {currentQuestion && (
        <div className="mt-6">
          <QuestionCard
            question={currentQuestion}
            questionNumber={(currentQuestion.questionIndex ?? 0) + 1}
            totalQuestions={session.totalQuestions}
          />
        </div>
      )}

      {/* Voice Recorder & Answering Interface */}
      <section aria-labelledby="answer-heading" className="mt-6 rounded-3xl border bg-card p-6 shadow-sm sm:p-8">
        <div className="flex items-center justify-between">
          <h2 id="answer-heading" className="font-heading text-lg font-medium">
            Trả lời câu hỏi
          </h2>
          {recorder.isRecording && (
            <span className="flex items-center gap-2 text-sm font-semibold text-destructive animate-pulse">
              <span className="size-2 rounded-full bg-destructive" />
              Đang ghi âm ({formatTime(recorder.recordingTime)})
            </span>
          )}
        </div>

        {/* Audio Visualizer Waveform */}
        <div className="mt-5">
          <VivaWaveform
            isRecording={recorder.isRecording}
            volumeLevel={recorder.volumeLevel}
          />
        </div>

        {/* Recording Controls */}
        <div className="mt-6 flex flex-wrap items-center justify-center gap-4">
          {!recorder.isRecording ? (
            <Button
              type="button"
              size="lg"
              onClick={recorder.startRecording}
              className="gap-2.5 rounded-full px-6 bg-brand hover:bg-brand-hover shadow-md hover:shadow-lg transition-all"
            >
              <Mic className="size-5" />
              Bắt đầu ghi âm câu trả lời
            </Button>
          ) : (
            <Button
              type="button"
              variant="destructive"
              size="lg"
              onClick={recorder.stopRecording}
              className="gap-2.5 rounded-full px-6 shadow-md transition-all"
            >
              <MicOff className="size-5" />
              Dừng ghi âm ({formatTime(recorder.recordingTime)})
            </Button>
          )}

          {recorder.audioUrl && !recorder.isRecording && (
            <div className="flex items-center gap-3">
              <audio controls src={recorder.audioUrl} className="h-10 max-w-xs" />
              <Button
                type="button"
                variant="outline"
                size="icon"
                onClick={recorder.resetRecording}
                title="Ghi âm lại"
                className="rounded-full"
              >
                <RotateCcw className="size-4" />
              </Button>
            </div>
          )}
        </div>

        {/* Fallback Text Input */}
        <div className="mt-6 border-t pt-5">
          <label htmlFor={textInputId} className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider">
            Hoặc nhập câu trả lời bằng văn bản (dự phòng)
          </label>
          <textarea
            id={textInputId}
            rows={3}
            value={textAnswer}
            onChange={(e) => setTextAnswer(e.target.value)}
            placeholder="Nếu gặp sự cố về micro, bạn có thể gõ nội dung câu trả lời tại đây..."
            className="mt-2 w-full rounded-2xl border border-input bg-background p-4 text-sm outline-none transition-colors focus:border-brand focus:ring-4 focus:ring-brand/15 resize-none"
          />
        </div>

        {/* Last Transcript Preview if available */}
        {transcript && (
          <div className="mt-4 rounded-2xl bg-muted/70 p-4 text-xs">
            <span className="font-semibold text-muted-foreground flex items-center gap-1.5 mb-1">
              <Volume2 className="size-3.5 text-brand" />
              Văn bản nhận diện gần nhất từ giọng nói:
            </span>
            <p className="text-foreground italic">"{transcript}"</p>
          </div>
        )}

        {/* Submit action */}
        <div className="mt-6 flex justify-end">
          <Button
            size="lg"
            onClick={handleSubmitAnswer}
            disabled={isSubmitting || recorder.isRecording || (!recorder.audioBlob && !textAnswer.trim())}
            className="gap-2 rounded-xl px-7"
          >
            {isSubmitting ? (
              'Đang xử lý câu trả lời…'
            ) : (
              <>
                <Send className="size-4" />
                Gửi câu trả lời
              </>
            )}
          </Button>
        </div>
      </section>
    </div>
  )
}
