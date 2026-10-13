"""Analyze a transcript against an approved rubric. Numeric scoring stays separate."""

import json
from pathlib import Path

from loguru import logger

from app.core.ai_client import BaseAIClient, get_ai_client
from app.models.ai_output import parse_ai_output
from app.models.feedback import FeedbackAnalysisOutput
from app.models.request import EvaluateAnswerRequest
from app.services.answer_evidence import blank_analysis, split_sentences, uses_vietnamese, validate_analysis
from app.services.markdown_processor import MarkdownProcessor

_PROMPT = (Path(__file__).parent.parent / "prompts" / "feedback_analysis_prompt.txt").read_text(encoding="utf-8")
_markdown = MarkdownProcessor()


def _document_context(request: EvaluateAnswerRequest) -> str:
    result = _markdown.process(request.documents)
    if result.is_valid and result.context_string:
        return result.context_string
    return "\n\n".join(f"{document.title}\n{document.content}" for document in request.documents)


class FeedbackAnalysisService:
    def __init__(self, ai_client: BaseAIClient | None = None) -> None:
        self.ai_client = ai_client or get_ai_client()

    async def analyze(self, request: EvaluateAnswerRequest) -> FeedbackAnalysisOutput:
        question = request.question_answer
        if not question.rubric:
            raise ValueError("Feedback analysis requires an approved rubric.")

        transcript = question.student_answer.strip()
        vietnamese = uses_vietnamese(
            transcript,
            question.question_text,
            question.sample_answer or "",
            question.learning_outcome or "",
            question.topic or "",
            *(criterion.description for criterion in question.rubric),
        )
        if not transcript:
            return validate_analysis(blank_analysis(question.rubric, vietnamese), transcript, question.rubric)

        rubric_json = json.dumps(
            [criterion.model_dump() for criterion in question.rubric],
            ensure_ascii=False,
        )
        system_prompt = (
            _PROMPT.replace("{course_name}", request.course_name)
            .replace("{document_context}", _document_context(request))
            .replace("{question_text}", question.question_text)
            .replace("{topic}", (question.topic or "").strip() or "n/a")
            .replace("{learning_outcome}", (question.learning_outcome or "").strip())
            .replace("{sample_answer}", (question.sample_answer or "").strip())
            .replace("{rubric_json}", rubric_json)
            .replace("{sentences_json}", json.dumps(split_sentences(transcript), ensure_ascii=False))
            .replace("{transcript}", transcript)
        )
        payload = {
            "task": "analyze_answer",
            "question_id": question.question_id,
            "question_text": question.question_text,
            "transcript": transcript,
            "sample_answer": (question.sample_answer or "").strip(),
            "learning_outcome": (question.learning_outcome or "").strip(),
            "topic": (question.topic or "").strip(),
            "rubric": [criterion.model_dump() for criterion in question.rubric],
        }
        raw = await self.ai_client.complete(system_prompt, json.dumps(payload, ensure_ascii=False))
        try:
            parsed = parse_ai_output(raw, FeedbackAnalysisOutput)
            return validate_analysis(parsed, transcript, question.rubric)
        except ValueError as exc:
            logger.warning(f"[FeedbackAnalysis] Rejected invalid analysis: {exc}")
            raise ValueError("AI returned an invalid feedback analysis.") from exc
