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
    ai_max_retries: int = Field(default=3, ge=0, le=10, alias="AI_MAX_RETRIES")
    ai_timeout_seconds: float = Field(default=60.0, gt=0, le=300, alias="AI_TIMEOUT_SECONDS")
    ai_retry_delay_seconds: float = Field(default=2.0, gt=0, le=30, alias="AI_RETRY_DELAY_SECONDS")
    question_generation_timeout_seconds: float = Field(
        default=240, gt=0, le=3600, alias="QUESTION_GENERATION_TIMEOUT_SECONDS"
    )
    gemini_api_key: str = Field(default="", alias="GEMINI_API_KEY")
    gemini_model: str = Field(default="gemini-3.1-flash-lite", alias="GEMINI_MODEL")
    gemini_stt_model: str = Field(default="", alias="GEMINI_STT_MODEL")
    openai_api_key: str = Field(default="", alias="OPENAI_API_KEY")
    openai_model: str = Field(default="gpt-4o-mini", alias="OPENAI_MODEL")

    # --- STT ---
    stt_provider: str = Field(default="mock", alias="STT_PROVIDER")
    google_stt_api_key: str = Field(default="", alias="GOOGLE_STT_API_KEY")
    faster_whisper_model: str = Field(default="base", alias="FASTER_WHISPER_MODEL")
    faster_whisper_device: str = Field(default="cpu", alias="FASTER_WHISPER_DEVICE")
    faster_whisper_compute_type: str = Field(default="int8", alias="FASTER_WHISPER_COMPUTE_TYPE")
    stt_max_audio_bytes: int = Field(default=50 * 1024 * 1024, gt=0, alias="STT_MAX_AUDIO_BYTES")
    stt_timeout_seconds: float = Field(default=110, gt=0, le=3600, alias="STT_TIMEOUT_SECONDS")

    # --- R2 ---
    r2_account_id: str = Field(default="", alias="R2_ACCOUNT_ID")
    r2_endpoint_url: str = Field(default="", alias="R2_ENDPOINT_URL")
    r2_access_key_id: str = Field(default="", alias="R2_ACCESS_KEY_ID")
    r2_secret_access_key: str = Field(default="", alias="R2_SECRET_ACCESS_KEY")
    r2_bucket_name: str = Field(default="aives-exam-audios", alias="R2_BUCKET_NAME")

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
