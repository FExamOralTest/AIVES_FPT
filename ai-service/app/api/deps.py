"""
AIVES AI Service - Shared FastAPI dependencies.

Service instances are provided here (rather than constructed at module import)
so that a provider misconfiguration surfaces as a request-time error and does
not prevent the application from importing.
"""

from functools import lru_cache

from app.services.evaluation_service import EvaluationService
from app.services.interview_service import InterviewService
from app.services.markdown_processor import MarkdownProcessor
from app.services.question_bank_service import QuestionBankService
from app.services.speech_service import SpeechToTextService
from app.services.tts_service import TextToSpeechService


@lru_cache()
def get_interview_service() -> InterviewService:
    return InterviewService()


@lru_cache()
def get_evaluation_service() -> EvaluationService:
    return EvaluationService()


@lru_cache()
def get_speech_service() -> SpeechToTextService:
    return SpeechToTextService()


@lru_cache()
def get_tts_service() -> TextToSpeechService:
    return TextToSpeechService()


@lru_cache()
def get_markdown_processor() -> MarkdownProcessor:
    return MarkdownProcessor()


@lru_cache()
def get_question_bank_service() -> QuestionBankService:
    return QuestionBankService()
