"""
AIVES AI Service - Markdown Processing API Router
Endpoint for reading, validating and normalising .md course documents
into a structured AI context string.

Called by ASP.NET Core Backend (not directly by Frontend - see BR-14).
"""

from fastapi import APIRouter, HTTPException, status
from loguru import logger

from app.models.request import ProcessMarkdownRequest
from app.models.response import (
    ProcessMarkdownResponse,
    DocumentProcessingSummary,
    ErrorResponse,
)
from app.services.markdown_processor import MarkdownProcessor

router = APIRouter(prefix="/markdown", tags=["Markdown Processing"])
_processor = MarkdownProcessor()


@router.post(
    "/process",
    response_model=ProcessMarkdownResponse,
    summary="Process & Normalise Markdown Documents",
    description=(
        "Reads, validates, cleans and normalises the two course .md documents "
        "(course_content + learning_outcomes) and returns: \n\n"
        "- A ready-to-use `context_string` for AI prompt templates.\n"
        "- Per-document metadata: extracted headings, learning outcomes, key topics.\n"
        "- Validation status and any errors/warnings.\n\n"
        "**Use case**: Called before starting an exam session to pre-validate documents "
        "and optionally cache the context string. Also useful for debugging prompt quality."
    ),
    responses={
        200: {"description": "Documents processed (may contain validation warnings)"},
        422: {"description": "Validation error in request body"},
        500: {"model": ErrorResponse, "description": "Processing failed unexpectedly"},
    },
)
async def process_markdown(
    request: ProcessMarkdownRequest,
) -> ProcessMarkdownResponse:
    """
    Process 2 .md documents and return structured AI context.

    Flow: Backend → POST /ai/markdown/process → MarkdownProcessor → context_string
    """
    try:
        logger.info(
            f"POST /markdown/process | course='{request.course_name}' "
            f"docs={[d.document_type for d in request.documents]}"
        )

        result = _processor.process(request.documents)

        # Build per-document summaries for the response
        summaries: list[DocumentProcessingSummary] = []

        def _struct_to_summary(struct) -> DocumentProcessingSummary:
            return DocumentProcessingSummary(
                document_id=struct.document_id,
                title=struct.title,
                document_type=struct.document_type,
                raw_char_count=struct.raw_char_count,
                clean_char_count=struct.clean_char_count,
                heading_count=struct.heading_count,
                headings=[h.text for h in struct.headings],
                learning_outcomes=struct.learning_outcomes,
                key_topics=struct.key_topics[:20],  # cap to 20 for response size
                is_valid=struct.is_valid,
                validation_errors=struct.validation_errors,
            )

        if result.course_content:
            summaries.append(_struct_to_summary(result.course_content))
        if result.learning_outcomes:
            summaries.append(_struct_to_summary(result.learning_outcomes))

        response = ProcessMarkdownResponse(
            success=result.is_valid,
            message=(
                None
                if result.is_valid
                else "Document validation failed. See validation_errors for details."
            ),
            course_name=request.course_name,
            is_valid=result.is_valid,
            validation_errors=result.validation_errors,
            documents=summaries,
            context_string=result.context_string,
        )

        logger.info(
            f"[MarkdownAPI] Processed '{request.course_name}': "
            f"valid={result.is_valid}, context_len={len(result.context_string)}"
        )
        return response

    except Exception as exc:
        logger.exception(f"[MarkdownAPI] Unexpected error: {exc}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="An unexpected error occurred while processing the markdown documents.",
        )
