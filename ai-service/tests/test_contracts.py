"""
AIVES AI Service - Contract Tests
Test các contract models và validation logic.
"""

import pytest
from pydantic import ValidationError

from app.models.contracts import (
    QuestionGenerationContract,
    FollowUpQuestionContract,
)


class TestQuestionGenerationContract:
    """Test QuestionGenerationContract validation."""

    def test_valid_question_generation(self):
        """Test valid question generation contract."""
        contract = QuestionGenerationContract(
            questionText="Explain Agile methodology",
            topic="Agile",
            learningOutcome="Understand Agile principles",
            difficulty=2,
            expectedKeywords=["sprint", "scrum"],
            sampleAnswer="Agile is iterative...",
            rubric=[
                {
                    "criterion_id": "c1",
                    "description": "Understanding of Agile principles",
                    "max_score": 30.0,
                }
            ],
        )
        assert contract.question_text == "Explain Agile methodology"
        assert contract.difficulty == 2
        assert contract.rubric[0].criterion_id == "c1"

    def test_invalid_difficulty(self):
        """Test that difficulty outside 1-3 raises error."""
        with pytest.raises(ValidationError):
            QuestionGenerationContract(
                questionText="Test",
                topic="Test",
                learningOutcome="Test",
                difficulty=5,  # Invalid
                sampleAnswer="Test",
                rubric=[
                    {
                        "criterion_id": "c1",
                        "description": "Test criterion",
                        "max_score": 10.0,
                    }
                ],
            )

    def test_rubric_requires_at_least_one_criterion(self):
        """Test that an empty rubric raises error."""
        with pytest.raises(ValidationError):
            QuestionGenerationContract(
                questionText="Test",
                topic="Test",
                learningOutcome="Test",
                difficulty=2,
                sampleAnswer="Test",
                rubric=[],
            )


class TestFollowUpQuestionContract:
    """Test FollowUpQuestionContract validation."""

    def test_should_follow_up_true_with_question(self):
        """Test follow-up contract when followup is needed."""
        contract = FollowUpQuestionContract(
            shouldFollowUp=True,
            followUpQuestion="Can you elaborate on that?",
            reason="Answer lacks detail",
            missingPoints=["examples", "depth"]
        )
        assert contract.should_follow_up is True
        assert contract.follow_up_question != ""

    def test_should_follow_up_false_empty_question(self):
        """Test follow-up contract when no followup needed."""
        contract = FollowUpQuestionContract(
            shouldFollowUp=False,
            followUpQuestion="",
            reason="Answer is complete",
            missingPoints=[]
        )
        assert contract.should_follow_up is False
        assert contract.follow_up_question == ""

    def test_should_follow_up_true_but_empty_question(self):
        """Test that shouldFollowUp=True with empty question raises error."""
        with pytest.raises(ValidationError) as exc_info:
            FollowUpQuestionContract(
                shouldFollowUp=True,
                followUpQuestion="",  # Empty but shouldFollowUp=True
                reason="Missing details",
                missingPoints=[]
            )
        assert "rỗng" in str(exc_info.value).lower() or "empty" in str(exc_info.value).lower()

    def test_should_follow_up_false_but_has_question(self):
        """Test that shouldFollowUp=False with non-empty question raises error."""
        with pytest.raises(ValidationError) as exc_info:
            FollowUpQuestionContract(
                shouldFollowUp=False,
                followUpQuestion="Why is that?",  # Has question but shouldFollowUp=False
                reason="Answer complete",
                missingPoints=[]
            )
        assert "rỗng" in str(exc_info.value).lower() or "empty" in str(exc_info.value).lower()
