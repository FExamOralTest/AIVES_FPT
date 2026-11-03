"""
AIVES AI Service - Evaluation Edge Cases Tests
Test các edge cases của luồng chấm theo rubric: timeout, retry, response hỏng,
điểm vượt trần criterion, thiếu rubric và rubric không khớp điểm câu.
"""

import json
from unittest.mock import AsyncMock

import pytest

from app.exceptions.ai_exceptions import AITimeoutError, MaxRetriesExceededError
from app.models.request import (
    AnswerRubricCriterion,
    EvaluateAnswerRequest,
    QuestionAnswerPair,
)
from app.services.evaluation_service import EvaluationService


def _request(
    course_content,
    learning_outcomes,
    question_answer,
    max_score=10.0,
) -> EvaluateAnswerRequest:
    return EvaluateAnswerRequest(
        exam_session_id="session-001",
        course_name="Software Engineering",
        documents=[course_content, learning_outcomes],
        question_answer=question_answer,
        max_score=max_score,
    )


def _single_criterion_pair(transcript: str = "Agile uses short iterations.") -> QuestionAnswerPair:
    return QuestionAnswerPair(
        question_id="q-edge",
        question_text="Explain Agile.",
        student_answer=transcript,
        sample_answer="Agile uses short iterations.",
        learning_outcome="Explain Agile",
        topic="Agile",
        max_score=3.0,
        rubric=[
            AnswerRubricCriterion(
                criterion_id="c1", description="Explain short iterations", max_score=3.0
            )
        ],
    )


def _analysis(awarded: float) -> str:
    return json.dumps({
        "sentence_feedback": [
            {"sentence": "Agile uses short iterations.", "judgment": "correct", "comment": "ok"}
        ],
        "criterion_analyses": [
            {
                "criterion_id": "c1",
                "judgment": "met",
                "awarded_score": awarded,
                "explanation": "covered",
                "errors": [],
                "omissions": [],
                "evidence_quotes": ["Agile uses short iterations"],
            }
        ],
        "strengths": ["short iterations"],
        "weaknesses": [],
        "missing_content": [],
        "improvements": [],
        "feedback": "The answer explains short iterations.",
    })


class TestEvaluationEdgeCases:
    """Edge cases trong evaluation service (rubric-driven)."""

    @pytest.mark.asyncio
    async def test_evaluate_with_good_answer(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good,
        fake_analysis_client,
    ):
        service = EvaluationService(ai_client=fake_analysis_client)
        result = await service.evaluate_answer(
            _request(mock_course_content, mock_learning_outcomes, mock_question_answer_good)
        )

        assert result.success is True
        assert result.question_id == mock_question_answer_good.question_id
        assert result.max_score == 10.0
        assert result.score == pytest.approx(
            sum(item.awarded_score for item in result.criterion_analyses)
        )
        assert result.score > 0

    @pytest.mark.asyncio
    async def test_evaluate_with_poor_answer(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_poor,
        fake_analysis_client,
    ):
        service = EvaluationService(ai_client=fake_analysis_client)
        result = await service.evaluate_answer(
            _request(mock_course_content, mock_learning_outcomes, mock_question_answer_poor)
        )

        assert result.success is True
        assert 0 <= result.score <= result.max_score
        assert result.weaknesses or result.missing_content

    @pytest.mark.asyncio
    async def test_evaluate_malformed_json_response(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good,
    ):
        client = AsyncMock()
        client.complete_with_retry.return_value = "{ invalid json"

        service = EvaluationService(ai_client=client)
        with pytest.raises(ValueError) as exc_info:
            await service.evaluate_answer(
                _request(mock_course_content, mock_learning_outcomes, mock_question_answer_good)
            )

        assert "invalid" in str(exc_info.value).lower()

    @pytest.mark.asyncio
    async def test_evaluate_missing_required_fields(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good,
    ):
        client = AsyncMock()
        client.complete_with_retry.return_value = json.dumps({"criterion_analyses": []})

        service = EvaluationService(ai_client=client)
        with pytest.raises(ValueError):
            await service.evaluate_answer(
                _request(mock_course_content, mock_learning_outcomes, mock_question_answer_good)
            )

    @pytest.mark.asyncio
    async def test_evaluate_awarded_score_exceeds_criterion_max(
        self, mock_course_content, mock_learning_outcomes
    ):
        client = AsyncMock()
        client.complete_with_retry.return_value = _analysis(awarded=99.0)

        service = EvaluationService(ai_client=client)
        with pytest.raises(ValueError):
            await service.evaluate_answer(
                _request(
                    mock_course_content,
                    mock_learning_outcomes,
                    _single_criterion_pair(),
                    max_score=3.0,
                )
            )

    @pytest.mark.asyncio
    async def test_evaluate_without_rubric_is_rejected(
        self, mock_course_content, mock_learning_outcomes
    ):
        service = EvaluationService(ai_client=AsyncMock())
        pair = _single_criterion_pair()
        pair.rubric = []

        with pytest.raises(ValueError) as exc_info:
            await service.evaluate_answer(
                _request(mock_course_content, mock_learning_outcomes, pair)
            )

        assert "rubric is required" in str(exc_info.value).lower()

    @pytest.mark.asyncio
    async def test_rubric_max_score_must_match_question_max(
        self, mock_course_content, mock_learning_outcomes
    ):
        client = AsyncMock()
        client.complete_with_retry.return_value = _analysis(awarded=3.0)
        service = EvaluationService(ai_client=client)

        with pytest.raises(ValueError) as exc_info:
            await service.evaluate_answer(
                _request(
                    mock_course_content,
                    mock_learning_outcomes,
                    _single_criterion_pair(),
                    max_score=10.0,
                )
            )

        assert "rubric max" in str(exc_info.value).lower()

    @pytest.mark.asyncio
    async def test_evaluate_timeout(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good,
    ):
        client = AsyncMock()
        client.complete_with_retry.side_effect = AITimeoutError(
            "Request timed out after 30 seconds", timeout_seconds=30.0
        )

        service = EvaluationService(ai_client=client)
        with pytest.raises(ValueError) as exc_info:
            await service.evaluate_answer(
                _request(mock_course_content, mock_learning_outcomes, mock_question_answer_good)
            )

        message = str(exc_info.value).lower()
        assert "timeout" in message or "timed out" in message

    @pytest.mark.asyncio
    async def test_evaluate_max_retries_exceeded(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good,
    ):
        client = AsyncMock()
        client.complete_with_retry.side_effect = MaxRetriesExceededError(
            "Max retries (3) exceeded", max_retries=3
        )

        service = EvaluationService(ai_client=client)
        with pytest.raises(ValueError) as exc_info:
            await service.evaluate_answer(
                _request(mock_course_content, mock_learning_outcomes, mock_question_answer_good)
            )

        message = str(exc_info.value).lower()
        assert "retries" in message or "retry" in message


class TestEvaluationResponseMetadata:
    """Response phải mang provider/model/schema_version cho audit."""

    @pytest.mark.asyncio
    async def test_response_carries_audit_metadata(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good,
        fake_analysis_client,
    ):
        service = EvaluationService(ai_client=fake_analysis_client)
        result = await service.evaluate_answer(
            _request(mock_course_content, mock_learning_outcomes, mock_question_answer_good)
        )

        assert result.provider
        assert result.model
        assert result.schema_version == "2.0"
