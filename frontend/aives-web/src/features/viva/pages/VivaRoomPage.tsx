import {
  AlertCircle,
  ArrowRight,
  CheckCircle2,
  Clock,
  Loader2,
  Mic,
  RotateCcw,
  Send,
  Sparkles,
  Square,
  Volume2,
  X,
} from 'lucide-react'
import { useEffect, useId, useRef, useState } from 'react'
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
  const prevIsRecordingRef = useRef(recorder.isRecording)

  // Format recording seconds MM:SS
  const formatTime = (secs: number) => {
    const mins = Math.floor(secs / 60)
    const rem = secs % 60
    return `${mins.toString().padStart(2, '0')}:${rem.toString().padStart(2, '0')}`
  }

  // Auto-fill textAnswer in real time as speech recognition transcribes words
  useEffect(() => {
    if (recorder.transcript && recorder.transcript.trim()) {
      setTextAnswer(recorder.transcript.trim())
    }
  }, [recorder.transcript])

  // When recording transitions from active to stopped, guarantee textAnswer is filled
  useEffect(() => {
    if (prevIsRecordingRef.current && !recorder.isRecording) {
      const text = recorder.transcript || recorder.interimTranscript
      if (text && text.trim()) {
        setTextAnswer(text.trim())
      }
    }
    prevIsRecordingRef.current = recorder.isRecording
  }, [recorder.isRecording, recorder.transcript, recorder.interimTranscript])

  // Stop recording and immediately populate textAnswer
  const handleStopRecording = () => {
    const textSnapshot = recorder.stopRecording()
    const textToFill = textSnapshot || recorder.transcript || recorder.interimTranscript
    if (textToFill && textToFill.trim()) {
      setTextAnswer(textToFill.trim())
    }
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

      // Reset input state for next question
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

  const hasRecordedAudio = Boolean(recorder.audioBlob && recorder.audioUrl)

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
            className="rounded-xl px-4"
          >
            {isFinishing ? 'Đang nộp…' : 'Nộp bài sớm'}
          </Button>
        </div>
      </header>

      {/* Progress Bar */}
      <div className="mt-4 flex items-center gap-3">
        <div className="relative h-2.5 w-full overflow-hidden rounded-full bg-muted">
          <div
            className="h-full bg-brand transition-all duration-300"
            style={{ width: `${progressPercent}%` }}
          />
        </div>
        <span className="text-xs font-semibold text-muted-foreground tabular-nums">
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

          {/* Active status indicator */}
          {recorder.isRecording && (
            <span className="flex items-center gap-2 rounded-full border border-destructive/30 bg-destructive/10 px-3.5 py-1 text-xs font-semibold text-destructive animate-pulse">
              <span className="size-2 rounded-full bg-destructive animate-ping" />
              Đang ghi âm ({formatTime(recorder.recordingTime)})
            </span>
          )}

          {!recorder.isRecording && hasRecordedAudio && (
            <span className="flex items-center gap-1.5 rounded-full border border-emerald-500/30 bg-emerald-500/10 px-3 py-1 text-xs font-semibold text-emerald-600 dark:text-emerald-400">
              <CheckCircle2 className="size-3.5" />
              Đã ghi âm xong ({formatTime(recorder.recordingTime)})
            </span>
          )}
        </div>

        {/* Microphone Error Alert */}
        {recorder.error && (
          <Alert variant="destructive" className="mt-4 rounded-2xl flex items-center justify-between">
            <div className="flex items-center gap-2">
              <AlertCircle className="size-4 shrink-0" />
              <AlertDescription className="text-xs font-medium">
                {recorder.error}
              </AlertDescription>
            </div>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={recorder.clearError}
              className="h-6 px-2 text-xs text-destructive hover:bg-destructive/10"
            >
              Đóng
            </Button>
          </Alert>
        )}

        {/* Audio Visualizer Waveform */}
        <div className="mt-5">
          <VivaWaveform
            isRecording={recorder.isRecording}
            volumeLevel={recorder.volumeLevel}
          />
        </div>

        {/* Real-time Live Speech-to-Text Preview while recording */}
        {recorder.isRecording && (
          <div className="mt-4 rounded-2xl border border-brand/20 bg-brand/5 p-4 text-sm animate-in fade-in duration-200">
            <div className="flex items-center gap-2 font-medium text-brand text-xs uppercase tracking-wider mb-1.5">
              <Sparkles className="size-3.5 animate-spin text-brand" />
              Đang nhận diện giọng nói (Speech-to-Text):
            </div>
            <p className="text-foreground text-sm font-normal min-h-[1.5rem]">
              {recorder.transcript || recorder.interimTranscript ? (
                <>
                  <span>{recorder.transcript}</span>{' '}
                  <span className="text-brand font-medium italic animate-pulse">
                    {recorder.interimTranscript}
                  </span>
                </>
              ) : (
                <span className="text-muted-foreground italic">
                  Đang lắng nghe... Bạn hãy nói to, rõ ràng vào micro.
                </span>
              )}
            </p>
          </div>
        )}

        {/* Recording Controls */}
        <div className="mt-6 flex flex-wrap items-center justify-center gap-4">
          {!recorder.isRecording && !hasRecordedAudio && (
            <Button
              type="button"
              size="lg"
              disabled={recorder.isStarting}
              onClick={recorder.startRecording}
              className="gap-2.5 rounded-full px-7 py-6 text-base bg-brand hover:bg-brand-hover shadow-md hover:shadow-lg transition-all font-medium"
            >
              {recorder.isStarting ? (
                <>
                  <Loader2 className="size-5 animate-spin" />
                  Đang bật Micro...
                </>
              ) : (
                <>
                  <Mic className="size-5" />
                  Bắt đầu ghi âm câu trả lời
                </>
              )}
            </Button>
          )}

          {recorder.isRecording && (
            <div className="flex items-center gap-3">
              <Button
                type="button"
                variant="destructive"
                size="lg"
                onClick={handleStopRecording}
                className="gap-2.5 rounded-full px-7 py-6 text-base shadow-lg shadow-destructive/25 transition-all font-medium animate-pulse"
              >
                <Square className="size-5 fill-current" />
                Dừng ghi âm ({formatTime(recorder.recordingTime)})
              </Button>
              <Button
                type="button"
                variant="outline"
                size="lg"
                onClick={recorder.resetRecording}
                className="gap-1.5 rounded-full px-5 py-6 text-muted-foreground hover:text-foreground"
              >
                <X className="size-4" />
                Hủy bỏ
              </Button>
            </div>
          )}

          {hasRecordedAudio && !recorder.isRecording && (
            <div className="flex flex-wrap items-center justify-center gap-4">
              <audio controls src={recorder.audioUrl!} className="h-10 max-w-xs" />
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={recorder.resetRecording}
                title="Ghi âm lại"
                className="gap-2 rounded-full px-4"
              >
                <RotateCcw className="size-4" />
                Ghi âm lại
              </Button>
            </div>
          )}
        </div>

        {/* Text Answer Input (Auto-filled from voice or manual typing) */}
        <div className="mt-6 border-t pt-5">
          <div className="flex items-center justify-between">
            <label htmlFor={textInputId} className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider">
              {hasRecordedAudio || textAnswer
                ? 'Nội dung câu trả lời (nhận diện từ giọng nói - bạn có thể chỉnh sửa):'
                : 'Hoặc nhập câu trả lời bằng văn bản (dự phòng):'}
            </label>
            {textAnswer && (
              <button
                type="button"
                onClick={() => setTextAnswer('')}
                className="text-xs text-muted-foreground hover:text-destructive transition-colors"
              >
                Xóa văn bản
              </button>
            )}
          </div>

          {hasRecordedAudio && textAnswer && (
            <div className="mt-2 flex items-center gap-1.5 text-xs text-emerald-600 dark:text-emerald-400 font-medium">
              <CheckCircle2 className="size-3.5" />
              <span>Đã chuyển giọng nói thành văn bản bên dưới. Bạn có thể bấm vào khung để chỉnh sửa hoặc bổ sung ý trước khi gửi.</span>
            </div>
          )}

          <textarea
            id={textInputId}
            rows={4}
            value={textAnswer}
            onChange={(e) => setTextAnswer(e.target.value)}
            placeholder={
              recorder.isRecording
                ? 'Lời nói của bạn đang được chuyển thành chữ tại đây theo thời gian thực...'
                : 'Văn bản câu trả lời sẽ xuất hiện tại đây sau khi bạn nói, hoặc bạn có thể gõ trực tiếp...'
            }
            className="mt-2 w-full rounded-2xl border border-input bg-background p-4 text-sm outline-none transition-colors focus:border-brand focus:ring-4 focus:ring-brand/15 resize-none leading-relaxed"
          />
        </div>

        {/* Last Transcript Preview if returned from AI service after submit */}
        {transcript && (
          <div className="mt-4 rounded-2xl bg-muted/70 p-4 text-xs">
            <span className="font-semibold text-muted-foreground flex items-center gap-1.5 mb-1">
              <Volume2 className="size-3.5 text-brand" />
              Văn bản AI ghi nhận câu trước đó:
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
              <>
                <Loader2 className="size-4 animate-spin" />
                Đang xử lý câu trả lời…
              </>
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
