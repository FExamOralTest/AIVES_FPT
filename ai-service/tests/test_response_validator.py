"""
AIVES AI Service - Response Validator Tests
Test ResponseValidator với các trường hợp sanitize, parse, validate.
"""

import json
import pytest

from app.services.response_validator import ResponseValidator, get_validator
from app.models.contracts import AIEvaluationContract, QuestionGenerationContract, FollowUpQuestionContract
from app.exceptions.ai_exceptions import AIParseError, AIContractViolationError


class TestSanitizeMarkdown:
    """Test sanitize_markdown function."""
    
    def test_sanitize_plain_json(self):
        """Test sanitizing plain JSON (no markdown)."""
        validator = get_validator()
        raw = '{"key": "value"}'
        result = validator.sanitize_markdown(raw)
        assert result == '{"key": "value"}'
    
    def test_sanitize_json_with_fence(self):
        """Test sanitizing JSON wrapped in ```json fence."""
        validator = get_validator()
        raw = '```json\n{"key": "value"}\n```'
        result = validator.sanitize_markdown(raw)
        assert result == '{"key": "value"}'
    
    def test_sanitize_json_with_uppercase_fence(self):
        """Test sanitizing JSON with uppercase fence."""
        validator = get_validator()
        raw = '```JSON\n{"key": "value"}\n```'
        result = validator.sanitize_markdown(raw)
        assert result == '{"key": "value"}'
    
    def test_sanitize_json_with_generic_fence(self):
        """Test sanitizing JSON with generic ``` fence."""
        validator = get_validator()
        raw = '```\n{"key": "value"}\n```'
        result = validator.sanitize_markdown(raw)
        assert result == '{"key": "value"}'
    
    def test_sanitize_non_string_input(self):
        """Test that non-string input raises error."""
        validator = get_validator()
        with pytest.raises(AIParseError):
            validator.sanitize_markdown({"key": "value"})


class TestParseJSON:
    """Test parse_json function."""
    
    def test_parse_valid_json(self):
        """Test parsing valid JSON."""
        validator = get_validator()
        json_str = '{"key": "value", "number": 42}'
        result = validator.parse_json(json_str)
        assert result == {"key": "value", "number": 42}
    
    def test_parse_malformed_json(self):
        """Test parsing malformed JSON raises error."""
        validator = get_validator()
        malformed = '{"key": "value", "number": }'  # Missing value
        with pytest.raises(AIParseError) as exc_info:
            validator.parse_json(malformed)
        assert "parse" in str(exc_info.value).lower()
    
    def test_parse_json_array_raises_error(self):
        """Test that JSON array raises error (expecting object)."""
        validator = get_validator()
        json_str = '[{"key": "value"}]'
        with pytest.raises(AIParseError) as exc_info:
            validator.parse_json(json_str)
        assert "object" in str(exc_info.value).lower()
    
    def test_parse_json_with_unicode(self):
        """Test parsing JSON with Vietnamese characters."""
        validator = get_validator()
        json_str = '{"message": "Câu trả lời tốt"}'
        result = validator.parse_json(json_str)
        assert result["message"] == "Câu trả lời tốt"


class TestValidateContract:
    """Test validate_contract function."""
    
    def test_validate_valid_contract(self):
        """Test validating valid contract."""
        validator = get_validator()
        data = {
            "transcript": "Test answer",
            "AiScore": 85.0,
            "confidence": 0.9,
            "feedback": {
                "summary": "Good",
                "strengths": [],
                "improvements": [],
                "actionableTips": []
            },
            "rubricResults": [
                {
                    "criterionId": "c1",
                    "criterionName": "Test",
                    "score": 25.0,
                    "maxScore": 30.0,
                    "evidence": "test evidence",
                    "comment": "test comment"
                }
            ]
        }
        contract = validator.validate_contract(data, AIEvaluationContract)
        assert contract.ai_score == 85.0
        assert contract.confidence == 0.9
    
    def test_validate_missing_required_field(self):
        """Test that missing required field raises error."""
        validator = get_validator()
        data = {
            "transcript": "Test",
            "AiScore": 85.0,
            # Missing confidence
            "feedback": {"summary": "Good", "strengths": [], "improvements": [], "actionableTips": []},
            "rubricResults": [
                {"criterionId": "c1", "criterionName": "t", "score": 10.0,
                 "maxScore": 10.0, "evidence": "e", "comment": "c"}
            ]
        }
        with pytest.raises(AIContractViolationError) as exc_info:
            validator.validate_contract(data, AIEvaluationContract)
        # Check that violations list contains the missing field
        assert exc_info.value.violations is not None
        assert any("confidence" in v.lower() for v in exc_info.value.violations)
    
    def test_validate_invalid_score_range(self):
        """Test that score outside valid range raises error."""
        validator = get_validator()
        data = {
            "transcript": "Test",
            "AiScore": 150.0,  # > 100
            "confidence": 0.9,
            "feedback": {"summary": "Good", "strengths": [], "improvements": [], "actionableTips": []},
            "rubricResults": [
                {"criterionId": "c1", "criterionName": "t", "score": 10.0,
                 "maxScore": 10.0, "evidence": "e", "comment": "c"}
            ]
        }
        with pytest.raises(AIContractViolationError):
            validator.validate_contract(data, AIEvaluationContract)


class TestValidateEvaluationResponse:
    """Test validate_evaluation_response end-to-end."""
    
    def test_validate_evaluation_success(self, mock_ai_response_valid):
        """Test validating valid evaluation response."""
        validator = get_validator()
        contract = validator.validate_evaluation_response(mock_ai_response_valid)
        assert contract.ai_score == 85.0
        assert contract.confidence == 0.92
        assert len(contract.rubric_results) == 3
    
    def test_validate_evaluation_with_markdown_fence(self, mock_ai_response_with_markdown_fence):
        """Test validating response with markdown fence."""
        validator = get_validator()
        contract = validator.validate_evaluation_response(mock_ai_response_with_markdown_fence)
        assert contract.ai_score == 80.0
        assert contract.confidence == 0.88
    
    def test_validate_evaluation_malformed_json(self, mock_ai_response_malformed):
        """Test that malformed JSON raises error."""
        validator = get_validator()
        with pytest.raises(AIParseError):
            validator.validate_evaluation_response(mock_ai_response_malformed)
    
    def test_validate_evaluation_missing_fields(self, mock_ai_response_missing_fields):
        """Test that missing fields raise error."""
        validator = get_validator()
        with pytest.raises(AIContractViolationError) as exc_info:
            validator.validate_evaluation_response(mock_ai_response_missing_fields)
        # Check violations list contains missing fields
        assert exc_info.value.violations is not None
        violations_str = " ".join(exc_info.value.violations).lower()
        assert "confidence" in violations_str or "rubricresults" in violations_str
    
    def test_validate_evaluation_invalid_score(self, mock_ai_response_invalid_score):
        """Test that invalid score (>100) raises error."""
        validator = get_validator()
        with pytest.raises(AIContractViolationError):
            validator.validate_evaluation_response(mock_ai_response_invalid_score)
    
    def test_validate_evaluation_score_exceeds_max(self, mock_ai_response_score_exceeds_max):
        """Test that rubric score > maxScore raises error."""
        validator = get_validator()
        with pytest.raises(AIContractViolationError):
            validator.validate_evaluation_response(mock_ai_response_score_exceeds_max)


class TestValidateQuestionResponse:
    """Test validate_question_generation_response."""
    
    def test_validate_question_success(self):
        """Test validating valid question generation response."""
        validator = get_validator()
        raw = json.dumps({
            "questionText": "Explain Agile methodology",
            "topic": "Agile",
            "learningOutcome": "Understand Agile principles",
            "difficulty": 2,
            "expectedKeywords": ["sprint", "scrum"],
            "sampleAnswer": "Agile is an iterative approach...",
            "rubric": [
                {
                    "criterionId": "c1",
                    "criterionName": "Understanding",
                    "score": 0,
                    "maxScore": 30.0,
                    "evidence": "N/A",
                    "comment": "Template rubric criterion"
                }
            ]
        })
        contract = validator.validate_question_generation_response(raw)
        assert contract.question_text == "Explain Agile methodology"
        assert contract.difficulty == 2


class TestValidateFollowUpResponse:
    """Test validate_followup_response."""
    
    def test_validate_followup_needed(self):
        """Test validating follow-up response when follow-up is needed."""
        validator = get_validator()
        raw = json.dumps({
            "shouldFollowUp": True,
            "followUpQuestion": "Can you provide an example?",
            "reason": "Answer lacks examples",
            "missingPoints": ["examples", "depth"]
        })
        contract = validator.validate_followup_response(raw)
        assert contract.should_follow_up is True
        assert "example" in contract.follow_up_question.lower()
    
    def test_validate_followup_not_needed(self):
        """Test validating follow-up response when no follow-up needed."""
        validator = get_validator()
        raw = json.dumps({
            "shouldFollowUp": False,
            "followUpQuestion": "",
            "reason": "Answer is complete",
            "missingPoints": []
        })
        contract = validator.validate_followup_response(raw)
        assert contract.should_follow_up is False
        assert contract.follow_up_question == ""


class TestCheckScoreRange:
    """Test check_score_range helper."""
    
    def test_score_in_range(self):
        """Test that score in range passes."""
        validator = get_validator()
        # Should not raise
        validator.check_score_range(50.0, 0.0, 100.0, "test_score")
    
    def test_score_below_min(self):
        """Test that score below min raises error."""
        validator = get_validator()
        from app.exceptions.ai_exceptions import AIValidationError
        with pytest.raises(AIValidationError):
            validator.check_score_range(-10.0, 0.0, 100.0, "test_score")
    
    def test_score_above_max(self):
        """Test that score above max raises error."""
        validator = get_validator()
        from app.exceptions.ai_exceptions import AIValidationError
        with pytest.raises(AIValidationError):
            validator.check_score_range(150.0, 0.0, 100.0, "test_score")
    
    def test_score_at_boundaries(self):
        """Test that score at exact boundaries passes."""
        validator = get_validator()
        # Should not raise
        validator.check_score_range(0.0, 0.0, 100.0, "min_score")
        validator.check_score_range(100.0, 0.0, 100.0, "max_score")
