"""Keyword validation and generation checks, independent of a live AI provider."""

import asyncio
import json
from unittest.mock import AsyncMock

import pytest
from pydantic import ValidationError

from app.core.ai_client import MockAIClient
from app.models.question_bank import GeneratedQuestion, GenerateQuestionsRequest
from app.services.question_bank_service import QuestionBankService
from app.services.question_service import QuestionGenerationError


def question_payload():
    return {
        "question_text": "Explain encapsulation in OOP",
        "topic": "OOP",
        "sample_answer": "Encapsulation hides state using access modifiers.",
        "learning_outcome": "Explain encapsulation in OOP",
        "rubric": [{"description": "Explain hiding state", "max_score": 1.0}],
        "max_score": 1.0,
        "level": 1,
        "is_follow_up_allowed": True,
        "expected_keywords": ["encapsulation", "access modifiers"],
    }


def bank_request(count=5):
    return GenerateQuestionsRequest(
        documents=[
            {"document_id": 1, "document_type": "CONTENT",
             "content": "# OOP\nEncapsulation hides state using access modifiers."},
            {"document_id": 2, "document_type": "LEARNING_OUTCOMES",
             "content": "Explain encapsulation in OOP"},
        ],
        question_count=count, focus_topics=["encapsulation"], max_score=1.0,
    )


def test_keywords_are_required():
    payload = question_payload()
    del payload["expected_keywords"]
    with pytest.raises(ValidationError, match="expected_keywords"):
        GeneratedQuestion.model_validate(payload)


@pytest.mark.parametrize("keywords", [None, [], [""], ["   "], ["..."],
                                      ["___"], ["valid", " "], ["concept"] * 51])
def test_invalid_keywords_are_rejected(keywords):
    with pytest.raises(ValidationError, match="expected_keywords"):
        GeneratedQuestion.model_validate({**question_payload(), "expected_keywords": keywords})


def test_keywords_are_trimmed_deduplicated_and_preserve_domain_symbols():
    keywords = [" Encapsulation ", "ENCAPSULATION", "access   modifiers",
                "đóng gói", "C++", "C#"]
    question = GeneratedQuestion.model_validate({**question_payload(), "expected_keywords": keywords})
    assert question.expected_keywords == ["Encapsulation", "access modifiers", "đóng gói", "C++", "C#"]


def test_mock_generates_keywords_in_every_batch():
    result = asyncio.run(QuestionBankService(MockAIClient()).generate(bank_request(7)))
    assert len(result.questions) == 7
    assert all(question.expected_keywords == ["encapsulation"] for question in result.questions)
    assert all(question["expected_keywords"] for question in result.model_dump()["questions"])


@pytest.mark.parametrize("keywords", [None, []])
def test_provider_cannot_silently_omit_keywords(keywords):
    questions = [{**question_payload(), "question_text": f"Explain encapsulation example {i}"}
                 for i in range(5)]
    for question in questions:
        if keywords is None:
            del question["expected_keywords"]
        else:
            question["expected_keywords"] = keywords
    client = AsyncMock()
    client.complete.return_value = json.dumps({"questions": questions})
    with pytest.raises(QuestionGenerationError):
        asyncio.run(QuestionBankService(client).generate(bank_request()))
