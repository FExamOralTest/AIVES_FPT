"""
AIVES AI Service - Evaluation API Router
Endpoints for AI scoring of student answers (single Q&A + full session).
Result is always PendingReview until Teacher approves it (see BR-09, BR-10).
"""

from fastapi import APIRouter, Depends, HTTPException
from loguru import logger

from app.api.deps import get_evaluation_service
from app.api.errors import to_http_exception
from app.models.request import EvaluateAnswerRequest, EvaluateSessionRequest
from app.models.response import (
    ErrorResponse,
    EvaluateAnswerResponse,
    EvaluateSessionResponse,
)
from app.services.evaluation_service import EvaluationService

router = APIRouter(prefix="/evaluate", tags=["Evaluation"])


@router.post(
    "/answer",
    response_model=EvaluateAnswerResponse,
    summary="Evaluate Single Answer",
    description=(
        "Score a single question-answer pair using AI. "
        "When an approved rubric is included, also judge correctness, errors, and omissions "
        "for each transcript sentence and each criterion, citing only the transcript. "
    ),
    responses={
        200: {"description": "Answer evaluated successfully"},
        401: {"description": "Missing or invalid X-AI-Service-Key"},
        422: {"description": "Validation error"},
        500: {"model": ErrorResponse, "description": "Evaluation failed"},
    },
)
async def evaluate_answer(
    request: EvaluateAnswerRequest,
    service: EvaluationService = Depends(get_evaluation_service),
) -> EvaluateAnswerResponse:
    """
    Evaluate a single Q&A pair.

    Flow: Backend → POST /ai/evaluate/answer → score + feedback
    """
    try:
        logger.info(
            f"POST /evaluate/answer | session={request.exam_session_id} | "
            f"question={request.question_answer.question_id}"
        )
        return await service.evaluate_answer(request)
    except HTTPException:
        raise
    except Exception as exc:
        logger.exception(f"[EvalAPI] Evaluation error: {exc}")
        raise to_http_exception(exc, context="Answer evaluation") from exc


@router.post(
    "/session",
    response_model=EvaluateSessionResponse,
    summary="Evaluate Full Exam Session",
    description=(
        "Evaluate all Q&A pairs in an exam session at once. "
        "Returns total score, percentage, per-question breakdown, and overall feedback. "
        "With an approved rubric, overall feedback is assembled from the per-answer analysis. "
        "Result status is always 'PendingReview' until Teacher approves (BR-09, BR-10)."
    ),
    responses={
        200: {"description": "Session evaluated; result is PendingReview"},
        401: {"description": "Missing or invalid X-AI-Service-Key"},
        422: {"description": "Validation error"},
        500: {"model": ErrorResponse, "description": "Session evaluation failed"},
    },
)
async def evaluate_session(
    request: EvaluateSessionRequest,
    service: EvaluationService = Depends(get_evaluation_service),
) -> EvaluateSessionResponse:
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
        return await service.evaluate_session(request)
    except HTTPException:
        raise
    except Exception as exc:
        logger.exception(f"[EvalAPI] Session evaluation error: {exc}")
        raise to_http_exception(exc, context="Session evaluation") from exc
