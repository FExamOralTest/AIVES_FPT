"""
AIVES AI Service - Contract Tests
Test các contract models và validation logic.
"""

import pytest
from pydantic import ValidationError

from app.models.contracts import (
    AIEvaluationContract,
    FeedbackContract,
    RubricResultContract,
    QuestionGenerationContract,
    FollowUpQuestionContract,
)


class TestRubricResultContract:
    """Test RubricResultContract validation."""
    
    def test_valid_rubric_result(self):
        """Test valid rubric result contract."""
        result = RubricResultContract(
            criterionId="crit-001",
            criterionName="Test Criterion",
            score=25.0,
            maxScore=30.0,
            evidence="Student mentioned key concept",
            comment="Good understanding"
        )
        assert result.criterion_id == "crit-001"
        assert result.score == 25.0
        assert result.max_score == 30.0
    
    def test_score_exceeds_max_score(self):
        """Test that score > maxScore raises error."""
        with pytest.raises(ValidationError) as exc_info:
            RubricResultContract(
                criterionId="crit-001",
                criterionName="Test",
                score=50.0,  # > maxScore
                maxScore=30.0,
                evidence="test",
                comment="test"
            )
        assert "Score" in str(exc_info.value) or "max_score" in str(exc_info.value)
    
    def test_negative_score(self):
        """Test that negative score raises error."""
        with pytest.raises(ValidationError):
            RubricResultContract(
                criterionId="crit-001",
                criterionName="Test",
                score=-10.0,
                maxScore=30.0,
                evidence="test",
                comment="test"
            )
    
    def test_empty_criterion_id(self):
        """Test that empty criterion_id raises error."""
        with pytest.raises(ValidationError):
            RubricResultContract(
                criterionId="",
                criterionName="Test",
                score=25.0,
                maxScore=30.0,
                evidence="test",
                comment="test"
            )
    
    def test_empty_evidence(self):
        """Test that empty evidence raises error."""
        with pytest.raises(ValidationError):
            RubricResultContract(
                criterionId="crit-001",
                criterionName="Test",
                score=25.0,
                maxScore=30.0,
                evidence="",
                comment="test"
            )


class TestFeedbackContract:
    """Test FeedbackContract validation."""
    
    def test_valid_feedback(self):
        """Test valid feedback contract."""
        feedback = FeedbackContract(
            summary="Good answer overall",
            strengths=["Clear explanation", "Good examples"],
            improvements=["Need more depth"],
            actionableTips=["Study chapter 5"]
        )
        assert feedback.summary == "Good answer overall"
        assert len(feedback.strengths) == 2
    
    def test_empty_summary(self):
        """Test that empty summary raises error."""
        with pytest.raises(ValidationError):
            FeedbackContract(
                summary="",
                strengths=["test"],
                improvements=[],
                actionableTips=[]
            )
    
    def test_feedback_with_empty_lists(self):
        """Test feedback with empty lists (should be valid)."""
        feedback = FeedbackContract(
            summary="Basic answer",
            strengths=[],
            improvements=[],
            actionableTips=[]
        )
        assert feedback.strengths == []
        assert feedback.improvements == []


class TestAIEvaluationContract:
    """Test AIEvaluationContract validation."""
    
    def test_valid_evaluation_contract(self):
        """Test valid AI evaluation contract."""
        contract = AIEvaluationContract(
            transcript="Student explained Agile methodology well.",
            AiScore=85.0,
            confidence=0.92,
            feedback=FeedbackContract(
                summary="Good answer",
                strengths=["Clear"],
                improvements=[],
                actionableTips=[]
            ),
            rubricResults=[
                RubricResultContract(
                    criterionId="crit-001",
                    criterionName="Understanding",
                    score=28.0,
                    maxScore=30.0,
                    evidence="explained well",
                    comment="Good"
                )
            ]
        )
        assert contract.ai_score == 85.0
        assert contract.confidence == 0.92
        assert len(contract.rubric_results) == 1
    
    def test_empty_transcript(self):
        """Test that empty transcript raises error."""
        with pytest.raises(ValidationError):
            AIEvaluationContract(
                transcript="",
                AiScore=85.0,
                confidence=0.92,
                feedback=FeedbackContract(summary="test", strengths=[], improvements=[], actionableTips=[]),
                rubricResults=[
                    RubricResultContract(
                        criterionId="c1", criterionName="t", score=10.0,
                        maxScore=10.0, evidence="e", comment="c"
                    )
                ]
            )
    
    def test_ai_score_above_100(self):
        """Test that AiScore > 100 raises error."""
        with pytest.raises(ValidationError):
            AIEvaluationContract(
                transcript="test",
                AiScore=150.0,
                confidence=0.92,
                feedback=FeedbackContract(summary="test", strengths=[], improvements=[], actionableTips=[]),
                rubricResults=[
                    RubricResultContract(
                        criterionId="c1", criterionName="t", score=10.0,
                        maxScore=10.0, evidence="e", comment="c"
                    )
                ]
            )
    
    def test_ai_score_negative(self):
        """Test that negative AiScore raises error."""
        with pytest.raises(ValidationError):
            AIEvaluationContract(
                transcript="test",
                AiScore=-10.0,
                confidence=0.92,
                feedback=FeedbackContract(summary="test", strengths=[], improvements=[], actionableTips=[]),
                rubricResults=[
                    RubricResultContract(
                        criterionId="c1", criterionName="t", score=10.0,
                        maxScore=10.0, evidence="e", comment="c"
                    )
                ]
            )
    
    def test_confidence_above_1(self):
        """Test that confidence > 1.0 raises error."""
        with pytest.raises(ValidationError):
            AIEvaluationContract(
                transcript="test",
                AiScore=85.0,
                confidence=1.5,
                feedback=FeedbackContract(summary="test", strengths=[], improvements=[], actionableTips=[]),
                rubricResults=[
                    RubricResultContract(
                        criterionId="c1", criterionName="t", score=10.0,
                        maxScore=10.0, evidence="e", comment="c"
                    )
                ]
            )
    
    def test_confidence_negative(self):
        """Test that negative confidence raises error."""
        with pytest.raises(ValidationError):
            AIEvaluationContract(
                transcript="test",
                AiScore=85.0,
                confidence=-0.1,
                feedback=FeedbackContract(summary="test", strengths=[], improvements=[], actionableTips=[]),
                rubricResults=[
                    RubricResultContract(
                        criterionId="c1", criterionName="t", score=10.0,
                        maxScore=10.0, evidence="e", comment="c"
                    )
                ]
            )
    
    def test_empty_rubric_results(self):
        """Test that empty rubricResults raises error."""
        with pytest.raises(ValidationError):
            AIEvaluationContract(
                transcript="test",
                AiScore=85.0,
                confidence=0.92,
                feedback=FeedbackContract(summary="test", strengths=[], improvements=[], actionableTips=[]),
                rubricResults=[]
            )
    
    def test_duplicate_criterion_ids(self):
        """Test that duplicate criterion IDs raise error."""
        with pytest.raises(ValidationError) as exc_info:
            AIEvaluationContract(
                transcript="test",
                AiScore=85.0,
                confidence=0.92,
                feedback=FeedbackContract(summary="test", strengths=[], improvements=[], actionableTips=[]),
                rubricResults=[
                    RubricResultContract(
                        criterionId="crit-001", criterionName="t1", score=10.0,
                        maxScore=10.0, evidence="e", comment="c"
                    ),
                    RubricResultContract(
                        criterionId="crit-001", criterionName="t2", score=10.0,
                        maxScore=10.0, evidence="e", comment="c"
                    )
                ]
            )
        assert "trùng lặp" in str(exc_info.value).lower() or "duplicate" in str(exc_info.value).lower()


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
                RubricResultContract(
                    criterionId="c1", criterionName="Understanding",
                    score=0, maxScore=30.0, evidence="N/A", comment="Template rubric"
                )
            ]
        )
        assert contract.question_text == "Explain Agile methodology"
        assert contract.difficulty == 2
    
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
                    RubricResultContract(
                        criterionId="c1", criterionName="t",
                        score=0, maxScore=10.0, evidence="N/A", comment="Template"
                    )
                ]
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
