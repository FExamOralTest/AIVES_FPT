"""
AIVES AI Service - AI Client Abstraction
Supports multiple LLM providers: mock (deterministic, for tests/dev), gemini, openai.

Provider selection is driven by settings.ai_provider. When gemini/openai is
selected the matching client is constructed eagerly; a missing/invalid
configuration raises at startup (see app.core.config) instead of falling back
to mock answers.
"""

import asyncio
import json
import logging
import re
from abc import ABC, abstractmethod
from typing import Any, List, Optional

import httpx

from app.core.config import get_settings

logger = logging.getLogger(__name__)
settings = get_settings()

# Number of attempts (1 initial + retries) for transient provider errors.
_MAX_ATTEMPTS = 4
_RETRYABLE_STATUS = {408, 409, 429, 500, 502, 503, 504}
_MAX_RETRY_DELAY_SECONDS = 30.0


def strip_code_fence(text: str) -> str:
    """
    Remove a single surrounding ```json ... ``` (or plain ``` ... ```) fence
    from a model response and return the inner content.

    If the response is not fully wrapped in a fence, returns it unchanged.
    """
    if not text:
        return text
    stripped = text.strip()
    fenced = re.fullmatch(r"```(?:json)?\s*(.*?)\s*```", stripped, re.DOTALL)
    if fenced:
        return fenced.group(1).strip()
    # Fallback: response contains prose before/after a fenced block.
    inner = re.search(r"```(?:json)?\s*(.*?)\s*```", stripped, re.DOTALL)
    if inner:
        return inner.group(1).strip()
    return stripped


def _collect_texts(node: Any) -> List[str]:
    """Recursively collect text fragments from a nested response structure."""
    out: List[str] = []
    if isinstance(node, str):
        out.append(node)
    elif isinstance(node, dict):
        text = node.get("text")
        if isinstance(text, str):
            out.append(text)
        for key in ("parts", "content", "outputs", "output", "steps", "data"):
            if key in node:
                out.extend(_collect_texts(node[key]))
    elif isinstance(node, list):
        for item in node:
            out.extend(_collect_texts(item))
    return out


class BaseAIClient(ABC):
    """Abstract base class for AI clients."""

    @abstractmethod
    async def complete(self, system_prompt: str, user_message: str) -> str:
        """
        Send a system + user prompt to the model and return its raw text response.
        """
        pass


class MockAIClient(BaseAIClient):
    """
    Deterministic mock used for local development and tests.

    It produces valid, parseable responses so the full request pipeline can be
    exercised without a network call or an API key.
    """

    async def complete(self, system_prompt: str, user_message: str) -> str:
        logger.info("[MockAIClient] complete() called")
        lowered = (system_prompt + " " + user_message).lower()

        if "json" in lowered:
            payload = {
                "questions": [
                    {
                        "question": "Mock generated question 1?",
                        "level": "easy",
                        "max_score": 5,
                        "is_follow_up_allowed": False,
                    },
                    {
                        "question": "Mock generated question 2?",
                        "level": "medium",
                        "max_score": 10,
                        "is_follow_up_allowed": True,
                    },
                ]
            }
            return json.dumps(payload, ensure_ascii=False)

        return "Mock AI response."


class GeminiAIClient(BaseAIClient):
    """
    Google Gemini client using the Interactions API.

    Endpoint shape:  {GEMINI_API_BASE_URL}/interactions
    Auth:            x-goog-api-key header (Google AI Studio key)
    Revision:        Api-Revision header (see GEMINI_API_REVISION)

    The response parser is intentionally tolerant: it prefers the
    convenience `output_text` field, then walks `steps`/`outputs`/`output`
    for text parts, and finally falls back to `candidates` (generateContent
    shape). This keeps the client working across Interactions API revisions.
    """

    def __init__(self) -> None:
        self.api_key = settings.gemini_api_key
        self.model = settings.gemini_model
        self.base_url = settings.gemini_api_base_url.rstrip("/")
        self.api_revision = settings.gemini_api_revision
        if not self.api_key:
            raise ValueError(
                "GEMINI_API_KEY is not set. Configure it in ai-service/.env or "
                "set AI_PROVIDER=mock."
            )

    @property
    def _endpoint(self) -> str:
        return f"{self.base_url}/interactions"

    async def complete(self, system_prompt: str, user_message: str) -> str:
        headers = {
            "x-goog-api-key": self.api_key,
            "Content-Type": "application/json",
        }
        if self.api_revision:
            headers["Api-Revision"] = self.api_revision

        combined_input = f"{system_prompt}\n\n---\n\n{user_message}"
        payload = {"model": self.model, "input": combined_input}

        data = await self._post_with_retry(headers, payload)
        return self._extract_text(data)

    async def _post_with_retry(
        self, headers: dict, payload: dict
    ) -> dict:
        """POST to the Interactions endpoint, retrying transient failures."""
        last_exc: Optional[Exception] = None
        for attempt in range(_MAX_ATTEMPTS):
            try:
                async with httpx.AsyncClient(timeout=120.0) as client:
                    response = await client.post(
                        self._endpoint, headers=headers, json=payload
                    )
                    response.raise_for_status()
                    return response.json()
            except httpx.HTTPStatusError as exc:
                last_exc = exc
                status = exc.response.status_code
                # Che body trước khi log - xoá/chuyển key nhạy cảm
                raw_body = exc.response.text
                cheated_body = self._cheat_sensitive_data(raw_body)
                retryable = status in _RETRYABLE_STATUS
                if retryable and attempt < _MAX_ATTEMPTS - 1:
                    delay = self._retry_delay(exc.response, attempt)
                    logger.warning(
                        "[GeminiAIClient] HTTP %s from %s; retry %d/%d in %.1fs",
                        status,
                        self._endpoint,
                        attempt + 1,
                        _MAX_ATTEMPTS - 1,
                        delay,
                    )
                    logger.info(
                        "[GeminiAIClient] response body (cheated): %s", cheated_body
                    )
                    await asyncio.sleep(delay)
                    continue
                # Lỗi không thể retry: log body che, raise ValueError an toàn
                logger.error(
                    "[GeminiAIClient] HTTP %s from %s; body (cheated): %s",
                    status,
                    self._endpoint,
                    cheated_body,
                )
                raise ValueError(
                    f"Gemini provider returned HTTP {status}. The AI service is temporarily unavailable. Please retry later."
                ) from exc
            except httpx.HTTPError as exc:
                last_exc = exc
                if attempt < _MAX_ATTEMPTS - 1:
                    delay = min(2.0 ** attempt, _MAX_RETRY_DELAY_SECONDS)
                    logger.warning(
                        "[GeminiAIClient] network error (%s); retry %d/%d in %.1fs",
                        exc,
                        attempt + 1,
                        _MAX_ATTEMPTS - 1,
                        delay,
                    )
                    await asyncio.sleep(delay)
                    continue
                logger.error(
                    "[GeminiAIClient] network error from %s; %s", self._endpoint, exc
                )
                raise ValueError(
                    "The AI service is temporarily unavailable. Please retry later."
                ) from exc

        # Defensive: loop always returns or raises.
        raise ValueError(
            "The AI provider failed after maximum retries. Please try again later."
        ) from last_exc

    @staticmethod
    def _cheat_sensitive_data(body: str) -> str:
        """Mask potential API keys or secrets in the provider response body."""
        import re
        # Remove anything that looks like an API key (AQ. followed by caps/alphanum)
        cheated = re.sub(r'AQ\.[A-Za-z0-9_\-]+', 'AQ.<masked>', body)
        # Replace common secret patterns
        cheated = cheated.replace('"api_key"', '"api_key": "<redacted>"')
        cheated = cheated.replace('"key"', '"key": "<redacted>"')
        # Truncate if very long
        if len(cheated) > 500:
            cheated = cheated[:500] + "...[truncated]"
        return cheated

    @staticmethod
    def _retry_delay(response: httpx.Response, attempt: int) -> float:
        """Honour Retry-After when present, otherwise use exponential backoff."""
        retry_after = response.headers.get("Retry-After")
        if retry_after:
            retry_after = retry_after.strip()
            try:
                return min(float(retry_after), _MAX_RETRY_DELAY_SECONDS)
            except ValueError:
                # HTTP-date form is uncommon here; fall through to backoff.
                pass
        return min(2.0 ** attempt, _MAX_RETRY_DELAY_SECONDS)

    def _extract_text(self, data: Any) -> str:
        if not isinstance(data, dict):
            raise ValueError(
                f"Unexpected Gemini response type: {type(data).__name__}"
            )

        raw = data.get("output_text")
        if isinstance(raw, str) and raw.strip():
            return strip_code_fence(raw)

        texts: List[str] = []
        for key in ("steps", "outputs", "output", "candidates", "messages"):
            if key in data:
                texts.extend(_collect_texts(data[key]))

        combined = "".join(texts).strip()
        if not combined:
            logger.error(
                "[GeminiAIClient] unreadable response. Top-level keys: %s",
                sorted(data.keys()),
            )
            raise ValueError(
                "Gemini response did not contain any text output. "
                "The Interactions API response schema may have changed."
            )
        return strip_code_fence(combined)


class OpenAIClient(BaseAIClient):
    """OpenAI client using the Chat Completions API."""

    def __init__(self) -> None:
        self.api_key = settings.openai_api_key
        self.model = settings.openai_model
        if not self.api_key:
            raise ValueError(
                "OPENAI_API_KEY is not set. Configure it in ai-service/.env or "
                "set AI_PROVIDER=mock."
            )

    async def complete(self, system_prompt: str, user_message: str) -> str:
        url = "https://api.openai.com/v1/chat/completions"
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
        }
        async with httpx.AsyncClient(timeout=120.0) as client:
            response = await client.post(url, headers=headers, json=payload)
            response.raise_for_status()
            data = response.json()
        return data["choices"][0]["message"]["content"]


def get_ai_client() -> BaseAIClient:
    """
    Factory: returns the AI client for the configured provider.

    Raises ValueError for an unknown provider or missing credentials; it never
    silently substitutes the mock client.
    """
    provider = settings.ai_provider
    if provider == "gemini":
        logger.info("[AI] Using GeminiAIClient (model=%s)", settings.gemini_model)
        return GeminiAIClient()
    if provider == "openai":
        logger.info("[AI] Using OpenAIClient (model=%s)", settings.openai_model)
        return OpenAIClient()
    if provider == "mock":
        logger.info("[AI] Using MockAIClient")
        return MockAIClient()
    raise ValueError(f"Unsupported AI_PROVIDER: {provider!r}")
