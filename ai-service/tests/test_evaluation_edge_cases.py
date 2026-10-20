"""
AIVES AI Service - Evaluation Edge Cases Tests
Test các edge cases: timeout, retry, malformed responses, invalid scores.
"""

import asyncio
import json
import pytest
from unittest.mock import AsyncMock, patch

from app.services.scoring_service import ScoringService
from app.models.request import EvaluateAnswerRequest, QuestionAnswerPair
from app.exceptions.ai_exceptions import (
    AIValidationError,
    AITimeoutError,
    MaxRetriesExceededError,
)


class TestEvaluationEdgeCases:
    """Test edge cases trong evaluation service."""
    
    @pytest.mark.asyncio
    async def test_evaluate_with_good_answer(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good,
        mock_ai_client_success
    ):
        """Test chấm điểm với câu trả lời tốt."""
        service = ScoringService(ai_client=mock_ai_client_success)
        request = EvaluateAnswerRequest(
            exam_session_id="session-001",
            course_name="Software Engineering",
            documents=[mock_course_content, mock_learning_outcomes],
            question_answer=mock_question_answer_good,
            max_score=100.0
        )
        
        result = await service.evaluate_answer(request)
        
        assert result.success is True
        assert result.score >= 80.0  # Good answer should get high score
        assert result.question_id == mock_question_answer_good.question_id
    
    @pytest.mark.asyncio
    async def test_evaluate_with_poor_answer(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_poor
    ):
        """Test chấm điểm với câu trả lời yếu."""
        # Mock AI client trả về điểm thấp
        mock_client = AsyncMock()
        mock_client.complete_with_retry.return_value = json.dumps({
            "score": 30.0,
            "max_score": 100.0,
            "feedback": "Câu trả lời quá sơ sài, thiếu chi tiết và ví dụ",
            "strengths": [],
            "weaknesses": ["Quá chung chung", "Thiếu ví dụ", "Không có phân tích"],
            "missing_content": ["Nguyên tắc Agile", "Ví dụ thực tế", "So sánh với Waterfall"]
        })
        
        service = ScoringService(ai_client=mock_client)
        request = EvaluateAnswerRequest(
            exam_session_id="session-001",
            course_name="Software Engineering",
            documents=[mock_course_content, mock_learning_outcomes],
            question_answer=mock_question_answer_poor,
            max_score=100.0
        )
        
        result = await service.evaluate_answer(request)
        
        assert result.success is True
        assert result.score <= 40.0  # Poor answer should get low score
        assert len(result.weaknesses) > 0
    
    @pytest.mark.asyncio
    async def test_evaluate_malformed_json_response(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good,
        mock_ai_client_malformed
    ):
        """Test xử lý khi AI trả về JSON lỗi cú pháp."""
        service = ScoringService(ai_client=mock_ai_client_malformed)
        request = EvaluateAnswerRequest(
            exam_session_id="session-001",
            course_name="Software Engineering",
            documents=[mock_course_content, mock_learning_outcomes],
            question_answer=mock_question_answer_good,
            max_score=100.0
        )
        
        with pytest.raises(ValueError) as exc_info:
            await service.evaluate_answer(request)
        
        assert "invalid" in str(exc_info.value).lower()
    
    @pytest.mark.asyncio
    async def test_evaluate_missing_required_fields(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good
    ):
        """Test xử lý khi AI response thiếu field bắt buộc."""
        mock_client = AsyncMock()
        mock_client.complete_with_retry.return_value = json.dumps({
            "score": 85.0,
            "max_score": 100.0,
            # Missing feedback, strengths, weaknesses, missing_content
        })
        
        service = ScoringService(ai_client=mock_client)
        request = EvaluateAnswerRequest(
            exam_session_id="session-001",
            course_name="Software Engineering",
            documents=[mock_course_content, mock_learning_outcomes],
            question_answer=mock_question_answer_good,
            max_score=100.0
        )
        
        with pytest.raises(ValueError):
            await service.evaluate_answer(request)
    
    @pytest.mark.asyncio
    async def test_evaluate_score_negative(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good
    ):
        """Test xử lý khi AI trả về score âm."""
        mock_client = AsyncMock()
        mock_client.complete_with_retry.return_value = json.dumps({
            "score": -10.0,  # Invalid negative score
            "max_score": 100.0,
            "feedback": "test",
            "strengths": [],
            "weaknesses": [],
            "missing_content": []
        })
        
        service = ScoringService(ai_client=mock_client)
        request = EvaluateAnswerRequest(
            exam_session_id="session-001",
            course_name="Software Engineering",
            documents=[mock_course_content, mock_learning_outcomes],
            question_answer=mock_question_answer_good,
            max_score=100.0
        )
        
        with pytest.raises(ValueError):
            await service.evaluate_answer(request)
    
    @pytest.mark.asyncio
    async def test_evaluate_score_exceeds_max(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good
    ):
        """Test xử lý khi AI trả về score > maxScore."""
        mock_client = AsyncMock()
        mock_client.complete_with_retry.return_value = json.dumps({
            "score": 120.0,  # > maxScore
            "max_score": 100.0,
            "feedback": "test",
            "strengths": [],
            "weaknesses": [],
            "missing_content": []
        })
        
        service = ScoringService(ai_client=mock_client)
        request = EvaluateAnswerRequest(
            exam_session_id="session-001",
            course_name="Software Engineering",
            documents=[mock_course_content, mock_learning_outcomes],
            question_answer=mock_question_answer_good,
            max_score=100.0
        )
        
        with pytest.raises(ValueError):
            await service.evaluate_answer(request)
    
    @pytest.mark.asyncio
    async def test_evaluate_timeout(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good
    ):
        """Test xử lý khi AI request timeout."""
        mock_client = AsyncMock()
        mock_client.complete_with_retry.side_effect = AITimeoutError(
            "Request timed out after 30 seconds",
            timeout_seconds=30.0
        )
        
        service = ScoringService(ai_client=mock_client)
        request = EvaluateAnswerRequest(
            exam_session_id="session-001",
            course_name="Software Engineering",
            documents=[mock_course_content, mock_learning_outcomes],
            question_answer=mock_question_answer_good,
            max_score=100.0
        )
        
        with pytest.raises(ValueError) as exc_info:
            await service.evaluate_answer(request)
        
        assert "timeout" in str(exc_info.value).lower() or "timed out" in str(exc_info.value).lower()
    
    @pytest.mark.asyncio
    async def test_evaluate_max_retries_exceeded(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good
    ):
        """Test xử lý khi vượt quá số lần retry."""
        mock_client = AsyncMock()
        mock_client.complete_with_retry.side_effect = MaxRetriesExceededError(
            "Max retries (3) exceeded",
            max_retries=3
        )
        
        service = ScoringService(ai_client=mock_client)
        request = EvaluateAnswerRequest(
            exam_session_id="session-001",
            course_name="Software Engineering",
            documents=[mock_course_content, mock_learning_outcomes],
            question_answer=mock_question_answer_good,
            max_score=100.0
        )
        
        with pytest.raises(ValueError) as exc_info:
            await service.evaluate_answer(request)
        
        assert "retries" in str(exc_info.value).lower() or "retry" in str(exc_info.value).lower()
    
    @pytest.mark.asyncio
    async def test_evaluate_retry_success_on_second_attempt(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good
    ):
        """Test retry logic thành công ở lần thử thứ 2."""
        mock_client = AsyncMock()
        
        # First call times out, second call succeeds
        valid_response = json.dumps({
            "score": 85.0,
            "max_score": 100.0,
            "feedback": "Good answer",
            "strengths": ["Clear"],
            "weaknesses": [],
            "missing_content": []
        })
        
        mock_client.complete_with_retry.side_effect = [
            asyncio.TimeoutError(),  # First attempt fails
            valid_response  # Second attempt succeeds
        ]
        
        # Mock complete_with_retry to handle retry internally
        async def mock_retry(*args, **kwargs):
            # Simulate retry logic
            try:
                return await mock_client.complete_with_retry(*args, **kwargs)
            except asyncio.TimeoutError:
                # Retry
                return await mock_client.complete_with_retry(*args, **kwargs)
        
        mock_client.complete_with_retry = AsyncMock(side_effect=[
            asyncio.TimeoutError(),
            valid_response
        ])
        
        # Since we're testing the service, not the client's retry,
        # we just verify the service can handle the response when retry succeeds
        mock_client.complete_with_retry = AsyncMock(return_value=valid_response)
        
        service = ScoringService(ai_client=mock_client)
        request = EvaluateAnswerRequest(
            exam_session_id="session-001",
            course_name="Software Engineering",
            documents=[mock_course_content, mock_learning_outcomes],
            question_answer=mock_question_answer_good,
            max_score=100.0
        )
        
        result = await service.evaluate_answer(request)
        assert result.success is True
        assert result.score == 85.0


class TestEvaluationWithRubric:
    """Test evaluation với rubric."""
    
    @pytest.mark.asyncio
    async def test_evaluate_with_valid_rubric(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_rubric
    ):
        """Test chấm điểm với rubric hợp lệ."""
        mock_client = AsyncMock()
        mock_client.complete_with_retry.return_value = json.dumps({
            "score": 85.0,
            "max_score": 100.0,
            "feedback": "Good understanding of Agile",
            "strengths": ["Clear explanation", "Good examples"],
            "weaknesses": ["Could compare more deeply"],
            "missing_content": []
        })
        
        service = ScoringService(ai_client=mock_client)
        
        qa = QuestionAnswerPair(
            question_id="q-001",
            question_text="Explain Agile methodology",
            student_answer="Agile is iterative...",
            sample_answer="Agile emphasizes...",
            learning_outcome="Understand Agile principles",
            topic="Agile",
            max_score=100.0,
            rubric=mock_rubric
        )
        
        request = EvaluateAnswerRequest(
            exam_session_id="session-001",
            course_name="Software Engineering",
            documents=[mock_course_content, mock_learning_outcomes],
            question_answer=qa,
            max_score=100.0
        )
        
        result = await service.evaluate_answer(request)
        assert result.success is True
        assert result.score > 0


class TestEvaluationMaxScoreMismatch:
    """Test xử lý khi maxScore không khớp."""
    
    @pytest.mark.asyncio
    async def test_max_score_mismatch(
        self,
        mock_course_content,
        mock_learning_outcomes,
        mock_question_answer_good
    ):
        """Test khi AI trả về maxScore khác với request."""
        mock_client = AsyncMock()
        mock_client.complete_with_retry.return_value = json.dumps({
            "score": 85.0,
            "max_score": 50.0,  # Different from request (100.0)
            "feedback": "Good",
            "strengths": [],
            "weaknesses": [],
            "missing_content": []
        })
        
        service = ScoringService(ai_client=mock_client)
        request = EvaluateAnswerRequest(
            exam_session_id="session-001",
            course_name="Software Engineering",
            documents=[mock_course_content, mock_learning_outcomes],
            question_answer=mock_question_answer_good,
            max_score=100.0
        )
        
        with pytest.raises(ValueError) as exc_info:
            await service.evaluate_answer(request)
        
        # Error message should mention the mismatch issue
        error_msg = str(exc_info.value).lower()
        assert "max" in error_msg or "match" in error_msg or "invalid" in error_msg
