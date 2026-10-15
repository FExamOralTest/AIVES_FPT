"""
AIVES AI Service - Internal authentication.

The AI Service is only meant to be called by the .NET backend (BR-14: the
frontend never talks to it directly). The backend authenticates with a shared
secret sent in the `X-AI-Service-Key` header.

This key is DISTINCT from GEMINI_API_KEY:
  - AI_SERVICE_API_KEY  : Backend <-> AI Service shared secret (this file).
  - GEMINI_API_KEY      : AI Service <-> Google Gemini credential (ai_client.py).

If AI_SERVICE_API_KEY is unset (empty), enforcement is disabled so local
development and the test suite work without extra setup. In any deployed
environment the key MUST be set.
"""

import logging
import secrets

from fastapi import Header, HTTPException, status

from app.core.config import get_settings

logger = logging.getLogger(__name__)


def verify_ai_service_key(
    x_ai_service_key: str | None = Header(
        default=None,
        alias="X-AI-Service-Key",
        description="Shared secret between the .NET backend and the AI service.",
    ),
) -> None:
    """FastAPI dependency enforcing the internal API key."""
    settings = get_settings()
    expected = (settings.ai_service_api_key or "").strip()

    if not expected:
        # Enforcement disabled (development / tests).
        return

    if not x_ai_service_key or not secrets.compare_digest(
        x_ai_service_key.strip(), expected
    ):
        logger.warning(
            "[auth] rejected request: missing or invalid X-AI-Service-Key"
        )
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Missing or invalid X-AI-Service-Key.",
            headers={"WWW-Authenticate": "X-AI-Service-Key"},
        )
