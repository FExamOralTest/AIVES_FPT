"""
AIVES AI Service - Speech API Router
Endpoints for Speech-to-Text and Text-to-Speech.
Called by ASP.NET Core Backend (not directly by Frontend - see BR-14).
"""

import asyncio
import secrets

from fastapi import APIRouter, Depends, Header, HTTPException, UploadFile, File, Form, status
from loguru import logger

from app.core.config import get_settings
from app.models.request import StoredSpeechToTextRequest, TextToSpeechRequest
from app.models.response import StoredSpeechToTextResponse, SpeechToTextResponse, TextToSpeechResponse, ErrorResponse
from app.services.r2_audio_service import AudioStorageError, download_audio
from app.services.speech_service import SpeechToTextService
from app.services.tts_service import TextToSpeechService

router = APIRouter(prefix="/speech", tags=["Speech"])
_stt_service = SpeechToTextService()
_tts_service = TextToSpeechService()


def _require_internal_token(x_internal_token: str | None = Header(default=None)) -> None:
    secret = get_settings().ai_service_secret_key
    if secret in {"", "dev-secret-key", "your-secret-key-here"}:
        raise HTTPException(status_code=503, detail="Internal service authentication is not configured.")
    if not x_internal_token or not secrets.compare_digest(x_internal_token.encode(), secret.encode()):
        raise HTTPException(status_code=401, detail="Invalid internal service token.")


@router.post(
    "/transcribe-object",
    response_model=StoredSpeechToTextResponse,
    dependencies=[Depends(_require_internal_token)],
    summary="Transcribe an answer audio object from private R2 storage",
)
async def transcribe_audio_object(request: StoredSpeechToTextRequest) -> StoredSpeechToTextResponse:
    try:
        audio = await asyncio.to_thread(download_audio, request.object_key)
        result = await _stt_service.transcribe(
            audio_bytes=audio,
            exam_session_id=str(request.answer_id),
            language=request.language,
        )
        return StoredSpeechToTextResponse(
            success=result.success,
            message=result.message,
            answer_id=request.answer_id,
            transcript=result.transcript,
            language=result.language_detected or request.language,
            duration=result.duration,
        )
    except AudioStorageError as exc:
        raise HTTPException(status_code=exc.status_code, detail=str(exc)) from exc
    except Exception as exc:
        logger.exception("STT failed for answer {}", request.answer_id)
        raise HTTPException(status_code=500, detail="Audio transcription failed.") from exc


@router.post(
    "/transcribe",
    response_model=SpeechToTextResponse,
    summary="Speech to Text",
    description=(
        "Transcribe student voice audio to text. "
        "Accepts multipart/form-data with audio file + session metadata. "
        "Supports WAV, WebM, MP3 formats."
    ),
    responses={
        200: {"description": "Transcript returned successfully"},
        400: {"description": "Invalid audio file"},
        500: {"model": ErrorResponse, "description": "STT transcription failed"},
    },
)
async def speech_to_text(
    audio_file: UploadFile = File(..., description="Student voice recording"),
    exam_session_id: str = Form(..., description="UUID of the ExamSession"),
    language: str = Form(default="vi-VN", description="BCP-47 language code"),
) -> SpeechToTextResponse:
    """
    Transcribe audio to text using the configured STT provider.

    Flow: Backend → POST /ai/speech/transcribe (multipart) → transcript text
    """
    try:
        logger.info(
            f"POST /speech/transcribe | session={exam_session_id} | "
            f"file={audio_file.filename} | lang={language}"
        )

        if not audio_file.filename:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="No audio file provided.",
            )

        audio_bytes = await audio_file.read()
        if not audio_bytes:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Audio file is empty.",
            )

        return await _stt_service.transcribe(
            audio_bytes=audio_bytes,
            exam_session_id=exam_session_id,
            language=language,
        )

    except HTTPException:
        raise
    except ValueError as exc:
        logger.error(f"[SpeechAPI] STT error: {exc}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=str(exc),
        )
    except Exception as exc:
        logger.exception(f"[SpeechAPI] Unexpected STT error: {exc}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Transcription failed unexpectedly.",
        )


@router.post(
    "/synthesize",
    response_model=TextToSpeechResponse,
    summary="Text to Speech",
    description=(
        "Convert AI question text to audio for playback in the student's browser. "
        "Returns base64-encoded MP3 or WAV audio."
    ),
    responses={
        200: {"description": "Audio returned as base64"},
        422: {"description": "Validation error in request body"},
        500: {"model": ErrorResponse, "description": "TTS synthesis failed"},
    },
)
async def text_to_speech(request: TextToSpeechRequest) -> TextToSpeechResponse:
    """
    Synthesize text to speech audio.

    Flow: Backend → POST /ai/speech/synthesize → base64 audio → Frontend plays it
    """
    try:
        logger.info(
            f"POST /speech/synthesize | text_len={len(request.text)} | "
            f"lang={request.language}"
        )
        return await _tts_service.synthesize(request)
    except ValueError as exc:
        logger.error(f"[SpeechAPI] TTS error: {exc}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=str(exc),
        )
    except Exception as exc:
        logger.exception(f"[SpeechAPI] Unexpected TTS error: {exc}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Speech synthesis failed unexpectedly.",
        )
