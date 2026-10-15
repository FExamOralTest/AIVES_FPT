import os

# Force mock env vars for testing unless explicitly overridden
os.environ.setdefault("AI_PROVIDER", "mock")
os.environ.setdefault("STT_PROVIDER", "mock")
os.environ.setdefault("TTS_PROVIDER", "mock")
os.environ.setdefault("AI_SERVICE_API_KEY", "")
os.environ.setdefault("GEMINI_API_KEY", "")
os.environ.setdefault("GEMINI_API_BASE_URL", "https://generativelanguage.googleapis.com/v1beta")
os.environ.setdefault("GEMINI_API_REVISION", "2026-05-20")
os.environ.setdefault("AI_SERVICE_BASE_URL", "http://localhost:8000")

# Mock providers for testing
os.environ.setdefault("MOCK_MODE", "true")