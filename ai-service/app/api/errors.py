"""
AIVES AI Service - Shared HTTP error mapping for API routers.

Maps service-layer exceptions to consistent HTTP status codes so every router
reports provider failures the same way:

  InvalidQuestionContext -> 422 (bad/unusable context)
  QuestionGenerationError -> 502 (provider returned unusable output)
  httpx.HTTPError         -> 502 (provider/network failure)
  ValueError              -> 500 (service configuration error)
  anything else           -> 500 (unexpected)
"""

import httpx
from fastapi import HTTPException, status

from app.services.question_service import (
    InvalidQuestionContext,
    QuestionGenerationError,
)


def to_http_exception(exc: Exception, *, context: str) -> HTTPException:
    """Translate a service-layer exception into an HTTPException."""
    if isinstance(exc, HTTPException):
        return exc
    if isinstance(exc, InvalidQuestionContext):
        return HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY, detail=str(exc)
        )
    if isinstance(exc, QuestionGenerationError):
        return HTTPException(status_code=status.HTTP_502_BAD_GATEWAY, detail=str(exc))
    if isinstance(exc, httpx.HTTPError):
        return HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail="The AI provider is unavailable. Please retry later.",
        )
    if isinstance(exc, ValueError):
        return HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(exc)
        )
    return HTTPException(
        status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
        detail=f"{context} failed unexpectedly.",
    )
