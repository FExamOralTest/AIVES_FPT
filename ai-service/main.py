"""
AIVES AI Service - Root Entry Point
Run with: uvicorn main:app --reload
or: python main.py
"""

from app.main import app  # noqa: F401 - re-export for uvicorn

if __name__ == "__main__":
    import uvicorn
    from app.core.config import get_settings

    settings = get_settings()
    uvicorn.run(
        "main:app",
        host=settings.ai_service_host,
        port=settings.ai_service_port,
        reload=settings.is_development,
        log_level=settings.ai_service_log_level,
    )
