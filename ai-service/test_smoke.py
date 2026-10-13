import asyncio
import io
import struct
import wave

import httpx

BASE = "http://localhost:8000"


def safe(s: str) -> str:
    return s.encode("ascii", "replace").decode("ascii")


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


async def main():
    async with httpx.AsyncClient(timeout=180.0) as c:
        docs = [
            {
                "document_id": "d1",
                "title": "Chapter 1",
                "content": "Microservices architecture divides a system into small services that communicate via APIs.",
                "document_type": "course_content",
            }
        ]

        r = await c.post(
            f"{BASE}/ai/interview/question",
            json={
                "exam_session_id": "s1",
                "exam_id": "e1",
                "course_name": "Software Architecture",
                "documents": docs,
                "question_index": 1,
                "total_questions": 5,
                "already_asked_topics": [],
            },
        )
        print("QUESTION", r.status_code, safe(r.text[:300]))

        r = await c.post(
            f"{BASE}/ai/interview/follow-up",
            json={
                "exam_session_id": "s1",
                "course_name": "Software Architecture",
                "documents": docs,
                "original_question": "What is microservices?",
                "student_answer": "It is a way to build apps.",
                "question_index": 1,
            },
        )
        print("FOLLOWUP", r.status_code, safe(r.text[:300]))

        r = await c.post(
            f"{BASE}/ai/evaluate/answer",
            json={
                "exam_session_id": "s1",
                "course_name": "Software Architecture",
                "documents": docs,
                "question_answer": {
                    "question_id": "q1",
                    "question_text": "What is microservices?",
                    "student_answer": "It is a way to build apps using small services.",
                    "is_follow_up": False,
                },
                "max_score": 10.0,
            },
        )
        print("EVAL_ANSWER", r.status_code, safe(r.text[:300]))

        r = await c.post(
            f"{BASE}/ai/evaluate/session",
            json={
                "exam_session_id": "s1",
                "student_id": "st1",
                "course_name": "Software Architecture",
                "documents": docs,
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
        print("EVAL_SESSION", r.status_code, safe(r.text[:300]))

        r = await c.post(
            f"{BASE}/ai/speech/synthesize",
            json={"text": "Xin chào, đây là câu hỏi vấn đáp.", "language": "vi-VN"},
        )
        print("TTS", r.status_code, safe(r.text[:200]))

        audio = make_wav()
        r = await c.post(
            f"{BASE}/ai/speech/transcribe",
            files={"audio_file": ("test.wav", audio, "audio/wav")},
            data={"exam_session_id": "s1", "language": "vi-VN"},
        )
        print("STT", r.status_code, safe(r.text[:300]))


asyncio.run(main())
