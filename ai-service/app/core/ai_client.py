"""
AIVES AI Service - AI Client
Abstract base + concrete implementations for each AI provider.
Swap providers by changing AI_PROVIDER env var.
"""

from __future__ import annotations

import abc
import json
from typing import Optional

import httpx
from loguru import logger

from app.core.config import get_settings

settings = get_settings()


# ─────────────────────────────────────────────
#  Abstract Base Client
# ─────────────────────────────────────────────


class BaseAIClient(abc.ABC):
    """Abstract AI client. All providers must implement `complete()`."""

    @abc.abstractmethod
    async def complete(self, system_prompt: str, user_message: str) -> str:
        """
        Send a prompt to the LLM and return the text response.

        Args:
            system_prompt: The system/context instructions for the AI.
            user_message: The user's input message.

        Returns:
            The AI's text response.
        """
        ...


# ─────────────────────────────────────────────
#  Mock Client (for development/testing)
# ─────────────────────────────────────────────


class MockAIClient(BaseAIClient):
    """
    Mock AI client that returns realistic placeholder responses.
    Use in development without needing real API keys.
    """

    async def complete(self, system_prompt: str, user_message: str) -> str:
        logger.debug("[MockAIClient] Returning mock response.")

        # Structured task routing keeps generated metadata tied to the documents.
        try:
            payload = json.loads(user_message)
        except json.JSONDecodeError:
            payload = None
        if isinstance(payload, dict) and payload.get("task") == "generate_viva_question":
            context = payload["context"]
            topic = context["available_topics"][0]
            question_type = context["question_type"]
            templates = {
                "conceptual": "Bạn hãy giải thích nội dung cốt lõi của chủ đề {topic} trong môn {course}.",
                "application": "Bạn sẽ vận dụng kiến thức về {topic} vào một tình huống thực tế như thế nào?",
                "analysis": "Bạn hãy phân tích các yếu tố quan trọng của {topic} và giải thích mối liên hệ giữa chúng.",
            }
            return json.dumps(
                {
                    "question": templates[question_type].format(
                        topic=topic, course=context["course_name"]
                    ),
                    "question_type": question_type,
                    "difficulty": context["difficulty"],
                    "expected_keywords": [topic],
                    "topic": topic,
                    "learning_outcome": context["learning_outcomes"][0],
                },
                ensure_ascii=False,
            )

        # Detect intent from system_prompt keywords to return relevant mocks
        sp_lower = system_prompt.lower()

        if "câu hỏi" in sp_lower or "question" in sp_lower or "hỏi" in sp_lower:
            return json.dumps(
                {
                    "question": "Bạn có thể giải thích khái niệm đã học trong môn này và cho ví dụ thực tế không?",
                    "question_type": "conceptual",
                    "difficulty": "medium",
                    "expected_keywords": ["khái niệm", "ví dụ", "ứng dụng"],
                },
                ensure_ascii=False,
            )

        if "follow" in sp_lower or "đào sâu" in sp_lower or "phụ" in sp_lower:
            return json.dumps(
                {
                    "follow_up_question": "Bạn có thể giải thích chi tiết hơn về ví dụ vừa đề cập không?",
                    "reason": "Student cần làm rõ thêm về ví dụ thực tế.",
                },
                ensure_ascii=False,
            )

        if "chấm điểm" in sp_lower or "scoring" in sp_lower or "đánh giá" in sp_lower or "evaluat" in sp_lower:
            return json.dumps(
                {
                    "score": 7.5,
                    "max_score": 10.0,
                    "feedback": "Câu trả lời đã nêu đúng khái niệm cơ bản, tuy nhiên cần bổ sung thêm ví dụ thực tế.",
                    "strengths": ["Nắm đúng khái niệm", "Trình bày rõ ràng"],
                    "weaknesses": ["Thiếu ví dụ minh hoạ", "Chưa liên hệ thực tế"],
                    "missing_content": ["Ví dụ ứng dụng thực tế", "Liên hệ với learning outcomes"],
                },
                ensure_ascii=False,
            )

        # Default fallback
        return json.dumps(
            {"response": "Mock AI response. Hãy cấu hình AI_PROVIDER thực tế."},
            ensure_ascii=False,
        )


# ─────────────────────────────────────────────
#  Gemini Client
# ─────────────────────────────────────────────


class GeminiAIClient(BaseAIClient):
    """
    Google Gemini API client (Interactions endpoint, auth-key compatible).
    Set AI_PROVIDER=gemini and GEMINI_API_KEY in .env to use.
    """

    BASE_URL = "https://generativelanguage.googleapis.com/v1beta/interactions"
    API_REVISION = "2026-05-20"

    def __init__(self) -> None:
        self.api_key = settings.gemini_api_key
        self.model = settings.gemini_model
        if not self.api_key:
            raise ValueError("GEMINI_API_KEY is not set in environment variables.")

    async def complete(self, system_prompt: str, user_message: str) -> str:
        import asyncio

        headers = {
            "x-goog-api-key": self.api_key,
            "Api-Revision": self.API_REVISION,
            "Content-Type": "application/json",
        }
        combined_input = f"{system_prompt}\n\n---\n\n{user_message}"
        payload = {"model": self.model, "input": combined_input}

        last_exc: Exception | None = None
        for attempt in range(5):
            try:
                async with httpx.AsyncClient(timeout=60.0) as client:
                    response = await client.post(self.BASE_URL, headers=headers, json=payload)
                    response.raise_for_status()
                    data = response.json()
                break
            except httpx.HTTPStatusError as exc:
                last_exc = exc
                if exc.response.status_code in (429, 500, 502, 503, 504) and attempt < 4:
                    await asyncio.sleep(3 * (2 ** attempt))
                    continue
                raise
        else:
            raise ValueError(f"Gemini API failed after retries: {last_exc}") from last_exc

        texts = []
        for step in data.get("steps", []):
            if step.get("type") == "model_output":
                for part in step.get("content", []):
                    if part.get("type") == "text":
                        texts.append(part.get("text", ""))
        raw = "".join(texts).strip()
        if raw.startswith("```"):
            raw = raw.split("\n", 1)[1] if "\n" in raw else raw[3:]
            if raw.endswith("```"):
                raw = raw[:-3]
            raw = raw.strip()
        return raw


# ─────────────────────────────────────────────
#  OpenAI Client
# ─────────────────────────────────────────────


class OpenAIClient(BaseAIClient):
    """
    OpenAI Chat Completions API client.
    Set AI_PROVIDER=openai and OPENAI_API_KEY in .env to use.
    """

    BASE_URL = "https://api.openai.com/v1/chat/completions"

    def __init__(self) -> None:
        self.api_key = settings.openai_api_key
        self.model = settings.openai_model
        if not self.api_key:
            raise ValueError("OPENAI_API_KEY is not set in environment variables.")

    async def complete(self, system_prompt: str, user_message: str) -> str:
        headers = {
            "Authorization": f"Bearer {self.api_key}",
            "Content-Type": "application/json",
        }
        payload = {
            "model": self.model,
            "messages": [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_message},
            ],
            "temperature": 0.7,
            "max_tokens": 2048,
            "response_format": {"type": "json_object"},
        }

        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.post(self.BASE_URL, headers=headers, json=payload)
            response.raise_for_status()
            data = response.json()

        try:
            return data["choices"][0]["message"]["content"]
        except (KeyError, IndexError) as exc:
            logger.error(f"[OpenAIClient] Unexpected response structure: {data}")
            raise ValueError("Failed to parse OpenAI response.") from exc


# ─────────────────────────────────────────────
#  Client Factory
# ─────────────────────────────────────────────


def get_ai_client() -> BaseAIClient:
    """
    Factory function. Returns the AI client based on AI_PROVIDER env var.
    Supported values: 'mock', 'gemini', 'openai'
    """
    provider = settings.ai_provider.lower()
    logger.info(f"[AIClient] Initialising provider: {provider}")

    if provider == "gemini":
        return GeminiAIClient()
    elif provider == "openai":
        return OpenAIClient()
    else:
        if provider != "mock":
            logger.warning(
                f"[AIClient] Unknown provider '{provider}'. Falling back to Mock."
            )
        return MockAIClient()
