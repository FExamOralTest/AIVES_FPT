"""
AIVES AI Service - Test Fixtures
Shared fixtures và mocks cho toàn bộ test suite.
"""

import json
import pytest
from typing import Dict, Any
from unittest.mock import AsyncMock, Mock

from app.models.request import DocumentContext, QuestionAnswerPair, AnswerRubricCriterion


@pytest.fixture
def mock_course_content() -> DocumentContext:
    """Mock course content document."""
    return DocumentContext(
        document_id="doc-001",
        title="Introduction to Software Engineering",
        content="""# Software Engineering Fundamentals

## Agile Methodology
Agile is an iterative approach to software development that emphasizes flexibility and customer collaboration.

Key principles:
- Individuals and interactions over processes and tools
- Working software over comprehensive documentation
- Customer collaboration over contract negotiation
- Responding to change over following a plan

## SOLID Principles
SOLID is an acronym for five design principles:
1. Single Responsibility Principle
2. Open/Closed Principle
3. Liskov Substitution Principle
4. Interface Segregation Principle
5. Dependency Inversion Principle

## Testing Strategies
- Unit Testing: Test individual components
- Integration Testing: Test component interactions
- System Testing: Test the entire system
- Acceptance Testing: Validate against requirements
""",
        document_type="course_content"
    )


@pytest.fixture
def mock_learning_outcomes() -> DocumentContext:
    """Mock learning outcomes document."""
    return DocumentContext(
        document_id="doc-002",
        title="Learning Outcomes",
        content="""# Course Learning Outcomes

1. Explain the principles and practices of Agile software development
2. Apply SOLID principles to design maintainable object-oriented systems
3. Design and implement comprehensive testing strategies
4. Analyze software requirements and create appropriate design solutions
5. Evaluate trade-offs between different architectural patterns
""",
        document_type="learning_outcomes"
    )


@pytest.fixture
def mock_rubric() -> list[AnswerRubricCriterion]:
    """Mock rubric criteria."""
    return [
        AnswerRubricCriterion(
            criterion_id="crit-001",
            description="Giải thích đầy đủ các nguyên tắc cốt lõi của Agile",
            max_score=30.0
        ),
        AnswerRubricCriterion(
            criterion_id="crit-002",
            description="Nêu được ví dụ thực tế về áp dụng Agile",
            max_score=30.0
        ),
        AnswerRubricCriterion(
            criterion_id="crit-003",
            description="So sánh Agile với các phương pháp khác",
            max_score=40.0
        ),
    ]


@pytest.fixture
def mock_question_answer_good() -> QuestionAnswerPair:
    """Mock Q&A pair với câu trả lời tốt."""
    return QuestionAnswerPair(
        question_id="q-001",
        question_text="Giải thích phương pháp Agile và ưu điểm của nó trong phát triển phần mềm.",
        student_answer="""Agile là phương pháp phát triển phần mềm lặp đi lặp lại, tập trung vào sự linh hoạt 
và hợp tác với khách hàng. Các nguyên tắc chính bao gồm: tương tác giữa con người quan trọng hơn quy trình, 
phần mềm hoạt động quan trọng hơn tài liệu, hợp tác với khách hàng và phản ứng với thay đổi.

Ví dụ thực tế: trong dự án của công ty tôi, chúng tôi áp dụng Scrum với sprint 2 tuần, có daily standup 
và sprint review với khách hàng. Điều này giúp chúng tôi phát hiện sớm các vấn đề và điều chỉnh kịp thời.

So với Waterfall, Agile linh hoạt hơn vì cho phép thay đổi yêu cầu trong quá trình phát triển, trong khi 
Waterfall yêu cầu định nghĩa rõ ràng từ đầu.""",
        sample_answer="Agile emphasizes iterative development, customer collaboration, and flexibility.",
        learning_outcome="Explain the principles and practices of Agile software development",
        topic="Agile Methodology",
        max_score=100.0
    )


@pytest.fixture
def mock_question_answer_poor() -> QuestionAnswerPair:
    """Mock Q&A pair với câu trả lời yếu."""
    return QuestionAnswerPair(
        question_id="q-002",
        question_text="Giải thích phương pháp Agile và ưu điểm của nó trong phát triển phần mềm.",
        student_answer="Agile là một phương pháp phát triển phần mềm. Nó tốt hơn các phương pháp khác.",
        sample_answer="Agile emphasizes iterative development, customer collaboration, and flexibility.",
        learning_outcome="Explain the principles and practices of Agile software development",
        topic="Agile Methodology",
        max_score=100.0
    )


@pytest.fixture
def mock_ai_response_valid() -> str:
    """Mock valid AI response (JSON format)."""
    return json.dumps({
        "transcript": "Agile là phương pháp phát triển phần mềm lặp...",
        "AiScore": 85.0,
        "confidence": 0.92,
        "feedback": {
            "summary": "Câu trả lời thể hiện hiểu biết tốt về Agile",
            "strengths": ["Giải thích rõ các nguyên tắc", "Có ví dụ thực tế"],
            "improvements": ["Cần so sánh sâu hơn với các phương pháp khác"],
            "actionableTips": ["Bổ sung thêm ví dụ về các Agile frameworks như Kanban"]
        },
        "rubricResults": [
            {
                "criterionId": "crit-001",
                "criterionName": "Giải thích nguyên tắc Agile",
                "score": 28.0,
                "maxScore": 30.0,
                "evidence": "tương tác giữa con người quan trọng hơn quy trình",
                "comment": "Giải thích đầy đủ và chính xác"
            },
            {
                "criterionId": "crit-002",
                "criterionName": "Ví dụ thực tế",
                "score": 27.0,
                "maxScore": 30.0,
                "evidence": "trong dự án của công ty tôi, chúng tôi áp dụng Scrum",
                "comment": "Có ví dụ cụ thể và liên quan"
            },
            {
                "criterionId": "crit-003",
                "criterionName": "So sánh với phương pháp khác",
                "score": 30.0,
                "maxScore": 40.0,
                "evidence": "So với Waterfall, Agile linh hoạt hơn",
                "comment": "Có so sánh nhưng chưa đủ sâu"
            }
        ]
    }, ensure_ascii=False)


@pytest.fixture
def mock_ai_response_malformed() -> str:
    """Mock malformed JSON response."""
    return """{
        "transcript": "Test",
        "AiScore": 85.0,
        "confidence": 0.92,
        "feedback": {
            "summary": "Good answer"
        }
        "rubricResults": [  // <-- Missing comma here
    """


@pytest.fixture
def mock_ai_response_missing_fields() -> str:
    """Mock response thiếu field bắt buộc."""
    return json.dumps({
        "transcript": "Test answer",
        "AiScore": 75.0,
        # Missing confidence
        "feedback": {
            "summary": "Decent answer"
        },
        # Missing rubricResults
    })


@pytest.fixture
def mock_ai_response_invalid_score() -> str:
    """Mock response với score ngoài range."""
    return json.dumps({
        "transcript": "Test answer",
        "AiScore": 150.0,  # Invalid: > 100
        "confidence": 0.85,
        "feedback": {
            "summary": "Good answer",
            "strengths": [],
            "improvements": [],
            "actionableTips": []
        },
        "rubricResults": [
            {
                "criterionId": "crit-001",
                "criterionName": "Test",
                "score": 10.0,
                "maxScore": 10.0,
                "evidence": "test",
                "comment": "test"
            }
        ]
    })


@pytest.fixture
def mock_ai_response_score_exceeds_max() -> str:
    """Mock response với rubric score > maxScore."""
    return json.dumps({
        "transcript": "Test answer",
        "AiScore": 75.0,
        "confidence": 0.85,
        "feedback": {
            "summary": "Good answer",
            "strengths": [],
            "improvements": [],
            "actionableTips": []
        },
        "rubricResults": [
            {
                "criterionId": "crit-001",
                "criterionName": "Test",
                "score": 50.0,  # Invalid: > maxScore
                "maxScore": 30.0,
                "evidence": "test",
                "comment": "test"
            }
        ]
    })


@pytest.fixture
def mock_ai_response_with_markdown_fence() -> str:
    """Mock response wrapped in markdown fence."""
    return """```json
{
    "transcript": "Test answer",
    "AiScore": 80.0,
    "confidence": 0.88,
    "feedback": {
        "summary": "Good answer",
        "strengths": ["Clear explanation"],
        "improvements": [],
        "actionableTips": []
    },
    "rubricResults": [
        {
            "criterionId": "crit-001",
            "criterionName": "Test",
            "score": 25.0,
            "maxScore": 30.0,
            "evidence": "test evidence",
            "comment": "test comment"
        }
    ]
}
```"""


@pytest.fixture
def mock_ai_client_success():
    """Mock AI client that always returns valid response."""
    client = AsyncMock()
    valid_response = json.dumps({
        "score": 85.0,
        "max_score": 100.0,
        "feedback": "Good answer",
        "strengths": ["Clear"],
        "weaknesses": [],
        "missing_content": []
    }, ensure_ascii=False)
    
    # Mock both methods for compatibility
    client.complete.return_value = valid_response
    client.complete_with_retry.return_value = valid_response
    return client


@pytest.fixture
def mock_ai_client_timeout():
    """Mock AI client that times out."""
    client = AsyncMock()
    import asyncio
    client.complete.side_effect = asyncio.TimeoutError("Request timed out")
    return client


@pytest.fixture
def mock_ai_client_malformed():
    """Mock AI client that returns malformed JSON."""
    client = AsyncMock()
    client.complete.return_value = "{ invalid json"
    return client


# ─────────────────────────────────────────────
#  Deterministic unit-test wiring
#  Unit tests must not depend on the live provider configured in `.env`.
#  Live-provider behaviour is covered by tests/test_live_scoring.py (-m integration).
# ─────────────────────────────────────────────
@pytest.fixture(autouse=True)
def _deterministic_unit_providers(monkeypatch):
    from app.api import evaluation as evaluation_api
    from app.api import interview as interview_api
    from app.api import speech as speech_api
    from app.core.ai_client import MockAIClient
    from app.services.evaluation_service import EvaluationService

    mock_ai_client = MockAIClient()
    monkeypatch.setattr(
        evaluation_api, "_service", EvaluationService(ai_client=mock_ai_client)
    )
    monkeypatch.setattr(speech_api._stt_service, "provider", "mock")
    monkeypatch.setattr(speech_api._tts_service, "provider", "mock")
    monkeypatch.setattr(
        interview_api._service.followup_service, "ai_client", mock_ai_client
    )
    monkeypatch.setattr(
        interview_api._service.question_service, "ai_client", mock_ai_client
    )
    yield


# Windows consoles default to cp1252, which cannot print Vietnamese in test output.
import sys as _sys

if hasattr(_sys.stdout, "reconfigure"):
    try:
        _sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        _sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:  # pragma: no cover - best effort only
        pass
