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
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

from app.core.config import get_settings
from app.api import interview, speech, evaluation, markdown, questions

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
    api_prefix = "/ai"
    app.include_router(interview.router, prefix=api_prefix)
    app.include_router(speech.router, prefix=api_prefix)
    app.include_router(evaluation.router, prefix=api_prefix)
    app.include_router(markdown.router, prefix=api_prefix)
    app.include_router(questions.router, prefix=api_prefix)
    
    # Aliases for backwards compatibility
    app.post("/ai/generate-questions", tags=["Interview"], include_in_schema=False)(interview.batch_generate_questions)
    app.post("/api/ai/generate-questions", tags=["Interview"], include_in_schema=False)(interview.batch_generate_questions)

    # ── Root ───────────────────────────────────────────────
    @app.get("/", include_in_schema=False)
    async def root():
        return JSONResponse(
            {
                "service": "AIVES AI Service",
                "version": "1.0.0",
                "docs": "/docs",
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
