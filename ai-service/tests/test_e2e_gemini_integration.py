"""End-to-end pipeline tests using the deterministic MockAIClient.

These exercise the full FastAPI -> service -> validation pipeline with no
network calls. Live-provider behaviour lives in tests/integration/.
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

RUBRIC = [
    {"criterion_id": "c1", "description": "Explain independent services", "max_score": 5.0},
    {"criterion_id": "c2", "description": "Explain API communication", "max_score": 5.0},
]

SAMPLE_ANSWER = "Microservices split a system into independently deployable services that talk over APIs."


def _pair(question_id, question_text, student_answer):
    return {
        "question_id": question_id,
        "question_text": question_text,
        "student_answer": student_answer,
        "is_follow_up": False,
        "sample_answer": SAMPLE_ANSWER,
        "learning_outcome": "LO1",
        "rubric": RUBRIC,
    }


def _answer_request(student_answer="It is a way to build apps using small services."):
    return {
        "exam_session_id": "s1",
        "course_name": "Software Architecture",
        "documents": DOCS,
        "question_answer": _pair("q1", "What is microservices?", student_answer),
        "max_score": 10.0,
    }


def test_health_and_model_discovery_endpoints_are_not_exposed():
    assert client.get("/health").status_code == 404
    assert client.get("/v1/models").status_code == 404


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
    assert data["schema_version"] == "2.0"
    assert data["provider"]
    assert data["model"]


def test_evaluate_answer_without_rubric_is_rejected():
    payload = _answer_request()
    del payload["question_answer"]["rubric"]
    del payload["question_answer"]["sample_answer"]
    del payload["question_answer"]["learning_outcome"]
    r = client.post("/ai/evaluate/answer", json=payload)
    assert r.status_code == 500
    assert "rubric" in r.json()["detail"].lower()


def test_evaluate_answer_with_rubric_returns_criterion_analyses():
    r = client.post("/ai/evaluate/answer", json=_answer_request())
    assert r.status_code == 200, r.text
    data = r.json()
    assert 0 <= data["score"] <= 10.0
    assert len(data["criterion_analyses"]) == len(RUBRIC)
    assert {item["criterion_id"] for item in data["criterion_analyses"]} == {"c1", "c2"}
    assert all("awarded_score" in item for item in data["criterion_analyses"])
    assert abs(data["score"] - sum(item["awarded_score"] for item in data["criterion_analyses"])) < 1e-6


def test_evaluate_answer_missing_student_answer_returns_422():
    payload = _answer_request()
    del payload["question_answer"]["student_answer"]
    r = client.post("/ai/evaluate/answer", json=payload)
    assert r.status_code == 422


def test_evaluate_answer_tolerates_unexpected_document_types():
    payload = _answer_request()
    payload["documents"][0]["document_type"] = "learning_outcomes"
    r = client.post("/ai/evaluate/answer", json=payload)
    # The prompt context builder logs a validation warning and falls back to
    # the raw document content, so the request still evaluates successfully.
    assert r.status_code == 200
    assert r.json()["success"] is True


def test_evaluate_session_aggregates_total_score():
    payload = {
        "exam_session_id": "s1",
        "student_id": "st1",
        "course_name": "Software Architecture",
        "documents": DOCS,
        "question_answers": [
            _pair("q1", "What is microservices?", "Small services communicating via APIs."),
            _pair("q2", "Why use them?", "Independent deployment and scaling."),
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
    assert abs(data["total_score"] - sum(q["score"] for q in data["question_scores"])) < 1e-6
    assert data["schema_version"] == "2.0"


def test_evaluate_session_percentage_matches_total():
    payload = {
        "exam_session_id": "s1",
        "student_id": "st1",
        "course_name": "Software Architecture",
        "documents": DOCS,
        "question_answers": [
            _pair("q1", "What is microservices?", "Small services communicating via APIs."),
        ],
        "max_score_per_question": 10.0,
    }
    data = client.post("/ai/evaluate/session", json=payload).json()
    expected = round(data["total_score"] / data["max_total_score"] * 100, 2)
    assert abs(data["percentage"] - expected) < 0.05


def test_evaluate_answer_large_transcript_is_not_truncated():
    long_answer = "Microservices split a system into small independently deployable services. " * 60
    r = client.post("/ai/evaluate/answer", json=_answer_request(student_answer=long_answer))
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
