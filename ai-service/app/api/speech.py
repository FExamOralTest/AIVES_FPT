"""
AIVES AI Service - Speech API Router
Endpoints for Speech-to-Text and Text-to-Speech.
Called by ASP.NET Core Backend (not directly by Frontend - see BR-14).
"""

from fastapi import APIRouter, HTTPException, UploadFile, File, Form, status
from loguru import logger

from app.models.request import TextToSpeechRequest
from app.models.response import SpeechToTextResponse, TextToSpeechResponse, ErrorResponse
from app.services.speech_service import SpeechToTextService
from app.services.tts_service import TextToSpeechService

router = APIRouter(prefix="/speech", tags=["Speech"])
_stt_service = SpeechToTextService()
_tts_service = TextToSpeechService()


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
