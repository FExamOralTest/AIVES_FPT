"""
AIVES AI Service - Shared document-context builder.

The scoring, follow-up and feedback-analysis services all need to turn the two
request documents (course content + learning outcomes) into the context string
injected into their prompt templates. This module centralises that logic so the
behaviour (and the raw-content fallback) is identical everywhere.
"""

from typing import List, Sequence

from loguru import logger

from app.models.request import DocumentContext
from app.services.markdown_processor import MarkdownProcessor

# MarkdownProcessor is stateless, so a single shared instance is safe.
_processor = MarkdownProcessor()


def build_document_context(
    documents: Sequence[DocumentContext], *, source: str = "Service"
) -> str:
    """
    Normalise the request documents into a prompt-ready context string.

    Uses MarkdownProcessor first; if validation fails, falls back to joining
    the raw document title + content so evaluation can still proceed.
    """
    result = _processor.process(list(documents))
    if result.is_valid and result.context_string:
        return result.context_string

    logger.warning(
        "[{}] MarkdownProcessor validation failed; errors={}. "
        "Using raw content fallback.",
        source,
        result.validation_errors,
    )
    parts: List[str] = [
        f"### {doc.document_type.replace('_', ' ').title()} - {doc.title}\n\n{doc.content}"
        for doc in documents
    ]
    return "\n\n---\n\n".join(parts)
