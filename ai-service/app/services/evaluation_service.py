"""Score an answer from the rubric analysis alone. No weighted heuristic."""

from loguru import logger

from app.core.ai_client import BaseAIClient, get_model_name
from app.core.config import get_settings
from app.exceptions.ai_exceptions import (
    AIValidationError,
    AITimeoutError,
    MaxRetriesExceededError,
)
from app.models.request import EvaluateAnswerRequest, EvaluateSessionRequest
from app.models.response import EvaluateAnswerResponse, EvaluateSessionResponse, QuestionScoreDetail
from app.services.answer_evidence import SubmissionPart, build_submission_feedback
from app.services.feedback_analysis_service import FeedbackAnalysisService

SCHEMA_VERSION = "2.0"


class EvaluationService:
    """Award points from the rubric analysis: score = sum of awarded criterion scores."""

    def __init__(self, ai_client: BaseAIClient | None = None) -> None:
        self.analysis = FeedbackAnalysisService(ai_client)
        self.provider = get_settings().ai_provider
        self.model = get_model_name()

    async def evaluate_answer(self, request: EvaluateAnswerRequest) -> EvaluateAnswerResponse:
        if not request.question_answer.rubric:
            raise ValueError("A rubric is required for scoring.")

        try:
            analysis = await self.analysis.analyze(request)
        except (AIValidationError, AITimeoutError, MaxRetriesExceededError) as exc:
            logger.error(f"[EvaluationService] AI error: {exc}")
            raise ValueError(f"AI evaluation failed: {exc.message}") from exc

        score = round(sum(item.awarded_score for item in analysis.criterion_analyses), 4)
        return EvaluateAnswerResponse(
            success=True,
            question_id=request.question_answer.question_id,
            score=score,
            max_score=request.max_score,
            feedback=analysis.feedback,
            strengths=list(analysis.strengths),
            weaknesses=list(analysis.weaknesses),
            missing_content=list(analysis.missing_content),
            sentence_feedback=analysis.sentence_feedback,
            criterion_analyses=analysis.criterion_analyses,
            improvements=list(analysis.improvements),
            provider=self.provider,
            model=self.model,
            schema_version=SCHEMA_VERSION,
        )

    async def evaluate_session(self, request: EvaluateSessionRequest) -> EvaluateSessionResponse:
        question_scores: list[QuestionScoreDetail] = []
        parts: list[SubmissionPart] = []
        total_score = 0.0
        max_total = 0.0
        for pair in request.question_answers:
            max_score = pair.max_score if pair.max_score is not None else request.max_score_per_question
            max_total += max_score
            result = await self.evaluate_answer(
                EvaluateAnswerRequest(
                    exam_session_id=request.exam_session_id,
                    course_name=request.course_name,
                    documents=request.documents,
                    question_answer=pair,
                    max_score=max_score,
                )
            )
            total_score += result.score
            question_scores.append(
                QuestionScoreDetail(
                    question_id=pair.question_id,
                    question_text=pair.question_text,
                    student_answer=pair.student_answer,
                    score=result.score,
                    max_score=result.max_score,
                    feedback=result.feedback,
                    strengths=result.strengths,
                    weaknesses=result.weaknesses,
                    missing_content=result.missing_content,
                    sentence_feedback=result.sentence_feedback,
                    criterion_analyses=result.criterion_analyses,
                    improvements=result.improvements,
                )
            )
            parts.append(
                SubmissionPart(
                    feedback=result.feedback,
                    learning_outcome=(pair.learning_outcome or "").strip(),
                    topic=(pair.topic or "").strip(),
                    criterion_analyses=result.criterion_analyses,
                )
            )

        total_score = round(total_score, 4)
        max_total = round(max_total, 4)
        percentage = round(total_score / max_total * 100, 2) if max_total > 0 else 0.0
        overall_feedback, study_suggestions = build_submission_feedback(parts)
        return EvaluateSessionResponse(
            success=True,
            exam_session_id=request.exam_session_id,
            total_score=total_score,
            max_total_score=max_total,
            percentage=percentage,
            overall_feedback=overall_feedback,
            question_scores=question_scores,
            study_suggestions=study_suggestions,
            status="PendingReview",
            provider=self.provider,
            model=self.model,
            schema_version=SCHEMA_VERSION,
        )
