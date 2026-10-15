"""
AIVES AI Service - Interview API Router
Endpoints for AI question generation and follow-up questions.
Called by ASP.NET Core Backend (not directly by Frontend - see BR-14).
"""

from fastapi import APIRouter, Depends, HTTPException
from loguru import logger

from app.api.deps import get_interview_service, get_question_bank_service
from app.api.errors import to_http_exception
from app.models.question_bank import GenerateQuestionsRequest, GenerateQuestionsResponse
from app.models.request import GenerateFollowUpRequest, GenerateQuestionRequest
from app.models.response import (
    ErrorResponse,
    GenerateFollowUpResponse,
    GenerateQuestionResponse,
)
from app.services.interview_service import InterviewService
from app.services.question_bank_service import QuestionBankService

router = APIRouter(prefix="/interview", tags=["Interview"])


@router.post(
    "/question",
    response_model=GenerateQuestionResponse,
    summary="Generate Interview Question",
    description=(
        "Generate the next viva interview question for a student based on "
        "course .md documents and learning outcomes. "
        "Called by Backend at the start of a session or after each answered question."
    ),
    responses={
        200: {"description": "Question generated successfully"},
        401: {"description": "Missing or invalid X-AI-Service-Key"},
        422: {"description": "Invalid request, course documents or exhausted topics"},
        502: {"description": "AI provider failed or returned invalid questions"},
        500: {"model": ErrorResponse, "description": "AI generation failed"},
    },
)
async def generate_question(
    request: GenerateQuestionRequest,
    service: InterviewService = Depends(get_interview_service),
) -> GenerateQuestionResponse:
    """
    Generate the next interview question.

    Flow: Backend → POST /ai/interview/question → AI → question text + metadata
    """
    try:
        logger.info(
            f"POST /interview/question | session={request.exam_session_id} "
            f"q={request.question_index}/{request.total_questions}"
        )
        return await service.get_next_question(request)
    except HTTPException:
        raise
    except Exception as exc:
        logger.exception(f"[InterviewAPI] Question generation error: {exc}")
        raise to_http_exception(exc, context="Question generation") from exc


@router.post(
    "/follow-up",
    response_model=GenerateFollowUpResponse,
    summary="Generate Follow-up Question",
    description=(
        "Analyse the student's answer and generate a probing follow-up question "
        "if the answer is incomplete or needs clarification. "
        "Returns should_follow_up=false if the answer was sufficient."
    ),
    responses={
        200: {"description": "Follow-up decision + question returned"},
        401: {"description": "Missing or invalid X-AI-Service-Key"},
        422: {"description": "Validation error in request body"},
        500: {"model": ErrorResponse, "description": "AI generation failed"},
    },
)
async def generate_followup(
    request: GenerateFollowUpRequest,
    service: InterviewService = Depends(get_interview_service),
) -> GenerateFollowUpResponse:
    """
    Generate a follow-up question based on the student's answer.

    Flow: Backend → POST /ai/interview/follow-up → AI → follow_up_question or skip
    """
    try:
        logger.info(f"POST /interview/follow-up | session={request.exam_session_id}")
        return await service.get_follow_up(request)
    except HTTPException:
        raise
    except Exception as exc:
        logger.exception(f"[InterviewAPI] Follow-up generation error: {exc}")
        raise to_http_exception(exc, context="Follow-up generation") from exc


@router.post(
    "/generate-questions",
    response_model=GenerateQuestionsResponse,
    summary="(Deprecated) Generate Questions from Markdown",
    description=(
        "**Deprecated.** Use `POST /ai/questions/generate` instead.\n\n"
        "Kept temporarily for backwards compatibility: it accepts the same "
        "request body as `/ai/questions/generate` and delegates to the same "
        "QuestionBankService. It will be removed once no client uses it."
    ),
    deprecated=True,
    responses={
        200: {"description": "Questions generated successfully"},
        401: {"description": "Missing or invalid X-AI-Service-Key"},
        422: {"description": "Invalid documents or generation parameters"},
        502: {"description": "Provider unavailable or invalid response"},
    },
)
async def generate_questions_legacy(
    payload: GenerateQuestionsRequest,
    service: QuestionBankService = Depends(get_question_bank_service),
) -> GenerateQuestionsResponse:
    """
    Deprecated alias for /ai/questions/generate.

    Delegates to QuestionBankService so behaviour matches the canonical
    endpoint exactly.
    """
    logger.warning(
        "Deprecated endpoint POST /ai/interview/generate-questions called; "
        "use POST /ai/questions/generate instead."
    )
    try:
        return await service.generate(payload)
    except HTTPException:
        raise
    except Exception as exc:
        logger.exception("[InterviewAPI] Legacy generate-questions error: %s", exc)
        raise to_http_exception(exc, context="Question generation") from exc
