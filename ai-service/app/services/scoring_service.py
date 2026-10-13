"""
AIVES AI Service - Scoring / Evaluation Service
Scores individual Q&A pairs using AI based on course content.
"""

import json
from pathlib import Path
from typing import List

from loguru import logger

from app.core.ai_client import BaseAIClient, get_ai_client
from app.models.request import EvaluateAnswerRequest, EvaluateSessionRequest, DocumentContext
from app.models.response import (
    EvaluateAnswerResponse,
    EvaluateSessionResponse,
    QuestionScoreDetail,
)
from app.services.markdown_processor import MarkdownProcessor

_PROMPT_PATH = Path(__file__).parent.parent / "prompts" / "scoring_prompt.txt"
_SCORING_PROMPT_TEMPLATE: str = _PROMPT_PATH.read_text(encoding="utf-8")

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
        "[ScoringService] MarkdownProcessor validation failed; "
        f"errors={result.validation_errors}. Using raw content fallback."
    )
    parts = []
    for doc in documents:
        parts.append(
            f"### {doc.document_type.replace('_', ' ').title()} - {doc.title}\n\n{doc.content}"
        )
    return "\n\n---\n\n".join(parts)


class ScoringService:
    """
    Service responsible for AI-based scoring and feedback generation.
    Supports both single Q&A evaluation and full session evaluation.
    """

    def __init__(self, ai_client: BaseAIClient | None = None) -> None:
        self.ai_client = ai_client or get_ai_client()

    async def evaluate_answer(
        self, request: EvaluateAnswerRequest
    ) -> EvaluateAnswerResponse:
        """
        Score a single question-answer pair.

        Args:
            request: EvaluateAnswerRequest with the Q&A and course context.

        Returns:
            EvaluateAnswerResponse with score, feedback, strengths, and weaknesses.
        """
        logger.info(
            f"[ScoringService] Evaluating answer for question "
            f"{request.question_answer.question_id} in session {request.exam_session_id}"
        )

        document_context = _build_document_context(request.documents)
        qa = request.question_answer

        system_prompt = _SCORING_PROMPT_TEMPLATE
        system_prompt = system_prompt.replace("{course_name}", request.course_name)
        system_prompt = system_prompt.replace("{document_context}", document_context)
        system_prompt = system_prompt.replace("{question_text}", qa.question_text)
        system_prompt = system_prompt.replace("{student_answer}", qa.student_answer)
        system_prompt = system_prompt.replace("{max_score}", str(request.max_score))

        user_message = (
            f"Hãy chấm điểm câu trả lời của sinh viên cho câu hỏi này "
            f"với điểm tối đa là {request.max_score}."
        )

        raw_response = await self.ai_client.complete(system_prompt, user_message)

        try:
            data = json.loads(raw_response)
            score = float(data.get("score", 0))
            # Clamp score to valid range
            score = max(0.0, min(score, request.max_score))

            return EvaluateAnswerResponse(
                success=True,
                question_id=qa.question_id,
                score=score,
                max_score=request.max_score,
                feedback=data.get("feedback", ""),
                strengths=data.get("strengths", []),
                weaknesses=data.get("weaknesses", []),
                missing_content=data.get("missing_content", []),
            )
        except (json.JSONDecodeError, KeyError, ValueError) as exc:
            logger.error(
                f"[ScoringService] Failed to parse AI response: {raw_response!r}"
            )
            raise ValueError(f"AI returned invalid scoring format: {exc}") from exc

    async def evaluate_session(
        self, request: EvaluateSessionRequest
    ) -> EvaluateSessionResponse:
        """
        Evaluate all Q&A pairs in an exam session and generate a full Result.
        Calls evaluate_answer for each question, then aggregates the scores.

        Args:
            request: EvaluateSessionRequest with all Q&A pairs.

        Returns:
            EvaluateSessionResponse with total score, breakdown, and overall feedback.
        """
        logger.info(
            f"[ScoringService] Evaluating full session {request.exam_session_id} "
            f"({len(request.question_answers)} questions)"
        )

        question_scores: List[QuestionScoreDetail] = []
        total_score = 0.0
        max_total = len(request.question_answers) * request.max_score_per_question

        for qa in request.question_answers:
            single_request = EvaluateAnswerRequest(
                exam_session_id=request.exam_session_id,
                course_name=request.course_name,
                documents=request.documents,
                question_answer=qa,
                max_score=request.max_score_per_question,
            )
            result = await self.evaluate_answer(single_request)
            total_score += result.score

            question_scores.append(
                QuestionScoreDetail(
                    question_id=qa.question_id,
                    question_text=qa.question_text,
                    student_answer=qa.student_answer,
                    score=result.score,
                    max_score=result.max_score,
                    feedback=result.feedback,
                    strengths=result.strengths,
                    weaknesses=result.weaknesses,
                    missing_content=result.missing_content,
                )
            )

        percentage = (total_score / max_total * 100) if max_total > 0 else 0.0

        # Generate overall feedback based on percentage
        if percentage >= 80:
            overall_feedback = (
                "Sinh viên thể hiện sự hiểu biết vững vàng về môn học. "
                "Câu trả lời đầy đủ, chính xác và có ví dụ minh hoạ tốt."
            )
        elif percentage >= 60:
            overall_feedback = (
                "Sinh viên nắm được các kiến thức cơ bản của môn học. "
                "Cần bổ sung thêm ví dụ thực tế và đào sâu hơn vào một số chủ đề."
            )
        elif percentage >= 40:
            overall_feedback = (
                "Sinh viên còn nhiều khoảng trống kiến thức. "
                "Cần ôn tập lại nội dung môn học và learning outcomes."
            )
        else:
            overall_feedback = (
                "Sinh viên chưa nắm vững kiến thức môn học. "
                "Cần học lại toàn bộ nội dung và tham khảo thêm tài liệu."
            )

        return EvaluateSessionResponse(
            success=True,
            exam_session_id=request.exam_session_id,
            total_score=round(total_score, 2),
            max_total_score=round(max_total, 2),
            percentage=round(percentage, 2),
            overall_feedback=overall_feedback,
            question_scores=question_scores,
            status="PendingReview",
        )
