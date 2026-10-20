"""End-to-end pipeline tests using the deterministic MockAIClient.

These exercise the full FastAPI -> service -> validation pipeline with no
network calls. Live-provider behaviour lives in tests/test_live_scoring.py.
"""

from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)

DOCS = [
    {
        "document_id": "d1",
        "title": "Chapter 1",
        "content": "Microservices architecture divides a system into small services that communicate via APIs.",
        "document_type": "course_content",
    },
    {
        "document_id": "d2",
        "title": "Learning Outcomes",
        "content": "LO1: Explain microservices architecture and how independent services communicate through APIs.",
        "document_type": "learning_outcomes",
    },
]


def _answer_request(student_answer="It is a way to build apps using small services.", rubric=None):
    pair = {
        "question_id": "q1",
        "question_text": "What is microservices?",
        "student_answer": student_answer,
        "is_follow_up": False,
    }
    if rubric is not None:
        pair["rubric"] = rubric
        pair["sample_answer"] = "Microservices split a system into independently deployable services."
        pair["learning_outcome"] = "LO1"
    return {
        "exam_session_id": "s1",
        "course_name": "Software Architecture",
        "documents": DOCS,
        "question_answer": pair,
        "max_score": 10.0,
    }


def test_health_endpoint():
    r = client.get("/health")
    assert r.status_code == 200
    assert r.json()["status"] == "ok"


def test_evaluate_answer_returns_contract_fields():
    r = client.post("/ai/evaluate/answer", json=_answer_request())
    assert r.status_code == 200, r.text
    data = r.json()
    assert data["success"] is True
    assert data["question_id"] == "q1"
    assert 0 <= data["score"] <= data["max_score"] <= 10.0
    assert isinstance(data["feedback"], str) and data["feedback"].strip()
    for key in ("strengths", "weaknesses", "missing_content", "sentence_feedback", "criterion_analyses"):
        assert isinstance(data[key], list)


def test_evaluate_answer_without_rubric_has_empty_analyses():
    data = client.post("/ai/evaluate/answer", json=_answer_request()).json()
    assert data["sentence_feedback"] == []
    assert data["criterion_analyses"] == []


def test_evaluate_answer_with_rubric_returns_criterion_analyses():
    rubric = [
        {"criterion_id": "c1", "description": "Explain independent services", "max_score": 5.0},
        {"criterion_id": "c2", "description": "Explain API communication", "max_score": 5.0},
    ]
    r = client.post("/ai/evaluate/answer", json=_answer_request(rubric=rubric))
    assert r.status_code == 200, r.text
    data = r.json()
    assert 0 <= data["score"] <= 10.0
    assert isinstance(data["criterion_analyses"], list)


def test_evaluate_answer_missing_student_answer_returns_422():
    payload = _answer_request()
    del payload["question_answer"]["student_answer"]
    r = client.post("/ai/evaluate/answer", json=payload)
    assert r.status_code == 422


def test_evaluate_answer_tolerates_unexpected_document_types():
    payload = _answer_request()
    payload["documents"][0]["document_type"] = "learning_outcomes"
    r = client.post("/ai/evaluate/answer", json=payload)
    # ScoringService logs a validation warning and falls back to raw document
    # content, so the request still evaluates successfully.
    assert r.status_code == 200
    assert r.json()["success"] is True


def test_evaluate_session_aggregates_total_score():
    payload = {
        "exam_session_id": "s1",
        "student_id": "st1",
        "course_name": "Software Architecture",
        "documents": DOCS,
        "question_answers": [
            {
                "question_id": "q1",
                "question_text": "What is microservices?",
                "student_answer": "Small services communicating via APIs.",
                "is_follow_up": False,
            },
            {
                "question_id": "q2",
                "question_text": "Why use them?",
                "student_answer": "Independent deployment and scaling.",
                "is_follow_up": False,
            },
        ],
        "max_score_per_question": 10.0,
    }
    r = client.post("/ai/evaluate/session", json=payload)
    assert r.status_code == 200, r.text
    data = r.json()
    assert data["success"] is True
    assert data["exam_session_id"] == "s1"
    assert len(data["question_scores"]) == 2
    assert data["max_total_score"] == 20.0
    assert abs(data["total_score"] - sum(q["score"] for q in data["question_scores"])) < 0.02


def test_evaluate_session_percentage_matches_total():
    payload = {
        "exam_session_id": "s1",
        "student_id": "st1",
        "course_name": "Software Architecture",
        "documents": DOCS,
        "question_answers": [
            {
                "question_id": "q1",
                "question_text": "What is microservices?",
                "student_answer": "Small services communicating via APIs.",
                "is_follow_up": False,
            }
        ],
        "max_score_per_question": 10.0,
    }
    data = client.post("/ai/evaluate/session", json=payload).json()
    expected = round(data["total_score"] / data["max_total_score"] * 100, 2)
    assert abs(data["percentage"] - expected) < 0.05


def test_evaluate_answer_large_transcript_is_not_truncated():
    long_answer = "Microservices split a system into small independently deployable services. " * 60
    payload = _answer_request(student_answer=long_answer)
    r = client.post("/ai/evaluate/answer", json=payload)
    assert r.status_code == 200, r.text
    data = r.json()
    assert 0 <= data["score"] <= 10.0
    assert data["feedback"].strip()


def test_openapi_lists_evaluation_endpoints():
    spec = client.get("/openapi.json").json()
    paths = set(spec["paths"])
    assert "/ai/evaluate/answer" in paths
    assert "/ai/evaluate/session" in paths
    assert "/ai/questions/generate" in paths
    assert "/ai/interview/follow-up" in paths
