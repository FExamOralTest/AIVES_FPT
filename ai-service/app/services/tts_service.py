"""
AIVES AI Service - Text-to-Speech Service
Converts AI question text to audio for playback.
Pluggable: mock / Google TTS / OpenAI TTS.
"""

import base64
from loguru import logger
from app.core.config import get_settings
from app.models.request import TextToSpeechRequest
from app.models.response import TextToSpeechResponse

settings = get_settings()


class TextToSpeechService:
    """
    Service to convert text to speech audio.
    Provider is selected via TTS_PROVIDER env var.
    Supported: 'mock', 'google', 'openai_tts'
    """

    def __init__(self) -> None:
        self.provider = settings.tts_provider.lower()
        logger.info(f"[TTSService] Provider: {self.provider}")

    async def synthesize(self, request: TextToSpeechRequest) -> TextToSpeechResponse:
        """
        Convert text to speech audio.

        Args:
            request: TextToSpeechRequest with text and language config.

        Returns:
            TextToSpeechResponse with base64-encoded audio.
        """
        logger.info(
            f"[TTSService] Synthesizing {len(request.text)} chars "
            f"in {request.language}"
        )

        if self.provider == "google":
            return await self._synthesize_google(request)
        elif self.provider == "openai_tts":
            return await self._synthesize_openai(request)
        elif self.provider == "edge_tts":
            return await self._synthesize_edge_tts(request)
        else:
            return await self._synthesize_mock(request)

    # ── Mock ──────────────────────────────────────────────
    async def _synthesize_mock(self, request: TextToSpeechRequest) -> TextToSpeechResponse:
        """Returns empty base64 audio (silent) for development."""
        logger.debug("[TTSService] Using mock TTS.")
        # Return minimal valid base64 (represents a valid but silent WAV header)
        silent_wav_b64 = base64.b64encode(b"RIFF\x24\x00\x00\x00WAVEfmt ").decode()
        return TextToSpeechResponse(
            success=True,
            audio_base64=silent_wav_b64,
            audio_format="wav",
            duration_seconds=0.0,
            message="Mock TTS: configure TTS_PROVIDER=google or openai_tts for real audio.",
        )

    # ── Edge TTS (free, Vietnamese supported) ─────────────
    async def _synthesize_edge_tts(self, request: TextToSpeechRequest) -> TextToSpeechResponse:
        """
        Microsoft Edge TTS (free, no API key).
        Default voice: vi-VN-HoaiMyNeural.
        Requires TTS_PROVIDER=edge_tts.
        """
        import edge_tts

        voice = request.voice or "vi-VN-HoaiMyNeural"
        communicate = edge_tts.Communicate(request.text, voice)
        audio_chunks = bytearray()
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                audio_chunks.extend(chunk["data"])

        audio_b64 = base64.b64encode(bytes(audio_chunks)).decode("utf-8")
        return TextToSpeechResponse(
            success=True,
            audio_base64=audio_b64,
            audio_format="mp3",
        )

    # ── Google TTS ─────────────────────────────────────────
    async def _synthesize_google(self, request: TextToSpeechRequest) -> TextToSpeechResponse:
        """
        Google Cloud Text-to-Speech REST API.
        Requires GOOGLE_TTS_API_KEY in environment.
        """
        import httpx

        if not settings.google_tts_api_key:
            raise ValueError("GOOGLE_TTS_API_KEY is not set.")

        # Default Vietnamese female voice
        voice_name = request.voice or "vi-VN-Standard-A"
        url = (
            f"https://texttospeech.googleapis.com/v1/text:synthesize"
            f"?key={settings.google_tts_api_key}"
        )
        payload = {
            "input": {"text": request.text},
            "voice": {
                "languageCode": request.language,
                "name": voice_name,
                "ssmlGender": "FEMALE",
            },
            "audioConfig": {"audioEncoding": "MP3"},
        }

        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.post(url, json=payload)
            response.raise_for_status()
            data = response.json()

        return TextToSpeechResponse(
            success=True,
            audio_base64=data["audioContent"],
            audio_format="mp3",
        )

    # ── OpenAI TTS ─────────────────────────────────────────
    async def _synthesize_openai(self, request: TextToSpeechRequest) -> TextToSpeechResponse:
        """
        OpenAI TTS API.
        Requires OPENAI_API_KEY in environment.
        Note: OpenAI TTS does not natively support Vietnamese; falls back to en.
        """
        import httpx

        if not settings.openai_api_key:
            raise ValueError("OPENAI_API_KEY is not set.")

        voice = request.voice or "nova"
        payload = {
            "model": "tts-1",
            "input": request.text,
            "voice": voice,
            "response_format": "mp3",
        }

        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.post(
                "https://api.openai.com/v1/audio/speech",
                headers={"Authorization": f"Bearer {settings.openai_api_key}"},
                json=payload,
            )
            response.raise_for_status()
            audio_bytes = response.content

        audio_b64 = base64.b64encode(audio_bytes).decode("utf-8")
        return TextToSpeechResponse(
            success=True,
            audio_base64=audio_b64,
            audio_format="mp3",
        )
