"""
AIVES AI Service - Test Fixtures
Shared fixtures và mocks cho toàn bộ test suite.
"""

import pytest

from app.core.ai_client import MockAIClient
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
def rubric() -> list[AnswerRubricCriterion]:
    """Rubric whose max scores add up to the 10-point question maximum."""
    return [
        AnswerRubricCriterion(
            criterion_id="crit-001",
            description="Giải thích đầy đủ các nguyên tắc cốt lõi của Agile",
            max_score=3.0
        ),
        AnswerRubricCriterion(
            criterion_id="crit-002",
            description="Nêu được ví dụ thực tế về áp dụng Agile",
            max_score=3.0
        ),
        AnswerRubricCriterion(
            criterion_id="crit-003",
            description="So sánh Agile với các phương pháp khác",
            max_score=2.0
        ),
        AnswerRubricCriterion(
            criterion_id="crit-004",
            description="Nêu ưu điểm của Agile trong phát triển phần mềm",
            max_score=2.0
        ),
    ]


@pytest.fixture
def mock_question_answer_good(rubric) -> QuestionAnswerPair:
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
        max_score=10.0,
        rubric=rubric,
    )


@pytest.fixture
def mock_question_answer_poor(rubric) -> QuestionAnswerPair:
    """Mock Q&A pair với câu trả lời yếu."""
    return QuestionAnswerPair(
        question_id="q-002",
        question_text="Giải thích phương pháp Agile và ưu điểm của nó trong phát triển phần mềm.",
        student_answer="Agile là một phương pháp phát triển phần mềm. Nó tốt hơn các phương pháp khác.",
        sample_answer="Agile emphasizes iterative development, customer collaboration, and flexibility.",
        learning_outcome="Explain the principles and practices of Agile software development",
        topic="Agile Methodology",
        max_score=10.0,
        rubric=rubric,
    )


@pytest.fixture
def fake_analysis_client() -> MockAIClient:
    """Deterministic AI client that returns a rubric analysis with awarded_score."""
    return MockAIClient()


# ─────────────────────────────────────────────
#  Deterministic unit-test wiring
#  Unit tests must not depend on the live provider configured in `.env`.
#  Live-provider behaviour is covered by tests/integration/ (-m integration).
# ─────────────────────────────────────────────
@pytest.fixture(autouse=True)
def _deterministic_unit_providers(monkeypatch):
    from app.api import evaluation as evaluation_api
    from app.api import interview as interview_api
    from app.api import speech as speech_api
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
