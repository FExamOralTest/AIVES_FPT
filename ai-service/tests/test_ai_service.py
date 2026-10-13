import io
import struct
import wave

import pytest
from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)


def make_wav(seconds=1, freq=440, rate=16000):
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        frames = bytearray()
        for i in range(int(rate * seconds)):
            val = int(32767 * 0.3 * (1 if (i // (rate // freq)) % 2 else -1))
            frames += struct.pack("<h", val)
        w.writeframes(bytes(frames))
    return buf.getvalue()


DOCS = [
    {
        "document_id": "d1",
        "title": "Chapter 1",
        "content": "Microservices architecture divides a system into small services that communicate via APIs.",
        "document_type": "course_content",
    }
]


def test_health():
    r = client.get("/health")
    assert r.status_code == 200
    assert r.json()["status"] == "ok"


def test_generate_question():
    r = client.post(
        "/ai/interview/question",
        json={
            "exam_session_id": "s1",
            "exam_id": "e1",
            "course_name": "Software Architecture",
            "documents": DOCS,
            "question_index": 1,
            "total_questions": 5,
            "already_asked_topics": [],
        },
    )
    assert r.status_code == 200
    data = r.json()
    assert data["success"] is True
    assert len(data["question"]) > 0


def test_generate_followup():
    r = client.post(
        "/ai/interview/follow-up",
        json={
            "exam_session_id": "s1",
            "course_name": "Software Architecture",
            "documents": DOCS,
            "original_question": "What is microservices?",
            "student_answer": "It is a way to build apps.",
            "question_index": 1,
        },
    )
    assert r.status_code == 200
    data = r.json()
    assert data["success"] is True
    assert "follow_up_question" in data


def test_evaluate_answer():
    r = client.post(
        "/ai/evaluate/answer",
        json={
            "exam_session_id": "s1",
            "course_name": "Software Architecture",
            "documents": DOCS,
            "question_answer": {
                "question_id": "q1",
                "question_text": "What is microservices?",
                "student_answer": "It is a way to build apps using small services.",
                "is_follow_up": False,
            },
            "max_score": 10.0,
        },
    )
    assert r.status_code == 200
    data = r.json()
    assert data["success"] is True
    assert 0 <= data["score"] <= 10.0


def test_evaluate_session():
    r = client.post(
        "/ai/evaluate/session",
        json={
            "exam_session_id": "s1",
            "student_id": "st1",
            "course_name": "Software Architecture",
            "documents": DOCS,
            "question_answers": [
                {
                    "question_id": "q1",
                    "question_text": "What is microservices?",
                    "student_answer": "It is a way to build apps using small services.",
                    "is_follow_up": False,
                }
            ],
            "max_score_per_question": 10.0,
        },
    )
    assert r.status_code == 200
    data = r.json()
    assert data["success"] is True
    assert data["exam_session_id"] == "s1"
    assert len(data["question_scores"]) == 1


def test_text_to_speech():
    r = client.post(
        "/ai/speech/synthesize",
        json={"text": "Xin chào", "language": "vi-VN"},
    )
    assert r.status_code == 200
    data = r.json()
    assert data["success"] is True
    assert len(data["audio_base64"]) > 0


def test_speech_to_text():
    audio = make_wav()
    r = client.post(
        "/ai/speech/transcribe",
        files={"audio_file": ("test.wav", audio, "audio/wav")},
        data={"exam_session_id": "s1", "language": "vi-VN"},
    )
    assert r.status_code == 200
    data = r.json()
    assert data["success"] is True
    assert "transcript" in data
