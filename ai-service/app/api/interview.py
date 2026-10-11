"""
AIVES AI Service - Interview API Router
Endpoints for AI question generation and follow-up questions.
Called by ASP.NET Core Backend (not directly by Frontend - see BR-14).
"""

import httpx
from fastapi import APIRouter, HTTPException, status
from loguru import logger

from app.models.request import GenerateQuestionRequest, GenerateFollowUpRequest
from app.models.response import GenerateQuestionResponse, GenerateFollowUpResponse, ErrorResponse
from app.services.interview_service import InterviewService
from app.services.question_service import InvalidQuestionContext, QuestionGenerationError

router = APIRouter(prefix="/interview", tags=["Interview"])
_service = InterviewService()


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
        422: {"description": "Invalid request, course documents or exhausted topics"},
        502: {"description": "AI provider failed or returned invalid questions"},
        500: {"model": ErrorResponse, "description": "AI generation failed"},
    },
)
async def generate_question(request: GenerateQuestionRequest) -> GenerateQuestionResponse:
    """
    Generate the next interview question.

    Flow: Backend → POST /ai/interview/question → AI → question text + metadata
    """
    try:
        logger.info(
            f"POST /interview/question | session={request.exam_session_id} "
            f"q={request.question_index}/{request.total_questions}"
        )
        return await _service.get_next_question(request)
    except InvalidQuestionContext as exc:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=str(exc),
        ) from exc
    except QuestionGenerationError as exc:
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail=str(exc),
        ) from exc
    except httpx.HTTPError as exc:
        logger.warning("[InterviewAPI] Question generation provider request failed")
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail="The AI provider is unavailable. Please retry later.",
        ) from exc
    except ValueError as exc:
        logger.error(f"[InterviewAPI] Question generation error: {exc}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Question generation failed due to a service configuration error.",
        )
    except Exception as exc:
        logger.exception(f"[InterviewAPI] Unexpected error: {exc}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="An unexpected error occurred while generating the question.",
        )


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
        422: {"description": "Validation error in request body"},
        500: {"model": ErrorResponse, "description": "AI generation failed"},
    },
)
async def generate_followup(request: GenerateFollowUpRequest) -> GenerateFollowUpResponse:
    """
    Generate a follow-up question based on the student's answer.

    Flow: Backend → POST /ai/interview/follow-up → AI → follow_up_question or skip
    """
    try:
        logger.info(
            f"POST /interview/follow-up | session={request.exam_session_id}"
        )
        return await _service.get_follow_up(request)
    except ValueError as exc:
        logger.error(f"[InterviewAPI] Follow-up generation error: {exc}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=str(exc),
        )
    except Exception as exc:
        logger.exception(f"[InterviewAPI] Unexpected error: {exc}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="An unexpected error occurred while generating follow-up.",
        )
