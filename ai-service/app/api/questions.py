import asyncio

import httpx
from fastapi import APIRouter, Depends, HTTPException, Request
from loguru import logger

from app.api.deps import get_question_bank_service
from app.core.config import get_settings
from app.models.question_bank import GenerateQuestionsRequest, GenerateQuestionsResponse
from app.services.question_bank_service import QuestionBankService
from app.services.question_service import QuestionGenerationError


router = APIRouter(prefix="/questions", tags=["Question Bank"])


async def _wait_for_disconnect(request: Request) -> None:
    while True:
        message = await request.receive()
        if message["type"] == "http.disconnect":
            return


@router.post(
    "/generate",
    response_model=GenerateQuestionsResponse,
    responses={
        401: {"description": "Missing or invalid X-AI-Service-Key"},
        422: {"description": "Invalid documents or generation parameters"},
        502: {"description": "Provider unavailable or invalid response"},
        504: {"description": "Question generation timed out"},
    },
)
async def generate_questions(
    payload: GenerateQuestionsRequest,
    request: Request,
    service: QuestionBankService = Depends(get_question_bank_service),
) -> GenerateQuestionsResponse:
    logger.info("Question bank generation requested: count={}", payload.question_count)
    generation = asyncio.create_task(service.generate(payload))
    disconnected = asyncio.create_task(_wait_for_disconnect(request))
    try:
        async with asyncio.timeout(get_settings().question_generation_timeout_seconds):
            done, _ = await asyncio.wait({generation, disconnected}, return_when=asyncio.FIRST_COMPLETED)
            if disconnected in done:
                logger.info("Question bank generation cancelled: backend disconnected")
                raise HTTPException(status_code=499, detail="Question generation cancelled.")
            return await generation
    except (TimeoutError, httpx.TimeoutException) as exc:
        logger.warning("Question bank generation timed out")
        raise HTTPException(status_code=504, detail="AI question generation timed out.") from exc
    except QuestionGenerationError as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
    except httpx.HTTPError as exc:
        logger.warning("Question bank provider request failed: {}", type(exc).__name__)
        raise HTTPException(status_code=502, detail="The AI provider is unavailable. Please retry later.") from exc
    except HTTPException:
        raise
    except Exception as exc:
        logger.error("Question bank generation failed: {}", type(exc).__name__)
        raise HTTPException(status_code=502, detail="AI question generation failed.") from exc
    finally:
        generation.cancel()
        disconnected.cancel()
        await asyncio.gather(generation, disconnected, return_exceptions=True)
