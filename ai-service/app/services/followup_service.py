"""
AIVES AI Service - Follow-up Service
Generates probing / follow-up questions based on student's answers.
"""

import json
from pathlib import Path

from loguru import logger

from app.core.ai_client import BaseAIClient, get_ai_client
from app.models.ai_output import FollowUpOutput, parse_ai_output
from app.models.request import GenerateFollowUpRequest
from app.models.response import GenerateFollowUpResponse
from app.services.document_context import build_document_context

_PROMPT_PATH = Path(__file__).parent.parent / "prompts" / "followup_prompt.txt"
_FOLLOWUP_PROMPT_TEMPLATE: str = _PROMPT_PATH.read_text(encoding="utf-8")


class FollowUpService:
    """
    Service responsible for generating follow-up questions when
    the student's answer is incomplete or needs clarification.
    """

    def __init__(self, ai_client: BaseAIClient | None = None) -> None:
        self.ai_client = ai_client or get_ai_client()

    async def generate_followup(
        self, request: GenerateFollowUpRequest
    ) -> GenerateFollowUpResponse:
        """
        Analyse a student's answer and generate a follow-up question if needed.

        Args:
            request: GenerateFollowUpRequest with the Q&A context.

        Returns:
            GenerateFollowUpResponse with follow-up question and reasoning.
        """
        logger.info(
            f"[FollowUpService] Generating follow-up for session {request.exam_session_id}"
        )

        document_context = build_document_context(
            request.documents, source="FollowUpService"
        )

        system_prompt = _FOLLOWUP_PROMPT_TEMPLATE
        system_prompt = system_prompt.replace("{course_name}", request.course_name)
        system_prompt = system_prompt.replace("{document_context}", document_context)
        system_prompt = system_prompt.replace("{original_question}", request.original_question)
        system_prompt = system_prompt.replace("{student_answer}", request.student_answer)
        system_prompt = system_prompt.replace("{question_index}", str(request.question_index))

        user_message = json.dumps({"task": "generate_follow_up"})

        raw_response = await self.ai_client.complete(system_prompt, user_message)

        try:
            data = parse_ai_output(raw_response, FollowUpOutput)
            return GenerateFollowUpResponse(
                success=True,
                **data.model_dump(),
            )
        except ValueError as exc:
            logger.warning("[FollowUpService] Rejected invalid follow-up response.")
            raise ValueError("AI returned an invalid follow-up response.") from exc
