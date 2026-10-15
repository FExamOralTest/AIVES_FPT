"""
AIVES AI Service - Core Configuration
Loads and validates environment variables using pydantic-settings.

Startup validation:
  - When AI_PROVIDER=gemini, GEMINI_API_KEY and GEMINI_MODEL must be set.
  - When AI_PROVIDER=openai, OPENAI_API_KEY must be set.
  - When STT_PROVIDER=google / TTS_PROVIDER=google, the matching key must be set.
A misconfiguration fails fast with a clear message instead of silently
falling back to mock responses.
"""

from functools import lru_cache
from typing import Literal

from pydantic import Field, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


AIProvider = Literal["mock", "gemini", "openai"]
STTProvider = Literal["mock", "google", "gemini", "openai_whisper", "faster_whisper"]
TTSProvider = Literal["mock", "google", "openai_tts", "edge_tts"]


class Settings(BaseSettings):
    """
    Application settings loaded from environment variables / .env file.
    Supports multiple AI providers via AI_PROVIDER env var.
    """

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=False,
        extra="ignore",
    )

    # --- Service ---
    ai_service_port: int = Field(default=8000, alias="AI_SERVICE_PORT")
    ai_service_env: str = Field(default="development", alias="AI_SERVICE_ENV")
    ai_service_host: str = Field(default="0.0.0.0", alias="AI_SERVICE_HOST")
    ai_service_log_level: str = Field(default="info", alias="AI_SERVICE_LOG_LEVEL")

    # --- Internal auth between Backend (.NET) and AI Service ---
    # This is NOT the Gemini key. When empty, key enforcement is disabled (dev only).
    ai_service_api_key: str = Field(default="", alias="AI_SERVICE_API_KEY")

    # --- AI Provider (LLM) ---
    ai_provider: AIProvider = Field(default="mock", alias="AI_PROVIDER")
    question_generation_timeout_seconds: float = Field(
        default=240, gt=0, le=3600, alias="QUESTION_GENERATION_TIMEOUT_SECONDS"
    )

    # --- Google Gemini (Interactions API, auth-key compatible) ---
    gemini_api_key: str = Field(default="", alias="GEMINI_API_KEY")
    gemini_model: str = Field(default="gemini-3.8-flash", alias="GEMINI_MODEL")
    gemini_stt_model: str = Field(default="", alias="GEMINI_STT_MODEL")
    gemini_api_base_url: str = Field(
        default="https://generativelanguage.googleapis.com/v1beta",
        alias="GEMINI_API_BASE_URL",
    )
    gemini_api_revision: str = Field(
        default="2026-05-20", alias="GEMINI_API_REVISION"
    )

    # --- OpenAI ---
    openai_api_key: str = Field(default="", alias="OPENAI_API_KEY")
    openai_model: str = Field(default="gpt-4o-mini", alias="OPENAI_MODEL")

    # --- STT ---
    stt_provider: STTProvider = Field(default="mock", alias="STT_PROVIDER")
    google_stt_api_key: str = Field(default="", alias="GOOGLE_STT_API_KEY")
    faster_whisper_model: str = Field(default="base", alias="FASTER_WHISPER_MODEL")

    # --- TTS ---
    tts_provider: TTSProvider = Field(default="mock", alias="TTS_PROVIDER")
    google_tts_api_key: str = Field(default="", alias="GOOGLE_TTS_API_KEY")

    # --- Backend ---
    backend_url: str = Field(default="http://localhost:5000", alias="BACKEND_URL")

    @property
    def is_development(self) -> bool:
        return self.ai_service_env.lower() == "development"

    @property
    def is_production(self) -> bool:
        return self.ai_service_env.lower() == "production"

    @model_validator(mode="after")
    def validate_provider_credentials(self) -> "Settings":
        """Fail fast on missing credentials for the selected providers."""
        if self.ai_provider == "gemini":
            if not self.gemini_api_key.strip():
                raise ValueError(
                    "AI_PROVIDER=gemini requires GEMINI_API_KEY to be set. "
                    "Set it in ai-service/.env (see .env.example) or switch "
                    "AI_PROVIDER to 'mock' for local development without a key."
                )
            if not self.gemini_model.strip():
                raise ValueError(
                    "AI_PROVIDER=gemini requires GEMINI_MODEL to be set "
                    "(e.g. gemini-3.8-flash)."
                )
        elif self.ai_provider == "openai" and not self.openai_api_key.strip():
            raise ValueError(
                "AI_PROVIDER=openai requires OPENAI_API_KEY to be set."
            )

        if self.stt_provider == "google" and not self.google_stt_api_key.strip():
            raise ValueError(
                "STT_PROVIDER=google requires GOOGLE_STT_API_KEY to be set."
            )
        if self.stt_provider == "openai_whisper" and not self.openai_api_key.strip():
            raise ValueError(
                "STT_PROVIDER=openai_whisper requires OPENAI_API_KEY to be set."
            )
        if self.tts_provider == "google" and not self.google_tts_api_key.strip():
            raise ValueError(
                "TTS_PROVIDER=google requires GOOGLE_TTS_API_KEY to be set."
            )
        if self.tts_provider == "openai_tts" and not self.openai_api_key.strip():
            raise ValueError(
                "TTS_PROVIDER=openai_tts requires OPENAI_API_KEY to be set."
            )
        return self


@lru_cache()
def get_settings() -> Settings:
    """Returns cached settings instance."""
    return Settings()
