"""Combine numeric scoring with rubric-grounded feedback."""

from app.core.ai_client import BaseAIClient
from app.models.request import EvaluateAnswerRequest, EvaluateSessionRequest
from app.models.response import EvaluateAnswerResponse, EvaluateSessionResponse, QuestionScoreDetail
from app.services.answer_evidence import SubmissionPart, build_submission_feedback
from app.services.feedback_analysis_service import FeedbackAnalysisService
from app.services.scoring_service import ScoringService


class EvaluationService:
    """Score an answer, then attach transcript evidence when a rubric is supplied."""

    def __init__(self, ai_client: BaseAIClient | None = None) -> None:
        self.scoring = ScoringService(ai_client)
        self.analysis = FeedbackAnalysisService(ai_client)

    async def evaluate_answer(self, request: EvaluateAnswerRequest) -> EvaluateAnswerResponse:
        scored = await self.scoring.evaluate_answer(request)
        if not request.question_answer.rubric:
            return scored

        analysis = await self.analysis.analyze(request)
        return scored.model_copy(update={
            "feedback": analysis.feedback,
            "strengths": list(analysis.strengths),
            "weaknesses": list(analysis.weaknesses),
            "missing_content": list(analysis.missing_content),
            "sentence_feedback": analysis.sentence_feedback,
            "criterion_analyses": analysis.criterion_analyses,
            "improvements": list(analysis.improvements),
        })

    async def evaluate_session(self, request: EvaluateSessionRequest) -> EvaluateSessionResponse:
        if not any(pair.rubric for pair in request.question_answers):
            return await self.scoring.evaluate_session(request)

        question_scores: list[QuestionScoreDetail] = []
        parts: list[SubmissionPart] = []
        total_score = 0.0
        max_total = len(request.question_answers) * request.max_score_per_question
        for pair in request.question_answers:
            result = await self.evaluate_answer(
                EvaluateAnswerRequest(
                    exam_session_id=request.exam_session_id,
                    course_name=request.course_name,
                    documents=request.documents,
                    question_answer=pair,
                    max_score=request.max_score_per_question,
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

        percentage = (total_score / max_total * 100) if max_total > 0 else 0.0
        overall_feedback, study_suggestions = build_submission_feedback(parts)
        return EvaluateSessionResponse(
            success=True,
            exam_session_id=request.exam_session_id,
            total_score=round(total_score, 2),
            max_total_score=round(max_total, 2),
            percentage=round(percentage, 2),
            overall_feedback=overall_feedback,
            question_scores=question_scores,
            study_suggestions=study_suggestions,
            status="PendingReview",
        )
