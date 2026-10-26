"""
Live verification against the real Gemini API (no mocks).
Run from the ai-service folder:  python verify_live_scoring.py

Covers:
  1. Live scoring  (EvaluationService)  - Case A (good answer) + Case B (weak answer)
  2. Live question generation       (QuestionService)
  3. Live follow-up decision        (FollowUpService)
"""

import asyncio
import json
import sys
import time

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.stderr.reconfigure(encoding="utf-8", errors="replace")

from app.models.request import (
    AnswerRubricCriterion,
    DocumentContext,
    EvaluateAnswerRequest,
    GenerateQuestionRequest,
    QuestionAnswerPair,
)
from app.services.evaluation_service import EvaluationService
from app.services.followup_service import FollowUpService
from app.services.question_service import QuestionService


# ─────────────────────────────────────────────
#  Documents (exactly 1 course_content + 1 learning_outcomes)
# ─────────────────────────────────────────────

COURSE_CONTENT_DOC = DocumentContext(
    document_id="doc-course-1",
    title="Agile Methodology",
    content=(
        "# Agile\n\n"
        "## Core principles\n\n"
        "- Individuals and interactions over processes and tools\n"
        "- Working software over comprehensive documentation\n"
        "- Customer collaboration over contract negotiation\n"
        "- Responding to change over following a plan\n\n"
        "## Scrum\n\n"
        "- A Scrum project is delivered in sprints that last 2-4 weeks.\n"
        "- The team holds a daily standup meeting to synchronise work.\n"
        "- At the end of every sprint the team runs a sprint review with "
        "stakeholders and a retrospective to improve the process.\n"
    ),
    document_type="course_content",
)

LEARNING_OUTCOMES_DOC = DocumentContext(
    document_id="doc-los-1",
    title="Learning Outcomes",
    content=(
        "# Learning Outcomes\n\n"
        "- Explain the core principles of the Agile Manifesto.\n"
        "- Compare Agile with the Waterfall methodology.\n"
        "- Apply Scrum practices including sprints, daily standups, "
        "sprint review and retrospective.\n"
        "- Identify project types where Agile is a good fit.\n"
    ),
    document_type="learning_outcomes",
)

DOCUMENTS = [COURSE_CONTENT_DOC, LEARNING_OUTCOMES_DOC]

LO_PRINCIPLES = "Explain the core principles of the Agile Manifesto."
LO_WATERFALL = "Compare Agile with the Waterfall methodology."
LO_SCRUM = (
    "Apply Scrum practices including sprints, daily standups, "
    "sprint review and retrospective."
)
LO_FIT = "Identify project types where Agile is a good fit."
ALL_LOS = {LO_PRINCIPLES, LO_WATERFALL, LO_SCRUM, LO_FIT}

COURSE_NAME = "Software Engineering"


# ─────────────────────────────────────────────
#  Case A - GOOD answer
# ─────────────────────────────────────────────

CASE_A_ANSWER = (
    "Phương pháp Agile là một cách tiếp cận lặp (iterative) và tăng dần, trong đó "
    "dự án được chia thành các sprint ngắn thay vì thực hiện tuần tự một lần từ đầu "
    "đến cuối. Khác với mô hình Waterfall vốn đi theo trình tự thu hoạch, thiết kế, "
    "phát triển, kiểm thử và rất khó thay đổi yêu cầu khi đã vào giai đoạn sau, Agile "
    "cho phép khách hàng điều chỉnh yêu cầu sau mỗi sprint. Bốn giá trị cốt lõi của "
    "Agile Manifesto là: cá nhân và tương tác hơn là quy trình và công cụ; phần mềm "
    "chạy được hơn là tài liệu đầy đủ; cộng tác với khách hàng hơn là đàm phán hợp "
    "đồng; và sẵn sàng đáp ứng thay đổi hơn là bám theo kế hoạch. Trong một dự án "
    "thực tế, nhóm của tôi đã áp dụng Scrum với sprint 2 tuần: mỗi sáng có daily "
    "standup để đồng bộ tiến độ, cuối mỗi sprint có sprint review trình bày sản phẩm "
    "cho khách hàng và retrospective để nhóm rút kinh nghiệm cải tiến quy trình."
)

CASE_A_PAIR = QuestionAnswerPair(
    question_id="q-case-a",
    question_text="Giải thích phương pháp Agile và so sánh nó với mô hình Waterfall.",
    student_answer=CASE_A_ANSWER,
    is_follow_up=False,
    sample_answer=(
        "Agile phát triển sản phẩm theo các vòng lặp ngắn, ưu tiên một bản chạy được "
        "sau mỗi sprint, với Scrum sprint kéo dài 2-4 tuần kèm daily standup, sprint "
        "review và retrospective."
    ),
    learning_outcome=LO_WATERFALL,
    topic="Agile Methodology",
    max_score=10.0,
    expected_keywords=["Agile", "iterative", "Waterfall", "Agile Manifesto", "Scrum"],
    rubric=[
        AnswerRubricCriterion(
            criterion_id="c1",
            description="Explain core Agile principles accurately",
            max_score=4.0,
        ),
        AnswerRubricCriterion(
            criterion_id="c2",
            description="Compare with Waterfall methodology",
            max_score=3.0,
        ),
        AnswerRubricCriterion(
            criterion_id="c3",
            description="Provide real-world examples",
            max_score=3.0,
        ),
    ],
)

CASE_A_REQUEST = EvaluateAnswerRequest(
    exam_session_id="s1",
    course_name=COURSE_NAME,
    documents=DOCUMENTS,
    question_answer=CASE_A_PAIR,
    max_score=10.0,
)


# ─────────────────────────────────────────────
#  Case B - WEAK answer
# ─────────────────────────────────────────────

CASE_B_PAIR = QuestionAnswerPair(
    question_id="q-case-b",
    question_text="Giải thích phương pháp Agile.",
    student_answer="Agile là phương pháp linh hoạt.",
    is_follow_up=False,
    sample_answer="Agile là cách phát triển sản phẩm theo các vòng lặp ngắn; Scrum sprint 2-4 tuần.",
    learning_outcome=LO_PRINCIPLES,
    topic="Agile Methodology",
    max_score=10.0,
    expected_keywords=["Agile", "Agile Manifesto"],
    rubric=[
        AnswerRubricCriterion(
            criterion_id="c1",
            description="Explain what Agile methodology is",
            max_score=10.0,
        ),
    ],
)

CASE_B_REQUEST = EvaluateAnswerRequest(
    exam_session_id="s1",
    course_name=COURSE_NAME,
    documents=DOCUMENTS,
    question_answer=CASE_B_PAIR,
    max_score=10.0,
)


# ─────────────────────────────────────────────
#  Timing instrumentation (services create their own Gemini clients from .env)
# ─────────────────────────────────────────────

def _instrument(client, label: str) -> None:
    if getattr(client, "_instrumented", False):
        return
    original_complete = client.complete
    original_retry = client.complete_with_retry

    async def timed_complete(system_prompt: str, user_message: str) -> str:
        t0 = time.perf_counter()
        try:
            return await original_complete(system_prompt, user_message)
        finally:
            print(f"  [TIMING] {label}.complete = {time.perf_counter() - t0:.2f}s")

    async def timed_retry(system_prompt: str, user_message: str,
                          max_retries=None, retry_delay=None) -> str:
        t0 = time.perf_counter()
        try:
            return await original_retry(
                system_prompt, user_message,
                max_retries=max_retries, retry_delay=retry_delay,
            )
        finally:
            print(f"  [TIMING] {label}.complete_with_retry = {time.perf_counter() - t0:.2f}s")

    client.complete = timed_complete
    client.complete_with_retry = timed_retry
    client._instrumented = True


def _criterion_line(analysis) -> str:
    explanation = analysis.explanation
    if len(explanation) > 120:
        explanation = explanation[:120] + "..."
    return (f"    - criterion_id={analysis.criterion_id} | "
            f"judgment={analysis.judgment} | explanation={explanation}")


def _print_case(name: str, call_seconds: float, result) -> None:
    print(f"\n=== {name} (service call = {call_seconds:.2f}s) ===")
    print(f"score        : {result.score}")
    print(f"max_score    : {result.max_score}")
    print(f"feedback     : {result.feedback}")
    print(f"strengths    : {result.strengths}")
    print(f"weaknesses   : {result.weaknesses}")
    print(f"missing_content: {result.missing_content}")
    print(f"criterion_analyses: {len(result.criterion_analyses)}")
    for analysis in result.criterion_analyses:
        print(_criterion_line(analysis))


async def main() -> None:
    print("=" * 70)
    print("LIVE GEMINI VERIFICATION (no mocks, AI_PROVIDER from .env)")
    print("=" * 70)

    evaluation = EvaluationService()
    question_service = QuestionService()
    followup_service = FollowUpService()
    _instrument(evaluation.scoring.ai_client, "ScoringService")
    _instrument(evaluation.analysis.ai_client, "FeedbackAnalysis")
    _instrument(question_service.ai_client, "QuestionService")
    _instrument(followup_service.ai_client, "FollowUpService")

    results = {"case_a": None, "case_b": None, "question": None, "followup": None}

    # ── VIỆC 2: live scoring ──────────────────────────────
    for key, name, request in (
        ("case_a", "CASE A (good answer)", CASE_A_REQUEST),
        ("case_b", "CASE B (weak answer)", CASE_B_REQUEST),
    ):
        t0 = time.perf_counter()
        result = await evaluation.evaluate_answer(request)
        results[key] = result
        _print_case(name, time.perf_counter() - t0, result)

    # ── VIỆC 3: live question generation ──────────────────
    print("\n=== QUESTION GENERATION ===")
    t0 = time.perf_counter()
    generated = await question_service.generate_question(
        GenerateQuestionRequest(
            exam_session_id="s1",
            exam_id="e1",
            course_name=COURSE_NAME,
            documents=DOCUMENTS,
            question_index=1,
            total_questions=3,
            difficulty="medium",
        )
    )
    results["question"] = generated
    print(f"  [TIMING] generate_question = {time.perf_counter() - t0:.2f}s")
    print(f"question          : {generated.question}")
    print(f"question_type     : {generated.question_type}")
    print(f"difficulty        : {generated.difficulty}")
    print(f"topic             : {generated.topic}")
    print(f"expected_keywords : {generated.expected_keywords}")
    print(f"learning_outcome  : {generated.learning_outcome}")

    # ── VIỆC 3: live follow-up ────────────────────────────
    print("\n=== FOLLOW-UP (weak answer) ===")
    t0 = time.perf_counter()
    followup = await followup_service.generate_follow_up(
        course_name=COURSE_NAME,
        documents=DOCUMENTS,
        original_question="Giải thích phương pháp Agile",
        student_answer=CASE_B_PAIR.student_answer,
        previous_questions=["Giải thích phương pháp Agile"],
    )
    results["followup"] = followup
    print(f"  [TIMING] generate_follow_up = {time.perf_counter() - t0:.2f}s")
    print(f"should_follow_up   : {followup.should_follow_up}")
    print(f"follow_up_question : {followup.follow_up_question}")
    print(f"reason             : {followup.reason}")
    print(f"missing_points     : {followup.missing_points}")

    # ── Assertions ────────────────────────────────────────
    print("\n" + "=" * 70)
    print("ASSERTIONS")
    print("=" * 70)
    case_a = results["case_a"]
    case_b = results["case_b"]
    question = results["question"]
    followup = results["followup"]

    def check(label: str, condition: bool) -> None:
        print(f"  [{'PASS' if condition else 'FAIL'}] {label}")

    check("Case A success is True", case_a.success is True)
    check("Case A 0 <= score <= max_score", 0 <= case_a.score <= case_a.max_score)
    check("Case A feedback not empty", bool(case_a.feedback.strip()))
    check("Case A score >= 6.5", case_a.score >= 6.5)
    check("Case B score <= 5.0", case_b.score <= 5.0)

    ids = {a.criterion_id for a in case_a.criterion_analyses}
    check("Case A criterion ids == {c1, c2, c3}", ids == {"c1", "c2", "c3"})

    check("Question not empty", bool(question.question.strip()))
    check("Question learning_outcome in supplied LOs",
          question.learning_outcome in ALL_LOS)

    check("should_follow_up is bool", isinstance(followup.should_follow_up, bool))
    if followup.should_follow_up:
        check("follow_up_question not empty when should_follow_up=True",
              bool(followup.follow_up_question.strip()))

    failures = []
    if case_a.success is not True:
        failures.append("Case A success is not True")
    if not (0 <= case_a.score <= case_a.max_score):
        failures.append("Case A score out of range")
    if not case_a.feedback.strip():
        failures.append("Case A feedback empty")
    if case_a.score < 6.5:
        failures.append("Case A score < 6.5 (score thực tế = "
                        f"{case_a.score})")
    if case_b.score > 5.0:
        failures.append("Case B score > 5.0 (score thực tế = "
                        f"{case_b.score})")
    if ids != {"c1", "c2", "c3"}:
        failures.append(f"Case A criterion ids = {ids}, expected c1/c2/c3")
    if not question.question.strip():
        failures.append("Question empty")
    if question.learning_outcome not in ALL_LOS:
        failures.append(f"learning_outcome mismatch: {question.learning_outcome}")
    if followup.should_follow_up and not followup.follow_up_question.strip():
        failures.append("follow_up_question empty while should_follow_up=True")

    print()
    if failures:
        print("RESULT: FAIL")
        for failure in failures:
            print(f"  - {failure}")
        raise SystemExit(1)
    print("RESULT: ALL LIVE CHECKS PASSED")


if __name__ == "__main__":
    asyncio.run(main())
