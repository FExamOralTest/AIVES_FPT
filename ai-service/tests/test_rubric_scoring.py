"""Rubric-driven scoring: points come from awarded_score, never from a fixed weight."""

import json
from unittest.mock import AsyncMock

import pytest

from app.core.ai_client import MockAIClient
from app.models.request import (
    AnswerRubricCriterion,
    EvaluateAnswerRequest,
    EvaluateSessionRequest,
    QuestionAnswerPair,
)
from app.services.evaluation_service import EvaluationService

TRANSCRIPT = "Agile uses short iterations. Waterfall is sequential."
SENTENCES = ["Agile uses short iterations.", "Waterfall is sequential."]

RUBRIC = [
    AnswerRubricCriterion(criterion_id="c1", description="Explain short iterations", max_score=6.0),
    AnswerRubricCriterion(criterion_id="c2", description="Compare with Waterfall", max_score=4.0),
]


def _pair(
    transcript: str = TRANSCRIPT,
    rubric: list[AnswerRubricCriterion] | None = None,
    max_score: float = 10.0,
    question_id: str = "q1",
) -> QuestionAnswerPair:
    return QuestionAnswerPair(
        question_id=question_id,
        question_text="Explain Agile and compare it with Waterfall.",
        student_answer=transcript,
        sample_answer="Agile uses short iterations; Waterfall is sequential.",
        learning_outcome="Explain Agile principles",
        topic="Agile",
        max_score=max_score,
        rubric=RUBRIC if rubric is None else rubric,
    )


def _request(pair: QuestionAnswerPair, max_score: float = 10.0) -> EvaluateAnswerRequest:
    return EvaluateAnswerRequest(
        exam_session_id="s1",
        course_name="Software Engineering",
        documents=[],
        question_answer=pair,
        max_score=max_score,
    )


def _criterion(
    criterion_id: str,
    judgment: str,
    awarded: float,
    quote: str | None = "Agile uses short iterations",
) -> dict:
    return {
        "criterion_id": criterion_id,
        "judgment": judgment,
        "awarded_score": awarded,
        "explanation": f"{criterion_id} explanation",
        "errors": ["wrong claim"] if judgment == "not_met" else [],
        "omissions": ["missing idea"] if judgment != "met" else [],
        "evidence_quotes": [] if quote is None else [quote],
    }


def _analysis(criteria: list[dict], sentences: list[str] | None = None) -> str:
    return json.dumps({
        "sentence_feedback": [
            {"sentence": sentence, "judgment": "correct", "comment": "relevant"}
            for sentence in (SENTENCES if sentences is None else sentences)
        ],
        "criterion_analyses": criteria,
        "strengths": ["covered idea"],
        "weaknesses": ["gap"],
        "missing_content": ["gap"],
        "improvements": ["add the missing idea"],
        "feedback": "Summary of the answer.",
    })


def _client(payload: str) -> AsyncMock:
    client = AsyncMock()
    client.complete_with_retry.return_value = payload
    return client


async def _evaluate(payload: str, pair: QuestionAnswerPair | None = None):
    service = EvaluationService(ai_client=_client(payload))
    return await service.evaluate_answer(_request(pair or _pair()))


@pytest.mark.asyncio
async def test_question_score_is_the_sum_of_awarded_scores():
    payload = _analysis([
        _criterion("c1", "met", 6.0),
        _criterion("c2", "partial", 2.0, quote="Waterfall is sequential"),
    ])

    result = await _evaluate(payload)

    assert result.score == 8.0
    assert result.score == pytest.approx(
        sum(item.awarded_score for item in result.criterion_analyses)
    )
    assert result.max_score == 10.0


@pytest.mark.asyncio
async def test_misaligned_sentence_feedback_does_not_block_rubric_scoring():
    payload = _analysis(
        [
            _criterion("c1", "met", 6.0),
            _criterion("c2", "partial", 2.0, quote="Waterfall is sequential"),
        ],
        sentences=["The answer discusses Agile.", "It compares methodologies."],
    )

    result = await _evaluate(payload)

    assert result.score == 8.0
    assert result.sentence_feedback == []


@pytest.mark.asyncio
async def test_incomplete_sentence_feedback_does_not_block_rubric_scoring():
    payload = _analysis(
        [
            _criterion("c1", "met", 6.0),
            _criterion("c2", "partial", 2.0, quote="Waterfall is sequential"),
        ],
        sentences=["Agile uses short iterations."],
    )

    result = await _evaluate(payload)

    assert result.score == 8.0
    assert result.sentence_feedback == []


@pytest.mark.asyncio
async def test_session_total_is_the_sum_of_question_scores():
    service = EvaluationService(ai_client=MockAIClient())
    request = EvaluateSessionRequest(
        exam_session_id="s1",
        student_id="st1",
        course_name="Software Engineering",
        documents=[],
        question_answers=[
            _pair(
                transcript="Agile uses short iterations.",
                rubric=[AnswerRubricCriterion(criterion_id="c1", description="short iterations", max_score=10.0)],
                question_id="q1",
            ),
            _pair(
                transcript="Waterfall is sequential.",
                rubric=[AnswerRubricCriterion(criterion_id="c2", description="waterfall sequential", max_score=5.0)],
                max_score=5.0,
                question_id="q2",
            ),
        ],
    )

    result = await service.evaluate_session(request)

    assert result.total_score == pytest.approx(
        sum(question.score for question in result.question_scores)
    )
    assert result.max_total_score == pytest.approx(
        sum(question.max_score for question in result.question_scores)
    )
    assert result.max_total_score == 15.0
    assert result.total_score == 15.0
    assert result.percentage == 100.0


@pytest.mark.asyncio
async def test_awarded_score_above_criterion_max_is_rejected():
    payload = _analysis([
        _criterion("c1", "met", 6.5),
        _criterion("c2", "partial", 2.0, quote="Waterfall is sequential"),
    ])

    with pytest.raises(ValueError):
        await _evaluate(payload)


@pytest.mark.asyncio
async def test_missing_criterion_is_rejected():
    payload = _analysis([_criterion("c1", "met", 6.0)])

    with pytest.raises(ValueError):
        await _evaluate(payload)


@pytest.mark.asyncio
async def test_duplicate_criterion_is_rejected():
    payload = _analysis([
        _criterion("c1", "met", 6.0),
        _criterion("c1", "partial", 2.0, quote="Waterfall is sequential"),
    ])

    with pytest.raises(ValueError):
        await _evaluate(payload)


@pytest.mark.asyncio
async def test_quote_absent_from_transcript_is_rejected():
    payload = _analysis([
        _criterion("c1", "met", 6.0, quote="Microservices scale independently"),
        _criterion("c2", "partial", 2.0, quote="Waterfall is sequential"),
    ])

    with pytest.raises(ValueError):
        await _evaluate(payload)


@pytest.mark.asyncio
async def test_invalid_evidence_requests_one_corrected_analysis():
    invalid = _analysis([
        _criterion("c1", "met", 6.0, quote="Agile guarantees faster delivery"),
        _criterion("c2", "partial", 2.0, quote="Waterfall is sequential"),
    ])
    corrected = _analysis([
        _criterion("c1", "met", 6.0, quote="Agile uses short iterations"),
        _criterion("c2", "partial", 2.0, quote="Waterfall is sequential"),
    ])
    client = AsyncMock()
    client.complete_with_retry.side_effect = [invalid, corrected]

    result = await EvaluationService(ai_client=client).evaluate_answer(_request(_pair()))

    assert result.score == 8.0
    assert client.complete_with_retry.await_count == 2
    correction_prompt = client.complete_with_retry.await_args_list[1].args[0]
    correction_payload = json.loads(client.complete_with_retry.await_args_list[1].args[1])
    assert "Required correction" in correction_prompt
    assert correction_payload["previous_analysis"] == invalid
    assert "not in the transcript" in correction_payload["validation_error"]


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("judgment", "awarded"),
    [
        ("met", 3.0),
        ("not_met", 2.0),
        ("partial", 0.0),
        ("partial", 6.0),
    ],
)
async def test_judgment_must_match_awarded_score(judgment: str, awarded: float):
    quote = None if judgment == "not_met" else "Agile uses short iterations"
    payload = _analysis([
        _criterion("c1", judgment, awarded, quote=quote),
        _criterion("c2", "partial", 2.0, quote="Waterfall is sequential"),
    ])

    with pytest.raises(ValueError):
        await _evaluate(payload)


@pytest.mark.asyncio
async def test_empty_transcript_scores_zero_and_marks_every_criterion_not_met():
    service = EvaluationService(ai_client=MockAIClient())
    pair = _pair(transcript="")

    result = await service.evaluate_answer(_request(pair))

    assert result.score == 0
    assert all(item.judgment == "not_met" for item in result.criterion_analyses)
    assert all(item.awarded_score == 0 for item in result.criterion_analyses)


@pytest.mark.asyncio
async def test_same_transcript_with_a_different_rubric_scores_differently():
    service = EvaluationService(ai_client=MockAIClient())
    other_rubric = [
        AnswerRubricCriterion(criterion_id="c1", description="short iterations", max_score=2.0),
        AnswerRubricCriterion(criterion_id="c2", description="microservices deployment", max_score=8.0),
    ]

    first = await service.evaluate_answer(_request(_pair(rubric=RUBRIC)))
    second = await service.evaluate_answer(_request(_pair(rubric=other_rubric)))

    assert first.max_score == second.max_score == 10.0
    assert first.score != second.score


@pytest.mark.asyncio
async def test_missing_rubric_is_rejected_with_a_clear_error():
    service = EvaluationService(ai_client=MockAIClient())
    pair = _pair()
    pair.rubric = []

    with pytest.raises(ValueError) as exc_info:
        await service.evaluate_answer(_request(pair))

    assert "rubric is required" in str(exc_info.value).lower()
