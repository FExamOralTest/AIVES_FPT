"""
AIVES AI Service - Response Models
Pydantic schemas for all outgoing API responses to the Backend.
"""

from typing import Annotated, List, Literal, Optional
from pydantic import BaseModel, Field, StringConstraints


NonEmptyText = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1)]


# ─────────────────────────────────────────────
#  Shared / Common
# ─────────────────────────────────────────────


class BaseResponse(BaseModel):
    """Base wrapper for all AI service responses."""

    success: bool = Field(default=True)
    message: Optional[str] = Field(default=None)


class ErrorResponse(BaseModel):
    """Standard error response."""

    success: bool = False
    error: str
    detail: Optional[str] = None


# ─────────────────────────────────────────────
#  Interview - Generate Question Response
# ─────────────────────────────────────────────


class GenerateQuestionResponse(BaseResponse):
    """Response from AI question generation."""

    question: NonEmptyText = Field(..., description="The AI-generated interview question")
    question_type: Literal["conceptual", "application", "analysis"] = Field(
        ...,
        description="Type: 'conceptual' | 'application' | 'analysis'",
    )
    difficulty: Literal["easy", "medium", "hard"] = Field(
        ..., description="Difficulty: 'easy' | 'medium' | 'hard'"
    )
    expected_keywords: List[NonEmptyText] = Field(
        ...,
        min_length=1,
        description="Key concepts expected in a good answer",
    )
    topic: NonEmptyText = Field(
        ..., description="Topic/chapter this question covers"
    )
    learning_outcome: NonEmptyText = Field(
        ..., description="Learning outcome assessed, copied from the supplied document"
    )


# ─────────────────────────────────────────────
#  Interview - Follow-up Question Response
# ─────────────────────────────────────────────


class GenerateFollowUpResponse(BaseResponse):
    """Response containing a follow-up/probing question."""

    follow_up_question: str = Field(
        ..., description="The AI-generated follow-up question"
    )
    reason: Optional[str] = Field(
        default=None,
        description="Why this follow-up was generated (for Teacher review)",
    )
    should_follow_up: bool = Field(
        default=True,
        description="Whether a follow-up is needed (False = answer was sufficient)",
    )


# ─────────────────────────────────────────────
#  Speech - Speech-to-Text Response
# ─────────────────────────────────────────────


class SpeechToTextResponse(BaseResponse):
    """Response from STT transcription."""

    transcript: str = Field(..., description="Transcribed text from student's audio")
    confidence: Optional[float] = Field(
        default=None,
        ge=0.0,
        le=1.0,
        description="Confidence score (0.0 to 1.0)",
    )
    language_detected: Optional[str] = Field(
        default=None, description="Detected language BCP-47 code"
    )


# ─────────────────────────────────────────────
#  Speech - Text-to-Speech Response
# ─────────────────────────────────────────────


class TextToSpeechResponse(BaseResponse):
    """Response from TTS conversion."""

    audio_base64: str = Field(
        ..., description="Base64-encoded audio data (WAV/MP3)"
    )
    audio_format: str = Field(default="mp3", description="Audio format: 'mp3' | 'wav'")
    duration_seconds: Optional[float] = Field(
        default=None, description="Duration of audio in seconds"
    )


# ─────────────────────────────────────────────
#  Evaluation - Single Question Score Response
# ─────────────────────────────────────────────


class EvaluateAnswerResponse(BaseResponse):
    """Response from evaluating a single Q&A pair."""

    question_id: str = Field(..., description="UUID of the evaluated Question")
    score: float = Field(..., ge=0, description="Score awarded for this answer")
    max_score: float = Field(..., ge=0, description="Maximum possible score")
    feedback: str = Field(..., description="AI-generated textual feedback")
    strengths: List[str] = Field(
        default_factory=list, description="What the student did well"
    )
    weaknesses: List[str] = Field(
        default_factory=list, description="Areas needing improvement"
    )
    missing_content: List[str] = Field(
        default_factory=list,
        description="Key content missing from the student's answer",
    )


# ─────────────────────────────────────────────
#  Evaluation - Full Session Score Response
# ─────────────────────────────────────────────


class QuestionScoreDetail(BaseModel):
    """Per-question score detail within a session evaluation."""

    question_id: str
    question_text: str
    student_answer: str
    score: float
    max_score: float
    feedback: str
    strengths: List[str] = Field(default_factory=list)
    weaknesses: List[str] = Field(default_factory=list)
    missing_content: List[str] = Field(default_factory=list)


class EvaluateSessionResponse(BaseResponse):
    """Response from evaluating a complete exam session."""

    exam_session_id: str = Field(..., description="UUID of the ExamSession")
    total_score: float = Field(..., ge=0, description="Total score for the session")
    max_total_score: float = Field(
        ..., ge=0, description="Maximum possible total score"
    )
    percentage: float = Field(
        ..., ge=0.0, le=100.0, description="Score as percentage"
    )
    overall_feedback: str = Field(
        ..., description="Overall AI feedback for the session"
    )
    question_scores: List[QuestionScoreDetail] = Field(
        ..., description="Per-question breakdown"
    )
    status: str = Field(
        default="PendingReview",
        description="Result status: always 'PendingReview' after AI evaluation",
    )


# ─────────────────────────────────────────────
#  Health Check
# ─────────────────────────────────────────────


class HealthResponse(BaseModel):
    """Health check response."""

    status: str = "ok"
    service: str = "aives-ai"
    version: str = "1.0.0"
    ai_provider: Optional[str] = None
    stt_provider: Optional[str] = None
    tts_provider: Optional[str] = None


# ─────────────────────────────────────────────
#  Markdown Processing
# ─────────────────────────────────────────────


class DocumentProcessingSummary(BaseModel):
    """Summary of a single processed .md document."""

    document_id: str = Field(..., description="UUID of the MarkdownDocument")
    title: str = Field(..., description="Document title")
    document_type: str = Field(..., description="'course_content' | 'learning_outcomes'")

    raw_char_count: int = Field(..., ge=0, description="Original character count")
    clean_char_count: int = Field(
        ..., ge=0, description="Character count after markdown cleaning"
    )
    heading_count: int = Field(..., ge=0, description="Number of headings extracted")
    headings: List[str] = Field(
        default_factory=list, description="Extracted heading texts (level-aware)"
    )
    learning_outcomes: List[str] = Field(
        default_factory=list,
        description="Extracted learning outcome items (LO doc only)",
    )
    key_topics: List[str] = Field(
        default_factory=list,
        description="Bullet/numbered list items extracted as key topics",
    )
    is_valid: bool = Field(..., description="Whether this document passed validation")
    validation_errors: List[str] = Field(
        default_factory=list,
        description="Per-document validation warnings/errors",
    )


class ProcessMarkdownResponse(BaseResponse):
    """
    Response from POST /ai/markdown/process.
    Returns the assembled context string + per-document metadata.
    """

    course_name: str = Field(..., description="Name of the course processed")
    is_valid: bool = Field(
        ..., description="True if all documents passed validation"
    )
    validation_errors: List[str] = Field(
        default_factory=list,
        description="Global validation errors (e.g. missing document type)",
    )
    documents: List[DocumentProcessingSummary] = Field(
        ..., description="Per-document processing summaries"
    )
    context_string: str = Field(
        ...,
        description=(
            "Fully assembled, normalised context string ready for AI prompts. "
            "Inserted as {document_context} in prompt templates."
        ),
    )
