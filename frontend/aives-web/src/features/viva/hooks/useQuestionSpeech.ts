import { useCallback, useEffect, useRef, useState } from 'react'
import { vivaApi } from '../api/vivaApi'
import type { VivaQuestionDto } from '../types'

export type SpeechStatus = 'idle' | 'loading' | 'playing' | 'unavailable'

const VIETNAMESE_LETTERS = /[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]/i

/** Same rule as the backend: Vietnamese letters mean Vietnamese, anything else is read as English. */
export function speechLanguage(text: string): 'vi-VN' | 'en-US' {
  return VIETNAMESE_LETTERS.test(text) ? 'vi-VN' : 'en-US'
}

function canUseBrowserSpeech() {
  return typeof window !== 'undefined' && 'speechSynthesis' in window && 'SpeechSynthesisUtterance' in window
}

/**
 * Reads the question aloud when it appears and on "Listen again".
 * Main questions use the server's TTS audio. Follow-up questions, and any question the server
 * cannot voice, fall back to the browser's own speech synthesis. If neither works the
 * status becomes 'unavailable' and the student simply reads the text.
 */
export function useQuestionSpeech(sessionId: number | null, question: VivaQuestionDto | null, muted: boolean) {
  const [status, setStatus] = useState<SpeechStatus>('idle')
  const audioRef = useRef<HTMLAudioElement | null>(null)
  // Server audio for the question on screen, kept so "Listen again" does not refetch.
  const cacheRef = useRef<{ key: string; url: string } | null>(null)
  const runRef = useRef(0)

  const stop = useCallback(() => {
    runRef.current++
    audioRef.current?.pause()
    audioRef.current = null
    if (canUseBrowserSpeech()) window.speechSynthesis.cancel()
    setStatus((current) => (current === 'unavailable' ? current : 'idle'))
  }, [])

  const speakWithBrowser = useCallback((text: string, run: number) => {
    if (!canUseBrowserSpeech()) {
      setStatus('unavailable')
      return
    }
    const utterance = new SpeechSynthesisUtterance(text)
    utterance.lang = speechLanguage(text)
    utterance.onend = () => { if (runRef.current === run) setStatus('idle') }
    utterance.onerror = (event) => {
      if (runRef.current !== run) return
      // Cancelled on purpose (recording started), not a failure.
      setStatus(event.error === 'interrupted' || event.error === 'canceled' ? 'idle' : 'unavailable')
    }
    window.speechSynthesis.cancel()
    window.speechSynthesis.speak(utterance)
    setStatus('playing')
  }, [])

  const playUrl = useCallback(async (url: string, text: string, run: number) => {
    const audio = new Audio(url)
    audioRef.current = audio
    audio.onended = () => { if (runRef.current === run) setStatus('idle') }
    audio.onpause = () => { if (runRef.current === run) setStatus('idle') }
    try {
      await audio.play()
      if (runRef.current === run) setStatus('playing')
    } catch (err) {
      if (runRef.current !== run) return
      // Autoplay blocked: wait for the student to press "Listen again".
      if (err instanceof DOMException && err.name === 'NotAllowedError') setStatus('idle')
      // Undecodable audio (for example the AI service's mock TTS): use the browser voice instead.
      else speakWithBrowser(text, run)
    }
  }, [speakWithBrowser])

  const play = useCallback(async () => {
    if (!sessionId || !question?.questionText) return
    stop()
    const run = runRef.current
    const text = question.questionText

    if (question.isFollowUp) {
      speakWithBrowser(text, run)
      return
    }

    const key = `${sessionId}:${question.questionId}`
    if (cacheRef.current?.key === key) {
      await playUrl(cacheRef.current.url, text, run)
      return
    }

    setStatus('loading')
    try {
      const blob = await vivaApi.getQuestionAudio(sessionId, question.questionId)
      if (runRef.current !== run) return
      if (cacheRef.current) URL.revokeObjectURL(cacheRef.current.url)
      cacheRef.current = { key, url: URL.createObjectURL(blob) }
      await playUrl(cacheRef.current.url, text, run)
    } catch {
      if (runRef.current === run) speakWithBrowser(text, run)
    }
  }, [sessionId, question, stop, playUrl, speakWithBrowser])

  // Read each new question once, unless the student is recording.
  const spokenRef = useRef<string | null>(null)
  const questionKey = question ? `${question.questionId}:${question.isFollowUp}:${question.questionText}` : null
  useEffect(() => {
    if (!questionKey || muted || spokenRef.current === questionKey) return
    spokenRef.current = questionKey
    void play()
  }, [questionKey, muted, play])

  // Never let the question audio play into the microphone. The pause/cancel events reset the status.
  useEffect(() => {
    if (!muted) return
    audioRef.current?.pause()
    if (canUseBrowserSpeech()) window.speechSynthesis.cancel()
  }, [muted])

  useEffect(() => () => {
    runRef.current++
    audioRef.current?.pause()
    if (canUseBrowserSpeech()) window.speechSynthesis.cancel()
    if (cacheRef.current) URL.revokeObjectURL(cacheRef.current.url)
  }, [])

  return { status, play, stop }
}
