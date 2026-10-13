"""
AIVES AI Service - Interview Service
Orchestrates the viva interview flow:
  AI generates question → Student answers → STT → Follow-up decision → Next question.
"""

from loguru import logger

from app.core.ai_client import get_ai_client
from app.models.request import GenerateQuestionRequest, GenerateFollowUpRequest
from app.models.response import GenerateQuestionResponse, GenerateFollowUpResponse
from app.services.question_service import QuestionService
from app.services.followup_service import FollowUpService


class InterviewService:
    """
    High-level orchestrator for the AI Viva interview flow.
    Delegates to QuestionService and FollowUpService.
    Can be extended to maintain session state (e.g., Redis) in future sprints.
    """

    def __init__(self) -> None:
        ai_client = get_ai_client()
        self.question_service = QuestionService(ai_client=ai_client)
        self.followup_service = FollowUpService(ai_client=ai_client)

    async def get_next_question(
        self, request: GenerateQuestionRequest
    ) -> GenerateQuestionResponse:
        """
        Get the next interview question for the student.
        This is called by the Backend when it's time to ask a new question.
        """
        logger.info(
            f"[InterviewService] Getting question {request.question_index} "
            f"for session {request.exam_session_id}"
        )
        return await self.question_service.generate_question(request)

    async def get_follow_up(
        self, request: GenerateFollowUpRequest
    ) -> GenerateFollowUpResponse:
        """
        Analyse the student's answer and return a follow-up question if needed.
        """
        logger.info(
            f"[InterviewService] Evaluating follow-up for session {request.exam_session_id}"
        )
        return await self.followup_service.generate_followup(request)
