import { useCallback, useEffect, useRef, useState } from 'react'

export interface AudioRecorderState {
  isStarting: boolean
  isRecording: boolean
  recordingTime: number
  volumeLevel: number
  audioBlob: Blob | null
  audioUrl: string | null
  transcript: string
  interimTranscript: string
  isSpeechSupported: boolean
  /** False when the browser cannot record audio at all; the student can only type. */
  isRecordingSupported: boolean
  error: string | null
  startRecording: () => Promise<void>
  stopRecording: () => string
  /** Stops any ongoing recording and resolves once its audio is ready, e.g. when time runs out mid-answer. */
  stopAndCollect: () => Promise<{ audio: Blob | null; text: string }>
  resetRecording: () => void
  clearError: () => void
}

export function useAudioRecorder(): AudioRecorderState {
  const [isStarting, setIsStarting] = useState(false)
  const [isRecording, setIsRecording] = useState(false)
  const [recordingTime, setRecordingTime] = useState(0)
  const [volumeLevel, setVolumeLevel] = useState(0)
  const [audioBlob, setAudioBlob] = useState<Blob | null>(null)
  const [audioUrl, setAudioUrl] = useState<string | null>(null)
  const [transcript, setTranscript] = useState('')
  const [interimTranscript, setInterimTranscript] = useState('')
  const [error, setError] = useState<string | null>(null)

  const mediaRecorderRef = useRef<MediaRecorder | null>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const audioContextRef = useRef<AudioContext | null>(null)
  const analyserRef = useRef<AnalyserNode | null>(null)
  const animationFrameRef = useRef<number | null>(null)
  const timerRef = useRef<number | null>(null)
  const chunksRef = useRef<Blob[]>([])
  const recognitionRef = useRef<any>(null)
  const lastRecognizedRef = useRef('')
  const audioBlobRef = useRef<Blob | null>(null)
  const onStoppedRef = useRef<((blob: Blob) => void) | null>(null)

  const isSpeechSupported =
    typeof window !== 'undefined' &&
    ('SpeechRecognition' in window || 'webkitSpeechRecognition' in window)

  const isRecordingSupported =
    typeof window !== 'undefined' &&
    typeof window.MediaRecorder !== 'undefined' &&
    Boolean(navigator.mediaDevices?.getUserMedia)

  const stopAudioContext = useCallback(() => {
    if (animationFrameRef.current) {
      cancelAnimationFrame(animationFrameRef.current)
      animationFrameRef.current = null
    }
    if (audioContextRef.current && audioContextRef.current.state !== 'closed') {
      audioContextRef.current.close().catch(() => {})
      audioContextRef.current = null
    }
    setVolumeLevel(0)
  }, [])

  const stopRecognition = useCallback(() => {
    if (recognitionRef.current) {
      try {
        recognitionRef.current.onresult = null
        recognitionRef.current.onerror = null
        recognitionRef.current.onend = null
        recognitionRef.current.stop()
      } catch {
        // Ignore if already stopped
      }
      recognitionRef.current = null
    }
    setInterimTranscript('')
  }, [])

  const resetRecording = useCallback(() => {
    if (mediaRecorderRef.current && mediaRecorderRef.current.state !== 'inactive') {
      try {
        mediaRecorderRef.current.stop()
      } catch {}
    }
    stopRecognition()
    stopAudioContext()

    if (streamRef.current) {
      streamRef.current.getTracks().forEach((track) => track.stop())
      streamRef.current = null
    }
    if (timerRef.current) {
      clearInterval(timerRef.current)
      timerRef.current = null
    }
    if (audioUrl) {
      URL.revokeObjectURL(audioUrl)
    }

    setIsStarting(false)
    setIsRecording(false)
    setRecordingTime(0)
    setVolumeLevel(0)
    setAudioBlob(null)
    audioBlobRef.current = null
    setAudioUrl(null)
    setTranscript('')
    setInterimTranscript('')
    lastRecognizedRef.current = ''
    setError(null)
    chunksRef.current = []
  }, [audioUrl, stopAudioContext, stopRecognition])

  const clearError = useCallback(() => {
    setError(null)
  }, [])

  const startRecording = useCallback(async () => {
    try {
      setIsStarting(true)
      setError(null)

      // Stop any existing stream/session first
      if (streamRef.current) {
        streamRef.current.getTracks().forEach((t) => t.stop())
        streamRef.current = null
      }
      if (timerRef.current) {
        clearInterval(timerRef.current)
        timerRef.current = null
      }
      stopAudioContext()
      stopRecognition()
      if (audioUrl) {
        URL.revokeObjectURL(audioUrl)
        setAudioUrl(null)
      }
      setAudioBlob(null)
      audioBlobRef.current = null
      setTranscript('')
      setInterimTranscript('')
      lastRecognizedRef.current = ''
      chunksRef.current = []

      if (!isRecordingSupported) {
        throw new Error('Your browser cannot record audio. Type your answer in the box below instead.')
      }

      // Request microphone stream
      let stream: MediaStream
      try {
        stream = await navigator.mediaDevices.getUserMedia({
          audio: {
            echoCancellation: true,
            noiseSuppression: true,
            autoGainControl: true,
          },
        })
      } catch (mediaErr: any) {
        if (mediaErr?.name === 'NotAllowedError' || mediaErr?.name === 'PermissionDeniedError') {
          throw new Error('Microphone access is blocked or not granted. Click the settings / lock icon in the address bar to allow the microphone.')
        } else if (mediaErr?.name === 'NotFoundError' || mediaErr?.name === 'DevicesNotFoundError') {
          throw new Error('No microphone was found on your computer.')
        } else if (mediaErr?.name === 'NotReadableError' || mediaErr?.name === 'TrackStartError') {
          throw new Error('Another app (Zoom, Teams, etc.) is using the microphone. Close it and try again.')
        } else {
          throw new Error(mediaErr?.message || 'Cannot access the microphone. Check the microphone permission in your browser.')
        }
      }

      streamRef.current = stream

      // Set up AudioContext for real-time waveform visualization
      try {
        const AudioContextClass =
          window.AudioContext ||
          (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext
        if (AudioContextClass) {
          const audioCtx = new AudioContextClass()
          audioContextRef.current = audioCtx
          if (audioCtx.state === 'suspended') {
            await audioCtx.resume()
          }

          const analyser = audioCtx.createAnalyser()
          analyser.fftSize = 64
          analyserRef.current = analyser

          const source = audioCtx.createMediaStreamSource(stream)
          source.connect(analyser)

          const dataArray = new Uint8Array(analyser.frequencyBinCount)
          const updateVolume = () => {
            if (!analyserRef.current) return
            analyserRef.current.getByteFrequencyData(dataArray)
            let sum = 0
            for (let i = 0; i < dataArray.length; i++) {
              sum += dataArray[i]
            }
            const average = sum / dataArray.length
            const normalized = Math.min(100, Math.round((average / 128) * 100))
            setVolumeLevel(normalized)
            animationFrameRef.current = requestAnimationFrame(updateVolume)
          }
          updateVolume()
        }
      } catch (e) {
        console.warn('AudioContext visualization not available:', e)
      }

      // Start Web Speech API SpeechRecognition for client-side transcription
      try {
        const SpeechRec =
          (window as any).SpeechRecognition ||
          (window as any).webkitSpeechRecognition
        if (SpeechRec) {
          const recognition = new SpeechRec()
          recognition.continuous = true
          recognition.interimResults = true
          recognition.lang = 'vi-VN'

          recognition.onresult = (event: any) => {
            let finalStr = ''
            let interimStr = ''
            for (let i = 0; i < event.results.length; ++i) {
              const res = event.results[i]
              if (res.isFinal) {
                finalStr += (res[0]?.transcript || '') + ' '
              } else {
                interimStr += (res[0]?.transcript || '')
              }
            }

            const combinedText = (finalStr + ' ' + interimStr).trim()
            if (combinedText) {
              lastRecognizedRef.current = combinedText
              setTranscript(combinedText)
            }
            setInterimTranscript(interimStr.trim())
          }

          recognition.onerror = (e: any) => {
            // Ignore non-fatal no-speech error
            if (e.error !== 'no-speech') {
              console.warn('SpeechRecognition warning:', e.error)
            }
          }

          recognition.onend = () => {
            setInterimTranscript('')
          }

          recognitionRef.current = recognition
          recognition.start()
        }
      } catch (speechErr) {
        console.warn('Web Speech Recognition failed to initialize:', speechErr)
      }

      // Determine supported mime type for MediaRecorder
      let mimeType = 'audio/webm;codecs=opus'
      if (!MediaRecorder.isTypeSupported(mimeType)) {
        mimeType = MediaRecorder.isTypeSupported('audio/webm')
          ? 'audio/webm'
          : MediaRecorder.isTypeSupported('audio/mp4')
            ? 'audio/mp4'
            : ''
      }

      const recorder = mimeType ? new MediaRecorder(stream, { mimeType }) : new MediaRecorder(stream)
      mediaRecorderRef.current = recorder
      chunksRef.current = []

      recorder.ondataavailable = (event) => {
        if (event.data && event.data.size > 0) {
          chunksRef.current.push(event.data)
        }
      }

      recorder.onstop = () => {
        const blobType = recorder.mimeType || 'audio/webm'
        const finalBlob = new Blob(chunksRef.current, { type: blobType })
        setAudioBlob(finalBlob)
        audioBlobRef.current = finalBlob
        const url = URL.createObjectURL(finalBlob)
        setAudioUrl(url)
        stopAudioContext()
        onStoppedRef.current?.(finalBlob)
        onStoppedRef.current = null
      }

      recorder.start(250) // slices of 250ms
      setIsRecording(true)
      setIsStarting(false)

      // Start timer
      setRecordingTime(0)
      timerRef.current = window.setInterval(() => {
        setRecordingTime((prev) => prev + 1)
      }, 1000)
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Could not start recording.'
      setError(msg)
      setIsStarting(false)
      setIsRecording(false)
      stopAudioContext()
      stopRecognition()
      if (streamRef.current) {
        streamRef.current.getTracks().forEach((track) => track.stop())
        streamRef.current = null
      }
    }
  }, [audioUrl, isRecordingSupported, stopAudioContext, stopRecognition])

  const stopRecording = useCallback((): string => {
    const textSnapshot = lastRecognizedRef.current.trim()
    if (textSnapshot) {
      setTranscript(textSnapshot)
    }

    if (mediaRecorderRef.current && mediaRecorderRef.current.state !== 'inactive') {
      try {
        mediaRecorderRef.current.stop()
      } catch {}
    }
    stopRecognition()
    if (streamRef.current) {
      streamRef.current.getTracks().forEach((track) => track.stop())
      streamRef.current = null
    }
    if (timerRef.current) {
      clearInterval(timerRef.current)
      timerRef.current = null
    }
    setIsRecording(false)
    setIsStarting(false)

    return textSnapshot
  }, [stopRecognition])

  const stopAndCollect = useCallback(async () => {
    const recorder = mediaRecorderRef.current
    if (!recorder || recorder.state === 'inactive') {
      return { audio: audioBlobRef.current, text: lastRecognizedRef.current.trim() }
    }
    // onstop fires asynchronously after stop(); give up after a moment so time-up never hangs.
    const stopped = new Promise<Blob | null>((resolve) => {
      onStoppedRef.current = resolve
      window.setTimeout(() => resolve(null), 2000)
    })
    const text = stopRecording()
    return { audio: await stopped, text }
  }, [stopRecording])

  useEffect(() => {
    return () => {
      resetRecording()
    }
  }, [resetRecording])

  return {
    isStarting,
    isRecording,
    recordingTime,
    volumeLevel,
    audioBlob,
    audioUrl,
    transcript,
    interimTranscript,
    isSpeechSupported,
    isRecordingSupported,
    error,
    startRecording,
    stopRecording,
    stopAndCollect,
    resetRecording,
    clearError,
  }
}
