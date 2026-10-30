import asyncio

import httpx

from app.core import ai_client


def test_gemini_api_key_is_sent_in_header_not_url(monkeypatch):
    monkeypatch.setattr(ai_client.settings, "gemini_api_key", "test-only-key")
    monkeypatch.setattr(ai_client.settings, "gemini_model", "gemini-test")
    client_type = httpx.AsyncClient

    def handler(request):
        assert request.headers["x-goog-api-key"] == "test-only-key"
        assert "key=" not in str(request.url)
        return httpx.Response(
            200,
            json={"candidates": [{"content": {"parts": [{"text": '{"ok": true}'}]}}]},
        )

    monkeypatch.setattr(
        httpx,
        "AsyncClient",
        lambda **kwargs: client_type(
            transport=httpx.MockTransport(handler),
            **kwargs,
        ),
    )

    result = asyncio.run(ai_client.GeminiAIClient().complete("system", "user"))

    assert result == '{"ok": true}'
