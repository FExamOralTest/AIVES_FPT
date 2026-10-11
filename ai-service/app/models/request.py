"""
AIVES AI Service - Request Models
Pydantic schemas for all incoming API requests from the Backend.
"""

# NOTE: ProcessMarkdownRequest lives at the bottom of this file.
# It depends on DocumentContext defined above.

from typing import Annotated, List, Literal, Optional
from pydantic import BaseModel, Field, StringConstraints, model_validator


NonEmptyText = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1)]


# ─────────────────────────────────────────────
#  Shared / Common
# ─────────────────────────────────────────────


class DocumentContext(BaseModel):
    """Represents a .md document linked to a Course."""

    document_id: str = Field(..., description="UUID of the MarkdownDocument")
    title: str = Field(..., description="Document title")
    content: str = Field(..., description="Full markdown content of the document")
    document_type: str = Field(
        ..., description="'course_content' | 'learning_outcomes'"
    )


# ─────────────────────────────────────────────
#  Interview - Generate Question
# ─────────────────────────────────────────────


class GenerateQuestionRequest(BaseModel):
    """
    Request to generate the next interview question for a Student.
    Sent by Backend at the start of a session or after each answered question.
    """

    exam_session_id: str = Field(..., description="UUID of the ExamSession")
    exam_id: str = Field(..., description="UUID of the Exam")
    course_name: NonEmptyText = Field(..., description="Name of the course being examined")
    documents: List[DocumentContext] = Field(
        ...,
        min_length=2,
        max_length=2,
        description="One course_content document and one learning_outcomes document",
    )
    question_index: int = Field(
        default=1,
        ge=1,
        description="Which question number this is (1-based)",
    )
    total_questions: int = Field(
        default=5, ge=1, description="Total number of questions in this exam"
    )
    already_asked_topics: List[NonEmptyText] = Field(
        default_factory=list,
        description="Topics already covered to avoid repetition",
    )
    already_asked_questions: List[NonEmptyText] = Field(
        default_factory=list, description="Previous question texts to avoid repetition"
    )
    question_type: Optional[Literal["conceptual", "application", "analysis"]] = Field(
        default=None, description="Requested type; otherwise rotate types across questions"
    )
    difficulty: Literal["easy", "medium", "hard"] = Field(default="medium")

    @model_validator(mode="after")
    def validate_generation_context(self) -> "GenerateQuestionRequest":
        if self.question_index > self.total_questions:
            raise ValueError("question_index must not exceed total_questions")
        if {doc.document_type for doc in self.documents} != {
            "course_content",
            "learning_outcomes",
        }:
            raise ValueError(
                "Provide exactly one course_content and one learning_outcomes document"
            )
        if any(not doc.content.strip() or not doc.title.strip() for doc in self.documents):
            raise ValueError("Document titles and contents must not be blank")
        return self


# ─────────────────────────────────────────────
#  Interview - Generate Follow-up Question
# ─────────────────────────────────────────────


class GenerateFollowUpRequest(BaseModel):
    """
    Request to generate a follow-up / probing question based on
    the student's previous answer.
    """

    exam_session_id: str = Field(..., description="UUID of the ExamSession")
    course_name: str = Field(..., description="Name of the course")
    documents: List[DocumentContext] = Field(
        ..., description="List of .md documents for context"
    )
    original_question: str = Field(..., description="The main question that was asked")
    student_answer: str = Field(
        ..., description="Student's answer (from STT transcript)"
    )
    question_index: int = Field(default=1, ge=1, description="Current question number")


# ─────────────────────────────────────────────
#  Speech - Speech-to-Text
# ─────────────────────────────────────────────


class SpeechToTextRequest(BaseModel):
    """
    Request to transcribe audio to text.
    The audio file is sent as multipart/form-data (see API router).
    This model is used for JSON metadata accompanying the audio.
    """

    exam_session_id: str = Field(..., description="UUID of the ExamSession")
    language: str = Field(default="vi-VN", description="BCP-47 language code")


# ─────────────────────────────────────────────
#  Speech - Text-to-Speech
# ─────────────────────────────────────────────


class TextToSpeechRequest(BaseModel):
    """Request to convert AI question text into audio."""

    text: str = Field(..., description="Text to convert to speech")
    language: str = Field(default="vi-VN", description="BCP-47 language code")
    voice: Optional[str] = Field(
        default=None, description="Voice name (provider-specific)"
    )


# ─────────────────────────────────────────────
#  Evaluation - Score Answer
# ─────────────────────────────────────────────


class QuestionAnswerPair(BaseModel):
    """A single Q&A pair from the interview session."""

    question_id: str = Field(..., description="UUID of the Question")
    question_text: str = Field(..., description="The question that was asked")
    student_answer: str = Field(..., description="Student's transcribed answer")
    is_follow_up: bool = Field(default=False, description="Whether this was a follow-up")


class EvaluateAnswerRequest(BaseModel):
    """
    Request to score a single question-answer pair.
    Called after each question is answered.
    """

    exam_session_id: str = Field(..., description="UUID of the ExamSession")
    course_name: str = Field(..., description="Name of the course")
    documents: List[DocumentContext] = Field(
        ..., description="Course content + learning outcomes for scoring context"
    )
    question_answer: QuestionAnswerPair = Field(
        ..., description="The Q&A pair to evaluate"
    )
    max_score: float = Field(
        default=10.0, ge=0, description="Maximum score per question"
    )


class EvaluateSessionRequest(BaseModel):
    """
    Request to evaluate an entire exam session at once.
    Used at the end of the session to generate the full Result.
    """

    exam_session_id: str = Field(..., description="UUID of the ExamSession")
    student_id: str = Field(..., description="UUID of the Student")
    course_name: str = Field(..., description="Name of the course")
    documents: List[DocumentContext] = Field(
        ..., description="Course content + learning outcomes"
    )
    question_answers: List[QuestionAnswerPair] = Field(
        ..., description="All Q&A pairs from the session"
    )
    max_score_per_question: float = Field(
        default=10.0, ge=0, description="Max score per question"
    )


# ─────────────────────────────────────────────
#  Markdown Processing
# ─────────────────────────────────────────────


class ProcessMarkdownRequest(BaseModel):
    """
    Request to explicitly process and normalise 2 .md documents
    (course_content + learning_outcomes) into a structured AI context.

    Clients (Backend) can call POST /ai/markdown/process to:
      - Validate that both required document types are present.
      - Receive the assembled context string used in AI prompts.
      - Inspect extracted headings, LOs, and topics for debugging.
    """

    course_name: str = Field(..., description="Name of the course")
    documents: List[DocumentContext] = Field(
        ...,
        description="Exactly 2 documents: one 'course_content', one 'learning_outcomes'",
        min_length=1,
    )
