import asyncio
import base64
import json

import httpx
import pytest

from app.services import speech_service


@pytest.fixture
def gemini_service(monkeypatch):
    monkeypatch.setattr(speech_service.settings, "gemini_api_key", "test-only-key")
    monkeypatch.setattr(speech_service.settings, "gemini_stt_model", "")
    monkeypatch.setattr(speech_service.settings, "gemini_model", "gemini-3.1-flash-lite")
    service = speech_service.SpeechToTextService()
    service.provider = "gemini"
    return service


def install_transport(monkeypatch, handler):
    client_type = httpx.AsyncClient
    monkeypatch.setattr(httpx, "AsyncClient", lambda **kwargs: client_type(
        transport=httpx.MockTransport(handler), **kwargs
    ))


@pytest.mark.parametrize("audio,mime", [
    (b"RIFF1234WAVEdata", "audio/wav"),
    (b"\x1aE\xdf\xa3webm", "audio/webm"),
    (b"OggSdata", "audio/ogg"),
    (b"ID3data", "audio/mpeg"),
    (b"\xff\xfbdata", "audio/mpeg"),
    (b"1234ftypM4A ", "audio/mp4"),
])
def test_transcription_preserves_audio_and_provider_text(monkeypatch, gemini_service, audio, mime):
    def handler(request):
        assert request.headers["x-goog-api-key"] == "test-only-key"
        assert "key=" not in str(request.url)
        payload = json.loads(request.content)
        inline = payload["contents"][0]["parts"][1]["inlineData"]
        assert inline["mimeType"] == mime
        assert base64.b64decode(inline["data"]) == audio
        return httpx.Response(200, json={"candidates": [{"content": {"parts": [
            {"text": "Xin chào."}, {"text": "hidden reasoning", "thought": True}
        ]}}]})

    install_transport(monkeypatch, handler)
    result = asyncio.run(gemini_service.transcribe(audio, "verification", "vi-VN"))
    assert result.transcript == "Xin chào."
    assert result.confidence is None
    assert result.success


@pytest.mark.parametrize("audio", [b"", b"not an audio file", b"x" * 14_000_001], ids=["empty", "unsupported", "oversized"])
def test_invalid_audio_fails_before_provider_call(gemini_service, audio):
    with pytest.raises(ValueError):
        asyncio.run(gemini_service.transcribe(audio, "verification"))


def test_blocked_or_empty_response_does_not_become_a_fake_transcript(monkeypatch, gemini_service):
    install_transport(monkeypatch, lambda request: httpx.Response(200, json={"promptFeedback": {"blockReason": "SAFETY"}}))
    with pytest.raises(ValueError, match="no transcript"):
        asyncio.run(gemini_service.transcribe(b"RIFF1234WAVEdata", "verification"))


def test_authentication_failure_is_propagated(monkeypatch, gemini_service):
    install_transport(monkeypatch, lambda request: httpx.Response(403, json={"error": {"message": "Denied"}}))
    with pytest.raises(httpx.HTTPStatusError):
        asyncio.run(gemini_service.transcribe(b"RIFF1234WAVEdata", "verification"))


def test_transient_provider_failure_is_retried(monkeypatch, gemini_service):
    attempts = []

    async def no_wait(seconds):
        pass

    def handler(request):
        attempts.append(request)
        if len(attempts) == 1:
            return httpx.Response(503, json={"error": {"message": "Temporarily overloaded"}})
        return httpx.Response(200, json={"candidates": [{"content": {"parts": [{"text": "Recovered transcript"}]}}]})

    monkeypatch.setattr(speech_service.asyncio, "sleep", no_wait)
    install_transport(monkeypatch, handler)
    result = asyncio.run(gemini_service.transcribe(b"RIFF1234WAVEdata", "verification"))
    assert len(attempts) == 2
    assert result.transcript == "Recovered transcript"
