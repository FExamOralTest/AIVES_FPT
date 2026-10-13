"""
AIVES AI Service - Follow-up Service
Generates probing / follow-up questions based on student's answers.
"""

import json
from pathlib import Path
from typing import List

from loguru import logger

from app.core.ai_client import BaseAIClient, get_ai_client
from app.models.request import GenerateFollowUpRequest, DocumentContext
from app.models.response import GenerateFollowUpResponse
from app.services.markdown_processor import MarkdownProcessor

_PROMPT_PATH = Path(__file__).parent.parent / "prompts" / "followup_prompt.txt"
_FOLLOWUP_PROMPT_TEMPLATE: str = _PROMPT_PATH.read_text(encoding="utf-8")

# Shared MarkdownProcessor instance (stateless, safe to reuse)
_md_processor = MarkdownProcessor()


def _build_document_context(documents: List[DocumentContext]) -> str:
    """
    Process and normalise .md documents using MarkdownProcessor.
    Falls back to raw content joining if processing fails.
    """
    result = _md_processor.process(documents)
    if result.is_valid and result.context_string:
        return result.context_string

    logger.warning(
        "[FollowUpService] MarkdownProcessor validation failed; "
        f"errors={result.validation_errors}. Using raw content fallback."
    )
    parts = []
    for doc in documents:
        parts.append(
            f"### {doc.document_type.replace('_', ' ').title()} - {doc.title}\n\n{doc.content}"
        )
    return "\n\n---\n\n".join(parts)


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

        document_context = _build_document_context(request.documents)

        system_prompt = _FOLLOWUP_PROMPT_TEMPLATE
        system_prompt = system_prompt.replace("{course_name}", request.course_name)
        system_prompt = system_prompt.replace("{document_context}", document_context)
        system_prompt = system_prompt.replace("{original_question}", request.original_question)
        system_prompt = system_prompt.replace("{student_answer}", request.student_answer)
        system_prompt = system_prompt.replace("{question_index}", str(request.question_index))

        user_message = (
            f"Hãy phân tích câu trả lời của sinh viên và quyết định "
            f"có cần câu hỏi follow-up không."
        )

        raw_response = await self.ai_client.complete(system_prompt, user_message)

        try:
            data = json.loads(raw_response)
            return GenerateFollowUpResponse(
                success=True,
                should_follow_up=data.get("should_follow_up", True),
                follow_up_question=data.get("follow_up_question", ""),
                reason=data.get("reason"),
            )
        except (json.JSONDecodeError, KeyError) as exc:
            logger.error(
                f"[FollowUpService] Failed to parse AI response: {raw_response!r}"
            )
            raise ValueError(f"AI returned invalid response format: {exc}") from exc
