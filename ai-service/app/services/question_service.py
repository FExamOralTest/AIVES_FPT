"""Generate grounded viva questions from course content and learning outcomes."""

import json
import re
import unicodedata
from pathlib import Path

from loguru import logger
from pydantic import ValidationError

from app.core.ai_client import BaseAIClient, get_ai_client
from app.models.request import GenerateQuestionRequest
from app.models.response import GenerateQuestionResponse
from app.services.markdown_processor import MarkdownProcessor


_PROMPT_PATH = Path(__file__).parent.parent / "prompts" / "question_prompt.txt"
_QUESTION_PROMPT_TEMPLATE = _PROMPT_PATH.read_text(encoding="utf-8")
_QUESTION_TYPES = ("conceptual", "application", "analysis")
_MAX_ATTEMPTS = 3


class InvalidQuestionContext(ValueError):
    """The supplied documents cannot support another question."""


class QuestionGenerationError(ValueError):
    """The provider failed to produce a valid question within the retry budget."""


def _normalise(text: str) -> str:
    text = unicodedata.normalize("NFKC", text).casefold()
    return " ".join(re.sub(r"[^\w\s]", " ", text).split())


def _unique(items: list[str]) -> list[str]:
    seen: set[str] = set()
    result = []
    for item in items:
        item = item.strip()
        key = _normalise(item)
        if key and key not in seen:
            seen.add(key)
            result.append(item)
    return result


class QuestionService:
    """Stateless generation; the backend supplies the session's question history."""

    def __init__(self, ai_client: BaseAIClient | None = None) -> None:
        self.ai_client = ai_client or get_ai_client()
        self.processor = MarkdownProcessor()

    def _build_context(self, request: GenerateQuestionRequest) -> dict:
        result = self.processor.process(request.documents)
        if not result.is_valid or not result.course_content or not result.learning_outcomes:
            raise InvalidQuestionContext(
                "Invalid course documents: " + "; ".join(result.validation_errors)
            )

        course = result.course_content
        outcomes = _unique(result.learning_outcomes.learning_outcomes)
        if not outcomes:
            # Prose-only outcome documents remain usable without inventing outcomes.
            outcomes = [result.learning_outcomes.clean_body]
        offset = (request.question_index - 1) % len(outcomes)
        outcomes = outcomes[offset:] + outcomes[:offset]

        topics = _unique(
            [heading.text for heading in course.headings if heading.level > 1]
            + course.key_topics
        )
        if not topics:
            topics = _unique([heading.text for heading in course.headings]) or [course.title]
        asked = {_normalise(topic) for topic in request.already_asked_topics}
        available = [topic for topic in topics if _normalise(topic) not in asked]
        if not available:
            raise InvalidQuestionContext(
                "All available course topics have already been asked. "
                "Provide more distinct topics or reduce the number of questions."
            )

        return {
            "course_name": request.course_name,
            "course_content": course.clean_body,
            "available_topics": available,
            "learning_outcomes": outcomes,
            "question_index": request.question_index,
            "total_questions": request.total_questions,
            "question_type": request.question_type
            or _QUESTION_TYPES[(request.question_index - 1) % len(_QUESTION_TYPES)],
            "difficulty": request.difficulty,
            "already_asked_topics": request.already_asked_topics,
            "already_asked_questions": request.already_asked_questions,
        }

    def _parse_response(self, raw: str, context: dict) -> GenerateQuestionResponse:
        if not isinstance(raw, str):
            raise ValueError("Return a JSON object as text")
        raw = raw.strip()
        fenced = re.fullmatch(r"```(?:json)?\s*(.*?)\s*```", raw, re.DOTALL | re.IGNORECASE)
        if fenced:
            raw = fenced.group(1)
        try:
            data = json.loads(raw)
        except json.JSONDecodeError as exc:
            raise ValueError("Return valid JSON without surrounding commentary") from exc
        if not isinstance(data, dict):
            raise ValueError("Return one JSON object, not a list or scalar")
        try:
            response = GenerateQuestionResponse.model_validate(
                {**data, "success": True, "message": None}
            )
        except ValidationError as exc:
            errors = "; ".join(
                f"{'.'.join(map(str, error['loc']))}: {error['msg']}"
                for error in exc.errors(include_input=False, include_url=False)
            )
            raise ValueError("Invalid question fields: " + errors) from exc

        if not _normalise(response.question):
            raise ValueError("Provide a meaningful question, not only punctuation")
        if response.question_type != context["question_type"]:
            raise ValueError("Use the requested question_type")
        if response.difficulty != context["difficulty"]:
            raise ValueError("Use the requested difficulty")
        topic_map = {_normalise(topic): topic for topic in context["available_topics"]}
        topic = topic_map.get(_normalise(response.topic))
        if topic is None:
            raise ValueError("Copy topic from available_topics; do not repeat covered topics")
        if response.learning_outcome not in context["learning_outcomes"]:
            raise ValueError("Copy learning_outcome exactly from learning_outcomes")
        if _normalise(response.question) in {
            _normalise(question) for question in context["already_asked_questions"]
        }:
            raise ValueError("Generate a different question; this question has already been asked")

        response.topic = topic
        response.expected_keywords = _unique(response.expected_keywords)
        if not response.expected_keywords:
            raise ValueError("Provide meaningful expected_keywords")
        return response

    async def generate_question(
        self, request: GenerateQuestionRequest
    ) -> GenerateQuestionResponse:
        context = self._build_context(request)
        logger.info(
            "[QuestionService] Generating question {}/{} for session {}",
            request.question_index, request.total_questions, request.exam_session_id,
        )
        correction = None
        for attempt in range(_MAX_ATTEMPTS):
            payload = {"task": "generate_viva_question", "context": context}
            if correction:
                payload["validation_feedback"] = correction
            try:
                raw = await self.ai_client.complete(
                    _QUESTION_PROMPT_TEMPLATE, json.dumps(payload, ensure_ascii=False)
                )
            except ValueError as exc:
                raise QuestionGenerationError(
                    "The AI provider returned an unreadable response."
                ) from exc
            try:
                return self._parse_response(raw, context)
            except ValueError as exc:
                correction = str(exc)
                logger.warning(
                    "[QuestionService] Rejected response on attempt {}: {}",
                    attempt + 1, correction,
                )

        raise QuestionGenerationError(
            "AI could not generate a valid, non-repeating question after 3 attempts."
        )
