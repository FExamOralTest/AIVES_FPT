"""Live integration tests against the real Gemini provider configured in .env."""

import time

import pytest

from app.models.request import (
    AnswerRubricCriterion,
    DocumentContext,
    EvaluateAnswerRequest,
    GenerateQuestionRequest,
    QuestionAnswerPair,
)
from app.services.evaluation_service import EvaluationService
from app.services.question_service import QuestionService

pytestmark = pytest.mark.integration

COURSE = DocumentContext(
    document_id="doc-content",
    title="Agile Methodology",
    document_type="course_content",
    content=(
        "# Agile\n"
        "Agile is an iterative approach with short cycles and customer collaboration.\n"
        "- Individuals and interactions over processes and tools\n"
        "- Working software over comprehensive documentation\n"
        "- Customer collaboration over contract negotiation\n"
        "- Responding to change over following a plan\n"
        "## Scrum\n"
        "Sprints last 2-4 weeks with daily standup, sprint review and retrospective.\n"
    ),
)

OUTCOMES = DocumentContext(
    document_id="doc-lo",
    title="Learning Outcomes",
    document_type="learning_outcomes",
    content=(
        "- Explain core Agile principles\n"
        "- Compare Agile with Waterfall\n"
        "- Apply Scrum practices\n"
        "- Identify when Agile fits a project\n"
    ),
)

LEARNING_OUTCOMES = [
    "Explain core Agile principles",
    "Compare Agile with Waterfall",
    "Apply Scrum practices",
    "Identify when Agile fits a project",
]

GOOD_ANSWER = (
    "Agile la phuong phap phat trien phan mem lap di lap lai theo sprint ngan 2-4 tuan, "
    "khac voi Waterfall tuan tu va kho thay doi yeu cau. Cac gia tri cot loi gom: ca nhan "
    "hon quy trinh, phan mem hoat dong hon tai lieu, hop tac khach hang hon hop dong, va "
    "phan ung thay doi hon ke hoach cung nhac. Vi du du an cua toi dung Scrum voi sprint "
    "2 tuan, co daily standup moi sang, sprint planning, review va retrospective."
)

POOR_ANSWER = "Agile la phuong phap linh hoat."


def _request(answer: str, rubric: list[AnswerRubricCriterion]) -> EvaluateAnswerRequest:
    return EvaluateAnswerRequest(
        exam_session_id="live-001",
        course_name="Software Engineering",
        documents=[COURSE, OUTCOMES],
        question_answer=QuestionAnswerPair(
            question_id="q-live-1",
            question_text="Giai thich phuong phap Agile va so sanh voi Waterfall",
            student_answer=answer,
            rubric=rubric,
            sample_answer="Agile emphasises iterative delivery and customer collaboration; Scrum uses sprints.",
            learning_outcome="Explain core Agile principles",
        ),
        max_score=10.0,
    )


@pytest.mark.asyncio
async def test_live_good_answer_scores_high():
    rubric = [
        AnswerRubricCriterion(criterion_id="c1", description="Explain core Agile principles accurately", max_score=4.0),
        AnswerRubricCriterion(criterion_id="c2", description="Compare with Waterfall methodology", max_score=3.0),
        AnswerRubricCriterion(criterion_id="c3", description="Provide real-world examples", max_score=3.0),
    ]
    started = time.perf_counter()
    result = await EvaluationService().evaluate_answer(_request(GOOD_ANSWER, rubric))
    elapsed = time.perf_counter() - started

    print(f"[live] good answer score={result.score}/{result.max_score} in {elapsed:.1f}s")
    print(f"[live] feedback={result.feedback}")

    assert result.success is True
    assert 0 <= result.score <= result.max_score <= 10.0
    assert result.feedback.strip()
    assert {c.criterion_id for c in result.criterion_analyses} == {"c1", "c2", "c3"}
    for analysis in result.criterion_analyses:
        assert analysis.judgment in {"met", "partial", "not_met"}
        assert analysis.explanation.strip()
        assert 0 <= analysis.awarded_score <= 4.0
    assert abs(result.score - sum(c.awarded_score for c in result.criterion_analyses)) < 1e-4
    assert result.schema_version == "2.0"
    assert result.provider and result.model
    assert result.score >= 6.5, f"good answer scored only {result.score}"


@pytest.mark.asyncio
async def test_live_poor_answer_scores_low():
    rubric = [
        AnswerRubricCriterion(criterion_id="c1", description="Explain Agile principles with examples", max_score=10.0),
    ]
    started = time.perf_counter()
    result = await EvaluationService().evaluate_answer(_request(POOR_ANSWER, rubric))
    elapsed = time.perf_counter() - started

    print(f"[live] poor answer score={result.score}/{result.max_score} in {elapsed:.1f}s")
    print(f"[live] weaknesses={result.weaknesses}")
    print(f"[live] missing_content={result.missing_content}")

    assert result.success is True
    assert 0 <= result.score <= result.max_score <= 10.0
    assert result.weaknesses or result.missing_content
    assert abs(result.score - sum(c.awarded_score for c in result.criterion_analyses)) < 1e-4
    assert result.score <= 5.0, f"poor answer scored {result.score}, expected <= 5.0"


@pytest.mark.asyncio
async def test_live_question_generation_matches_learning_outcome():
    request = GenerateQuestionRequest(
        exam_session_id="live-002",
        exam_id="exam-001",
        course_name="Software Engineering",
        documents=[COURSE, OUTCOMES],
        question_index=1,
        total_questions=3,
        difficulty="medium",
    )
    started = time.perf_counter()
    result = await QuestionService().generate_question(request)
    elapsed = time.perf_counter() - started

    print(f"[live] generated question in {elapsed:.1f}s: {result.question}")
    print(f"[live] type={result.question_type} difficulty={result.difficulty} topic={result.topic}")

    assert result.success is True
    assert result.question.strip()
    assert result.question_type in {"conceptual", "application", "analysis"}
    assert result.difficulty == "medium"
    assert len(result.expected_keywords) >= 1
    assert result.learning_outcome in LEARNING_OUTCOMES
