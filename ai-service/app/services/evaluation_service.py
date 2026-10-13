"""
AIVES AI Service - Evaluation Service
Top-level service combining scoring + feedback generation.
Re-exports from ScoringService for clean API layer access.
"""

from app.services.scoring_service import ScoringService
from app.models.request import EvaluateAnswerRequest, EvaluateSessionRequest
from app.models.response import EvaluateAnswerResponse, EvaluateSessionResponse


class EvaluationService:
    """
    Evaluation service for the Teacher Review workflow.
    Wraps ScoringService; can be extended with feedback caching or
    post-processing logic in future sprints.
    """

    def __init__(self) -> None:
        self.scoring = ScoringService()

    async def evaluate_answer(
        self, request: EvaluateAnswerRequest
    ) -> EvaluateAnswerResponse:
        """Score a single question-answer pair."""
        return await self.scoring.evaluate_answer(request)

    async def evaluate_session(
        self, request: EvaluateSessionRequest
    ) -> EvaluateSessionResponse:
        """
        Evaluate a complete exam session.
        Result is always returned with status='PendingReview' for Teacher to approve.
        """
        return await self.scoring.evaluate_session(request)
