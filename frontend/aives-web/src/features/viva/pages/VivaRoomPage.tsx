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
import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { parseApiDate } from '@/lib/format/date'
import { toApiError } from '@/lib/http/apiError'
import { cn } from '@/lib/utils'
import { vivaApi } from '../api/vivaApi'
import { QuestionCard } from '../components/QuestionCard'
import { VivaSessionPicker } from '../components/VivaSessionPicker'
import { VivaWaveform } from '../components/VivaWaveform'
import { useAudioRecorder } from '../hooks/useAudioRecorder'
import { useCountdown } from '../hooks/useCountdown'
import type { StartVivaSessionResponse, VivaQuestionDto } from '../types'

export default function VivaRoomPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const navigate = useNavigate()
  const sessionParam = searchParams.get('sessionId')

  const [enteringId, setEnteringId] = useState<number | null>(null)
  const [session, setSession] = useState<StartVivaSessionResponse | null>(null)
  const [currentQuestion, setCurrentQuestion] = useState<VivaQuestionDto | null>(null)
  const [textAnswer, setTextAnswer] = useState('')
  const [transcript, setTranscript] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isFinishing, setIsFinishing] = useState(false)
  const [isCompleted, setIsCompleted] = useState(false)
  const [timedOut, setTimedOut] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [followUpAlert, setFollowUpAlert] = useState<string | null>(null)

  const recorder = useAudioRecorder()
  const textInputId = useId()
  const autoStartedSession = useRef<string | null>(null)
  // Guards against finishing twice (Submit early racing the timer).
  const finishingRef = useRef(false)

  // The clock starts when the student first enters, so a reload keeps counting from the same moment.
  // Uses this device's clock: it can drift if the device time is wrong (backend has no endsAt yet).
  const endsAt = session?.startedAt
    ? parseApiDate(session.startedAt).getTime() + session.durationMinutes * 60_000
    : null

  // Format recording seconds MM:SS
  const formatTime = (secs: number) => {
    const mins = Math.floor(secs / 60)
    const rem = secs % 60
    return `${mins.toString().padStart(2, '0')}:${rem.toString().padStart(2, '0')}`
  }

  // Mirror the live transcript into the editable answer, and fill it once more when recording stops.
  // Done during render (React's "adjust state when a prop changes" pattern) instead of in effects.
  const [seenTranscript, setSeenTranscript] = useState(recorder.transcript)
  const [wasRecording, setWasRecording] = useState(recorder.isRecording)
  if (recorder.transcript !== seenTranscript) {
    setSeenTranscript(recorder.transcript)
    if (recorder.transcript?.trim()) setTextAnswer(recorder.transcript.trim())
  }
  if (recorder.isRecording !== wasRecording) {
    setWasRecording(recorder.isRecording)
    const text = recorder.transcript || recorder.interimTranscript
    if (!recorder.isRecording && text?.trim()) setTextAnswer(text.trim())
  }

  // Stop recording and immediately populate textAnswer
  const handleStopRecording = () => {
    const textSnapshot = recorder.stopRecording()
    const textToFill = textSnapshot || recorder.transcript || recorder.interimTranscript
    if (textToFill && textToFill.trim()) {
      setTextAnswer(textToFill.trim())
    }
  }

  const handleStartSession = useCallback(async (rawId: string) => {
    const value = rawId.trim()
    const id = Number(value)
    if (!/^\d+$/.test(value) || !Number.isInteger(id) || id < 1 || id > 2147483647) {
      setErrorMessage('This link does not point to a valid session. Choose a session from the list below.')
      return
    }
    setIsLoading(true)
    setEnteringId(id)
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
      setEnteringId(null)
    }
  }, [])

  useEffect(() => {
    if (sessionParam && !session && autoStartedSession.current !== sessionParam) {
      autoStartedSession.current = sessionParam
      void handleStartSession(sessionParam)
    }
  }, [sessionParam, session, handleStartSession])

  // Picking a session puts it in the URL (so a reload returns to it) and starts it right away.
  function handleEnter(sessionId: number) {
    if (isLoading) return
    const value = String(sessionId)
    autoStartedSession.current = value
    setSearchParams({ sessionId: value }, { replace: true })
    void handleStartSession(value)
  }

  async function handleSubmitAnswer() {
    if (!session || !currentQuestion) return

    const hasAudio = Boolean(recorder.audioBlob)
    const hasText = Boolean(textAnswer.trim())

    if (!hasAudio && !hasText) {
      setErrorMessage('Please record your answer or type it before submitting.')
      return
    }

    setIsSubmitting(true)
    setErrorMessage(null)
    setFollowUpAlert(null)

    try {
      const res = await vivaApi.submitAnswer(
        session.sessionId,
        answerForm(currentQuestion, recorder.audioBlob, textAnswer.trim()),
      )

      if (res.transcript) {
        setTranscript(res.transcript)
      }

      // Reset input state for next question
      recorder.resetRecording()
      setTextAnswer('')

      if (res.shouldFollowUp && res.nextQuestion) {
        setFollowUpAlert('The AI has a follow-up question to clarify your last answer.')
        setCurrentQuestion(res.nextQuestion)
      } else if (res.isCompleted || !res.nextQuestion) {
        await handleFinishExam()
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
    if (!session || finishingRef.current) return
    finishingRef.current = true
    setIsFinishing(true)
    setErrorMessage(null)
    try {
      await vivaApi.finish(session.sessionId)
      setIsCompleted(true)
    } catch (err) {
      finishingRef.current = false
      const apiErr = toApiError(err)
      setErrorMessage(apiErr.message)
    } finally {
      setIsFinishing(false)
    }
  }

  // Time is up: hand in whatever is on screen for the current question (recording included), then finish.
  const { stopAndCollect } = recorder
  const handleTimeUp = useCallback(async () => {
    if (!session || isCompleted || finishingRef.current) return
    finishingRef.current = true
    setIsFinishing(true)
    setErrorMessage(null)
    try {
      const { audio, text } = await stopAndCollect()
      const answer = textAnswer.trim() || text
      let completed = false
      if (currentQuestion && !isSubmitting && (audio || answer)) {
        try {
          const res = await vivaApi.submitAnswer(session.sessionId, answerForm(currentQuestion, audio, answer))
          completed = res.isCompleted
        } catch {
          // Still finish: the answers submitted before this one are kept.
        }
      }
      if (!completed) await vivaApi.finish(session.sessionId)
      setTimedOut(true)
      setIsCompleted(true)
    } catch (err) {
      finishingRef.current = false
      setErrorMessage(`Time is up, but your exam could not be submitted: ${toApiError(err).message}`)
    } finally {
      setIsFinishing(false)
    }
  }, [session, isCompleted, currentQuestion, textAnswer, isSubmitting, stopAndCollect])

  const remainingMs = useCountdown(endsAt, () => void handleTimeUp())
  const isTimeUp = remainingMs === 0

  // 1. Initial screen: pick one of your sessions to enter
  if (!session) {
    return (
      <div className="mx-auto max-w-2xl py-6">
        <header>
          <h1 className="font-heading text-3xl font-medium tracking-tight">AI viva room</h1>
          <p className="mt-2 text-muted-foreground">
            Take your viva exam online by voice. Get your microphone ready and find a quiet place.
          </p>
        </header>

        <div className="mt-8 rounded-3xl border bg-card p-6 shadow-sm sm:p-8">
          {errorMessage && (
            <Alert variant="destructive" className="mb-6 rounded-2xl">
              <AlertCircle className="size-4" />
              <AlertDescription>{errorMessage}</AlertDescription>
            </Alert>
          )}
          <VivaSessionPicker enteringId={enteringId} onEnter={handleEnter} />
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
          {timedOut ? 'Time is up!' : 'Viva exam completed!'}
        </h1>
        <p className="mt-3 text-lg text-muted-foreground">
          {timedOut ? (
            <>
              Your answers in <span className="font-semibold text-foreground">{session.sessionName}</span> were submitted
              automatically.
            </>
          ) : (
            <>
              You have answered all the questions in <span className="font-semibold text-foreground">{session.sessionName}</span>.
            </>
          )}
        </p>

        <div className="mt-8 rounded-3xl border bg-card p-6 text-left shadow-sm sm:p-8">
          <h2 className="font-heading text-lg font-medium">Scoring status</h2>
          <p className="mt-2 text-sm text-muted-foreground">
            The AI has made a first assessment of all your answers. Your exam is now with the examiner, who will review it and publish the official score.
          </p>

          <div className="mt-6 flex flex-col gap-3 sm:flex-row">
            <Button asChild className="flex-1">
              <Link to={`/student/results?sessionId=${session.sessionId}`}>
                View result
                <ArrowRight className="size-4" />
              </Link>
            </Button>
            <Button variant="secondary" onClick={() => navigate('/student')}>
              Back to home
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
  const isLocked = isTimeUp || isFinishing
  const timerTone =
    remainingMs === null || remainingMs > 5 * 60_000
      ? 'bg-muted'
      : remainingMs > 60_000
        ? 'bg-amber-100 text-amber-900 dark:bg-amber-950/60 dark:text-amber-200'
        : 'bg-destructive/10 text-destructive'

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
          <div
            role="timer"
            aria-label="Time left"
            className={cn('flex items-center gap-2 rounded-2xl px-4 py-2 text-sm font-medium', timerTone)}
          >
            <Clock className={cn('size-4', remainingMs !== null && remainingMs <= 5 * 60_000 ? 'text-current' : 'text-brand')} />
            {remainingMs === null ? (
              <span>Duration: {session.durationMinutes} min</span>
            ) : (
              <span className="tabular-nums">{formatRemaining(remainingMs)} left</span>
            )}
          </div>

          <Button
            variant="destructive"
            size="sm"
            onClick={handleFinishExam}
            disabled={isLocked}
            className="rounded-xl px-4"
          >
            {isFinishing ? 'Submitting…' : 'Submit early'}
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

      {isTimeUp && isFinishing && (
        <Alert className="mt-4">
          <Loader2 className="size-4 animate-spin" />
          <AlertDescription>Time is up. Submitting your answers…</AlertDescription>
        </Alert>
      )}

      {/* The automatic submit failed (e.g. network): let the student retry it */}
      {isTimeUp && !isFinishing && (
        <div className="mt-4 flex justify-end">
          <Button onClick={() => void handleTimeUp()} className="gap-2 rounded-xl">
            <Send className="size-4" />
            Submit my exam
          </Button>
        </div>
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
            Your answer
          </h2>

          {/* Active status indicator */}
          {recorder.isRecording && (
            <span className="flex items-center gap-2 rounded-full border border-destructive/30 bg-destructive/10 px-3.5 py-1 text-xs font-semibold text-destructive animate-pulse">
              <span className="size-2 rounded-full bg-destructive animate-ping" />
              Recording ({formatTime(recorder.recordingTime)})
            </span>
          )}

          {!recorder.isRecording && hasRecordedAudio && (
            <span className="flex items-center gap-1.5 rounded-full border border-emerald-500/30 bg-emerald-500/10 px-3 py-1 text-xs font-semibold text-emerald-600 dark:text-emerald-400">
              <CheckCircle2 className="size-3.5" />
              Recorded ({formatTime(recorder.recordingTime)})
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
              Close
            </Button>
          </Alert>
        )}

        {/* Browsers without live speech-to-text (Firefox, some Safari) or without recording at all */}
        {(!recorder.isRecordingSupported || !recorder.isSpeechSupported) && (
          <Alert className="mt-4 rounded-2xl">
            <AlertCircle className="size-4" />
            <AlertDescription className="text-xs">
              {recorder.isRecordingSupported
                ? 'This browser cannot show your words while you speak. Record your answer and it will be transcribed after you submit, or type it in the box below.'
                : 'This browser cannot record audio. Type your answer in the box below.'}
            </AlertDescription>
          </Alert>
        )}

        {/* Audio Visualizer Waveform */}
        {recorder.isRecordingSupported && (
          <div className="mt-5">
            <VivaWaveform
              isRecording={recorder.isRecording}
              volumeLevel={recorder.volumeLevel}
            />
          </div>
        )}

        {/* Real-time Live Speech-to-Text Preview while recording */}
        {recorder.isRecording && (
          <div className="mt-4 rounded-2xl border border-brand/20 bg-brand/5 p-4 text-sm animate-in fade-in duration-200">
            <div className="flex items-center gap-2 font-medium text-brand text-xs uppercase tracking-wider mb-1.5">
              <Sparkles className="size-3.5 animate-spin text-brand" />
              Transcribing your speech:
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
                  Listening... Speak clearly into your microphone.
                </span>
              )}
            </p>
          </div>
        )}

        {/* Recording Controls */}
        <div className="mt-6 flex flex-wrap items-center justify-center gap-4">
          {recorder.isRecordingSupported && !recorder.isRecording && !hasRecordedAudio && (
            <Button
              type="button"
              size="lg"
              disabled={recorder.isStarting || isLocked}
              onClick={recorder.startRecording}
              className="gap-2.5 rounded-full px-7 py-6 text-base bg-brand hover:bg-brand-hover shadow-md hover:shadow-lg transition-all font-medium"
            >
              {recorder.isStarting ? (
                <>
                  <Loader2 className="size-5 animate-spin" />
                  Turning on the microphone...
                </>
              ) : (
                <>
                  <Mic className="size-5" />
                  Start recording your answer
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
                Stop recording ({formatTime(recorder.recordingTime)})
              </Button>
              <Button
                type="button"
                variant="outline"
                size="lg"
                onClick={recorder.resetRecording}
                className="gap-1.5 rounded-full px-5 py-6 text-muted-foreground hover:text-foreground"
              >
                <X className="size-4" />
                Cancel
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
                disabled={isLocked}
                title="Record again"
                className="gap-2 rounded-full px-4"
              >
                <RotateCcw className="size-4" />
                Record again
              </Button>
            </div>
          )}
        </div>

        {/* Text Answer Input (Auto-filled from voice or manual typing) */}
        <div className="mt-6 border-t pt-5">
          <div className="flex items-center justify-between">
            <label htmlFor={textInputId} className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider">
              {!recorder.isRecordingSupported
                ? 'Type your answer:'
                : !recorder.isSpeechSupported
                  ? 'Or type your answer:'
                  : hasRecordedAudio || textAnswer
                    ? 'Your answer (transcribed from your voice – you can edit it):'
                    : 'Or type your answer (fallback):'}
            </label>
            {textAnswer && (
              <button
                type="button"
                onClick={() => setTextAnswer('')}
                className="text-xs text-muted-foreground hover:text-destructive transition-colors"
              >
                Clear text
              </button>
            )}
          </div>

          {hasRecordedAudio && textAnswer && (
            <div className="mt-2 flex items-center gap-1.5 text-xs text-emerald-600 dark:text-emerald-400 font-medium">
              <CheckCircle2 className="size-3.5" />
              <span>Your speech has been transcribed below. Click the box to edit or add to it before submitting.</span>
            </div>
          )}

          {hasRecordedAudio && !textAnswer && (
            <p className="mt-2 text-xs text-muted-foreground">
              Your recording will be transcribed after you submit. You can also type a written answer.
            </p>
          )}

          <textarea
            id={textInputId}
            rows={4}
            value={textAnswer}
            disabled={isLocked}
            onChange={(e) => setTextAnswer(e.target.value)}
            placeholder={
              !recorder.isSpeechSupported
                ? 'Type your answer here...'
                : recorder.isRecording
                  ? 'Your words appear here in real time...'
                  : 'Your answer appears here after you speak, or you can type it directly...'
            }
            className="mt-2 w-full rounded-2xl border border-input bg-background p-4 text-sm outline-none transition-colors focus:border-brand focus:ring-4 focus:ring-brand/15 resize-none leading-relaxed disabled:cursor-not-allowed disabled:opacity-60"
          />
        </div>

        {/* Last Transcript Preview if returned from AI service after submit */}
        {transcript && (
          <div className="mt-4 rounded-2xl bg-muted/70 p-4 text-xs">
            <span className="font-semibold text-muted-foreground flex items-center gap-1.5 mb-1">
              <Volume2 className="size-3.5 text-brand" />
              What the AI recorded for your previous answer:
            </span>
            <p className="text-foreground italic">"{transcript}"</p>
          </div>
        )}

        {/* Submit action */}
        <div className="mt-6 flex justify-end">
          <Button
            size="lg"
            onClick={handleSubmitAnswer}
            disabled={isSubmitting || isLocked || recorder.isRecording || (!recorder.audioBlob && !textAnswer.trim())}
            className="gap-2 rounded-xl px-7"
          >
            {isSubmitting ? (
              <>
                <Loader2 className="size-4 animate-spin" />
                Processing your answer…
              </>
            ) : (
              <>
                <Send className="size-4" />
                Submit answer
              </>
            )}
          </Button>
        </div>
      </section>
    </div>
  )
}

function answerForm(question: VivaQuestionDto, audio: Blob | null, text: string) {
  const formData = new FormData()
  formData.append('questionId', question.questionId.toString())
  formData.append('isFollowUp', question.isFollowUp.toString())
  if (audio) {
    // The backend names the stored object after this extension, so it must match the real format.
    formData.append('audioFile', audio, `answer.${audioExtension(audio.type)}`)
  }
  if (text) formData.append('textAnswer', text)
  return formData
}

/** 754_000 → "12:34" (mm:ss, rounded up so "00:00" only shows once time is really up). */
function formatRemaining(ms: number) {
  const total = Math.ceil(ms / 1000)
  const mins = Math.floor(total / 60)
  const secs = total % 60
  return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`
}

/** "audio/webm;codecs=opus" → "webm", "audio/mp4" → "m4a"; MediaRecorder output varies by browser. */
function audioExtension(mimeType: string) {
  if (mimeType.includes('mp4')) return 'm4a'
  if (mimeType.includes('ogg')) return 'ogg'
  if (mimeType.includes('wav')) return 'wav'
  return 'webm'
}
