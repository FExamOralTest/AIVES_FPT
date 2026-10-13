"""
AIVES AI Service - Evaluation API Router
Endpoints for AI scoring of student answers (single Q&A + full session).
Result is always PendingReview until Teacher approves it (see BR-09, BR-10).
"""

from fastapi import APIRouter, HTTPException, status
from loguru import logger

from app.models.request import EvaluateAnswerRequest, EvaluateSessionRequest
from app.models.response import EvaluateAnswerResponse, EvaluateSessionResponse, ErrorResponse
from app.services.evaluation_service import EvaluationService

router = APIRouter(prefix="/evaluate", tags=["Evaluation"])
_service = EvaluationService()


@router.post(
    "/answer",
    response_model=EvaluateAnswerResponse,
    summary="Evaluate Single Answer",
    description=(
        "Score a single question-answer pair using AI. "
        "Returns score, feedback, strengths, weaknesses, and missing content. "
        "Called after each question in the session (optional, can also batch at end)."
    ),
    responses={
        200: {"description": "Answer evaluated successfully"},
        422: {"description": "Validation error"},
        500: {"model": ErrorResponse, "description": "Evaluation failed"},
    },
)
async def evaluate_answer(request: EvaluateAnswerRequest) -> EvaluateAnswerResponse:
    """
    Evaluate a single Q&A pair.

    Flow: Backend → POST /ai/evaluate/answer → score + feedback
    """
    try:
        logger.info(
            f"POST /evaluate/answer | session={request.exam_session_id} | "
            f"question={request.question_answer.question_id}"
        )
        return await _service.evaluate_answer(request)
    except ValueError as exc:
        logger.error(f"[EvalAPI] Evaluation error: {exc}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=str(exc),
        )
    except Exception as exc:
        logger.exception(f"[EvalAPI] Unexpected error: {exc}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Evaluation failed unexpectedly.",
        )


@router.post(
    "/session",
    response_model=EvaluateSessionResponse,
    summary="Evaluate Full Exam Session",
    description=(
        "Evaluate all Q&A pairs in an exam session at once. "
        "Returns total score, percentage, per-question breakdown, and overall feedback. "
        "Result status is always 'PendingReview' until Teacher approves (BR-09, BR-10)."
    ),
    responses={
        200: {"description": "Session evaluated; result is PendingReview"},
        422: {"description": "Validation error"},
        500: {"model": ErrorResponse, "description": "Session evaluation failed"},
    },
)
async def evaluate_session(request: EvaluateSessionRequest) -> EvaluateSessionResponse:
    """
    Evaluate a complete exam session.

    Flow: Backend → POST /ai/evaluate/session → full result (PendingReview)
          → Backend stores Result → Teacher reviews → Teacher approves → Published
    """
    try:
        logger.info(
            f"POST /evaluate/session | session={request.exam_session_id} | "
            f"questions={len(request.question_answers)}"
        )
        return await _service.evaluate_session(request)
    except ValueError as exc:
        logger.error(f"[EvalAPI] Session evaluation error: {exc}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=str(exc),
        )
    except Exception as exc:
        logger.exception(f"[EvalAPI] Unexpected error: {exc}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Session evaluation failed unexpectedly.",
        )
