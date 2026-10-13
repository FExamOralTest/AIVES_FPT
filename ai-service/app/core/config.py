"""
AIVES AI Service - Core Configuration
Loads and validates environment variables using pydantic-settings.
"""

from pydantic_settings import BaseSettings, SettingsConfigDict
from pydantic import Field
from functools import lru_cache


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
    ai_service_secret_key: str = Field(
        default="dev-secret-key", alias="AI_SERVICE_SECRET_KEY"
    )

    # --- AI Provider ---
    ai_provider: str = Field(default="mock", alias="AI_PROVIDER")
    gemini_api_key: str = Field(default="", alias="GEMINI_API_KEY")
    gemini_model: str = Field(default="gemini-3.1-flash-lite", alias="GEMINI_MODEL")
    openai_api_key: str = Field(default="", alias="OPENAI_API_KEY")
    openai_model: str = Field(default="gpt-4o-mini", alias="OPENAI_MODEL")

    # --- STT ---
    stt_provider: str = Field(default="mock", alias="STT_PROVIDER")
    google_stt_api_key: str = Field(default="", alias="GOOGLE_STT_API_KEY")
    faster_whisper_model: str = Field(default="base", alias="FASTER_WHISPER_MODEL")

    # --- TTS ---
    tts_provider: str = Field(default="mock", alias="TTS_PROVIDER")
    google_tts_api_key: str = Field(default="", alias="GOOGLE_TTS_API_KEY")

    # --- Backend ---
    backend_url: str = Field(default="http://localhost:5000", alias="BACKEND_URL")

    @property
    def is_development(self) -> bool:
        return self.ai_service_env.lower() == "development"

    @property
    def is_production(self) -> bool:
        return self.ai_service_env.lower() == "production"


@lru_cache()
def get_settings() -> Settings:
    """Returns cached settings instance."""
    return Settings()
