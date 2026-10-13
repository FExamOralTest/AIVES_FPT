"""
AIVES AI Service - Speech-to-Text Service
Transcribes student voice answers to text.
Pluggable: mock / Google STT / OpenAI Whisper.
"""

import asyncio
import base64
import os
import tempfile

from loguru import logger
from app.core.config import get_settings
from app.models.response import SpeechToTextResponse

settings = get_settings()


def _decode_audio(path: str):
    """Decode any audio file to 16kHz mono int16 numpy array using PyAV."""
    import av
    import numpy as np

    container = av.open(path)
    stream = container.streams.audio[0]
    resampler = av.AudioResampler(format="s16", layout="mono", rate=16000)
    chunks = []
    for frame in container.decode(stream):
        for rframe in resampler.resample(frame):
            arr = rframe.to_ndarray()
            if arr.size:
                chunks.append(arr.reshape(-1))
    if not chunks:
        return np.zeros(0, dtype=np.int16)
    return np.concatenate(chunks)


class SpeechToTextService:
    """
    Service to transcribe audio bytes to text.
    Provider is selected via STT_PROVIDER env var.
    Supported: 'mock', 'google', 'openai_whisper'
    """

    def __init__(self) -> None:
        self.provider = settings.stt_provider.lower()
        logger.info(f"[STTService] Provider: {self.provider}")

    async def transcribe(
        self,
        audio_bytes: bytes,
        exam_session_id: str,
        language: str = "vi-VN",
    ) -> SpeechToTextResponse:
        """
        Transcribe audio bytes to text.

        Args:
            audio_bytes: Raw audio bytes (WAV / WebM / MP3).
            exam_session_id: ID of the exam session.
            language: BCP-47 language code (default: vi-VN).

        Returns:
            SpeechToTextResponse with transcript and confidence.
        """
        logger.info(
            f"[STTService] Transcribing audio for session {exam_session_id} "
            f"({len(audio_bytes)} bytes, lang={language})"
        )

        if self.provider == "google":
            return await self._transcribe_google(audio_bytes, language)
        elif self.provider == "openai_whisper":
            return await self._transcribe_whisper(audio_bytes, language)
        elif self.provider == "faster_whisper":
            return await self._transcribe_faster_whisper(audio_bytes, language)
        else:
            return await self._transcribe_mock(audio_bytes, language)

    # ── Mock ──────────────────────────────────────────────
    async def _transcribe_mock(
        self, audio_bytes: bytes, language: str
    ) -> SpeechToTextResponse:
        """Returns a placeholder transcript for development/testing."""
        logger.debug("[STTService] Using mock transcription.")
        return SpeechToTextResponse(
            success=True,
            transcript=(
                "Đây là transcript mẫu từ mock STT. "
                "Cấu hình STT_PROVIDER=google hoặc openai_whisper để sử dụng thực tế."
            ),
            confidence=0.95,
            language_detected=language,
        )

    # ── Google STT ─────────────────────────────────────────
    async def _transcribe_google(
        self, audio_bytes: bytes, language: str
    ) -> SpeechToTextResponse:
        """
        Google Cloud Speech-to-Text v1 REST API.
        Requires GOOGLE_STT_API_KEY in environment.
        """
        import httpx

        if not settings.google_stt_api_key:
            raise ValueError("GOOGLE_STT_API_KEY is not set.")

        audio_base64 = base64.b64encode(audio_bytes).decode("utf-8")
        url = (
            f"https://speech.googleapis.com/v1/speech:recognize"
            f"?key={settings.google_stt_api_key}"
        )
        payload = {
            "config": {
                "encoding": "WEBM_OPUS",
                "sampleRateHertz": 48000,
                "languageCode": language,
                "enableAutomaticPunctuation": True,
            },
            "audio": {"content": audio_base64},
        }

        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.post(url, json=payload)
            response.raise_for_status()
            data = response.json()

        results = data.get("results", [])
        if not results:
            return SpeechToTextResponse(
                success=True,
                transcript="",
                confidence=0.0,
                language_detected=language,
                message="No speech detected in audio.",
            )

        best = results[0]["alternatives"][0]
        return SpeechToTextResponse(
            success=True,
            transcript=best.get("transcript", ""),
            confidence=best.get("confidence"),
            language_detected=language,
        )

    # ── Faster-Whisper (local, free) ───────────────────────
    async def _transcribe_faster_whisper(
        self, audio_bytes: bytes, language: str
    ) -> SpeechToTextResponse:
        """
        Local Faster-Whisper transcription (no API key, CPU-friendly).
        Requires STT_PROVIDER=faster_whisper.
        """
        from faster_whisper import WhisperModel

        lang_code = language.split("-")[0]
        model = WhisperModel(settings.faster_whisper_model, device="cpu", compute_type="int8")

        def _run() -> tuple[str, float | None]:
            tmp_path = None
            try:
                with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
                    tmp.write(audio_bytes)
                    tmp_path = tmp.name
                audio = _decode_audio(tmp_path)
                segments, info = model.transcribe(audio, language=lang_code)
                text = " ".join(seg.text for seg in segments).strip()
                return text, getattr(info, "language_probability", None)
            finally:
                if tmp_path:
                    try:
                        os.remove(tmp_path)
                    except OSError:
                        pass

        text, prob = await asyncio.to_thread(_run)
        return SpeechToTextResponse(
            success=True,
            transcript=text,
            confidence=prob,
            language_detected=language,
        )

    # ── OpenAI Whisper ─────────────────────────────────────
    async def _transcribe_whisper(
        self, audio_bytes: bytes, language: str
    ) -> SpeechToTextResponse:
        """
        OpenAI Whisper API transcription.
        Requires OPENAI_API_KEY in environment.
        """
        import httpx

        if not settings.openai_api_key:
            raise ValueError("OPENAI_API_KEY is not set.")

        # Whisper uses language code without region (vi not vi-VN)
        lang_code = language.split("-")[0]

        async with httpx.AsyncClient(timeout=60.0) as client:
            response = await client.post(
                "https://api.openai.com/v1/audio/transcriptions",
                headers={"Authorization": f"Bearer {settings.openai_api_key}"},
                files={"file": ("audio.webm", audio_bytes, "audio/webm")},
                data={"model": "whisper-1", "language": lang_code},
            )
            response.raise_for_status()
            data = response.json()

        return SpeechToTextResponse(
            success=True,
            transcript=data.get("text", ""),
            confidence=None,  # Whisper doesn't return confidence
            language_detected=language,
        )
