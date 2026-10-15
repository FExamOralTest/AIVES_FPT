"""
AIVES AI Service - Main Application Entry Point
FastAPI application with all routers, middleware and lifecycle events.

Architecture (see BR-14):
  Frontend → Backend (ASP.NET Core) → AI Service (FastAPI)
  Frontend NEVER calls AI Service directly.

API prefix: /ai
"""

from contextlib import asynccontextmanager
from loguru import logger
from fastapi import Depends, FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

from app.api import interview, speech, evaluation, markdown, questions
from app.core.config import get_settings
from app.core.security import verify_ai_service_key
from app.models.question_bank import GenerateQuestionsResponse
from app.models.response import HealthResponse

settings = get_settings()


# ─────────────────────────────────────────────
#  Lifespan (startup / shutdown)
# ─────────────────────────────────────────────


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan events."""
    logger.info("=" * 60)
    logger.info("🚀 AIVES AI Service starting up")
    logger.info(f"   Environment : {settings.ai_service_env}")
    logger.info(f"   AI Provider : {settings.ai_provider}")
    logger.info(f"   STT Provider: {settings.stt_provider}")
    logger.info(f"   TTS Provider: {settings.tts_provider}")
    logger.info(f"   Backend URL : {settings.backend_url}")
    logger.info(
        "   API guard   : {}",
        "X-AI-Service-Key enforced"
        if settings.ai_service_api_key.strip()
        else "DISABLED (AI_SERVICE_API_KEY not set - development only)",
    )
    logger.info("=" * 60)
    yield
    logger.info("🛑 AIVES AI Service shutting down")


# ─────────────────────────────────────────────
#  Application Factory
# ─────────────────────────────────────────────


def create_app() -> FastAPI:
    """Create and configure the FastAPI application."""

    app = FastAPI(
        title="AIVES AI Service",
        description=(
            "AI microservice for AIVES (AI-powered Viva Exam System). "
            "Handles AI question generation, speech processing, and answer evaluation. "
            "\n\n"
            "**Architecture Note**: Per BR-14, the Frontend never calls this service directly. "
            "All requests are proxied through the ASP.NET Core Backend."
        ),
        version="1.0.0",
        docs_url="/docs",
        redoc_url="/redoc",
        openapi_url="/openapi.json",
        lifespan=lifespan,
    )

    # ── CORS ──────────────────────────────────────────────
    # Only allows Backend to call AI Service (see BR-14)
    allowed_origins = (
        ["*"]
        if settings.is_development
        else [settings.backend_url]
    )
    app.add_middleware(
        CORSMiddleware,
        allow_origins=allowed_origins,
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    # ── Routers ────────────────────────────────────────────
    # All business endpoints require the internal backend key (see security.py).
    api_prefix = "/ai"
    auth = [Depends(verify_ai_service_key)]
    app.include_router(interview.router, prefix=api_prefix, dependencies=auth)
    app.include_router(speech.router, prefix=api_prefix, dependencies=auth)
    app.include_router(evaluation.router, prefix=api_prefix, dependencies=auth)
    app.include_router(markdown.router, prefix=api_prefix, dependencies=auth)
    app.include_router(questions.router, prefix=api_prefix, dependencies=auth)

    # Deprecated aliases kept for backwards compatibility. They delegate to
    # QuestionBankService and will be removed once no client uses them.
    for alias in ("/ai/generate-questions", "/api/ai/generate-questions"):
        app.add_api_route(
            alias,
            interview.generate_questions_legacy,
            methods=["POST"],
            response_model=GenerateQuestionsResponse,
            tags=["Interview (deprecated)"],
            deprecated=True,
            dependencies=auth,
        )

    # ── Health & Root ──────────────────────────────────────
    @app.get(
        "/health",
        response_model=HealthResponse,
        tags=["Health"],
        summary="Health Check",
        description="Returns service status and configured AI providers.",
    )
    async def health_check() -> HealthResponse:
        return HealthResponse(
            status="ok",
            service="aives-ai",
            version="1.0.0",
            ai_provider=settings.ai_provider,
            stt_provider=settings.stt_provider,
            tts_provider=settings.tts_provider,
        )

    @app.get("/", include_in_schema=False)
    async def root():
        return JSONResponse(
            {
                "service": "AIVES AI Service",
                "version": "1.0.0",
                "docs": "/docs",
                "health": "/health",
            }
        )

    return app


# ─────────────────────────────────────────────
#  Application Instance
# ─────────────────────────────────────────────

app = create_app()


# ─────────────────────────────────────────────
#  Run directly (development only)
# ─────────────────────────────────────────────

if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "app.main:app",
        host=settings.ai_service_host,
        port=settings.ai_service_port,
        reload=settings.is_development,
        log_level=settings.ai_service_log_level,
    )
