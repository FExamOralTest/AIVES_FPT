"""Live rubric-driven scoring against the real Gemini provider.

These tests exist to prove the rubric invariants hold with a real model, not
just with the deterministic mock:

  * score == sum of awarded_score
  * exactly one analysis per rubric criterion
  * awarded_score stays inside [0, criterion.max_score]
  * judgment and awarded_score agree
  * every evidence quote is a substring of the transcript
"""

import pytest

from app.models.request import (
    AnswerRubricCriterion,
    DocumentContext,
    EvaluateAnswerRequest,
    QuestionAnswerPair,
)
from app.services.evaluation_service import EvaluationService

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
    ),
)

TRANSCRIPT = (
    "Agile is an iterative approach that delivers software in short cycles. "
    "It values individuals and interactions over processes and tools, and working "
    "software over comprehensive documentation. Customer collaboration matters more "
    "than contract negotiation, and the team responds to change instead of following "
    "a fixed plan. Unlike Waterfall, which is sequential and hard to change, Agile "
    "welcomes requirement changes after every sprint. In my last project we used "
    "Scrum with two-week sprints, a daily standup, a sprint review and a retrospective."
)

RUBRIC_A = [
    AnswerRubricCriterion(criterion_id="c1", description="Explain the core Agile principles", max_score=4.0),
    AnswerRubricCriterion(criterion_id="c2", description="Compare Agile with Waterfall", max_score=3.0),
    AnswerRubricCriterion(criterion_id="c3", description="Describe Scrum practices used in a real project", max_score=3.0),
]

RUBRIC_B = [
    AnswerRubricCriterion(criterion_id="k1", description="Explain database normalization", max_score=5.0),
    AnswerRubricCriterion(criterion_id="k2", description="Explain SQL join types", max_score=5.0),
]


def _request(rubric: list[AnswerRubricCriterion]) -> EvaluateAnswerRequest:
    return EvaluateAnswerRequest(
        exam_session_id="live-rubric-001",
        course_name="Software Engineering",
        documents=[COURSE, OUTCOMES],
        question_answer=QuestionAnswerPair(
            question_id="q-live-rubric-1",
            question_text="Explain Agile, compare it with Waterfall, and describe Scrum.",
            student_answer=TRANSCRIPT,
            sample_answer=(
                "Agile delivers software iteratively with customer collaboration; "
                "Waterfall is sequential; Scrum uses sprints, standups and reviews."
            ),
            learning_outcome="Explain core Agile principles",
            topic="Agile Methodology",
            rubric=rubric,
            max_score=10.0,
        ),
        max_score=10.0,
    )


def _assert_invariants(result, rubric: list[AnswerRubricCriterion]) -> None:
    by_id = {criterion.criterion_id: criterion for criterion in rubric}
    analyses = {item.criterion_id: item for item in result.criterion_analyses}

    assert len(result.criterion_analyses) == len(rubric)
    assert set(analyses) == set(by_id)

    for criterion_id, analysis in analyses.items():
        maximum = by_id[criterion_id].max_score
        assert 0 <= analysis.awarded_score <= maximum
        if analysis.judgment == "met":
            assert analysis.awarded_score == pytest.approx(maximum)
        elif analysis.judgment == "not_met":
            assert analysis.awarded_score == 0
        else:
            assert 0 < analysis.awarded_score < maximum

        for quote in analysis.evidence_quotes:
            assert quote in TRANSCRIPT

    assert result.score == pytest.approx(
        sum(item.awarded_score for item in result.criterion_analyses)
    )
    assert result.max_score == pytest.approx(10.0)
    assert result.schema_version == "2.0"
    assert result.provider and result.model


@pytest.mark.asyncio
async def test_live_score_is_the_sum_of_awarded_scores():
    result = await EvaluationService().evaluate_answer(_request(RUBRIC_A))

    print(f"[live] rubric A score={result.score}/{result.max_score}")
    for analysis in result.criterion_analyses:
        print(
            f"[live]   {analysis.criterion_id} judgment={analysis.judgment} "
            f"awarded={analysis.awarded_score} quotes={analysis.evidence_quotes}"
        )

    _assert_invariants(result, RUBRIC_A)


@pytest.mark.asyncio
async def test_live_unrelated_rubric_awards_no_points():
    service = EvaluationService()
    first = await service.evaluate_answer(_request(RUBRIC_A))
    second = await service.evaluate_answer(_request(RUBRIC_B))

    print(f"[live] matching rubric score={first.score} | unrelated rubric score={second.score}")

    _assert_invariants(first, RUBRIC_A)
    _assert_invariants(second, RUBRIC_B)

    # The transcript says nothing about databases, so the same answer must score
    # lower under a database rubric: points come from the rubric, not a fixed mix.
    assert first.score > second.score
    assert second.score == 0
