"""Explicit opt-in live check using synthetic course documents."""

import asyncio
import json

import pytest

from app.core.ai_client import get_ai_client
from app.core.config import get_settings
from app.models.question_bank import GenerateQuestionsRequest
from app.services.question_bank_service import QuestionBankService


@pytest.mark.integration
def test_live_question_bank_generates_keywords():
    settings = get_settings()
    if settings.ai_provider != "gemini" or not settings.gemini_api_key:
        pytest.skip("Requires Gemini configured with an API key")
    request = GenerateQuestionsRequest(
        documents=[
            {
                "document_id": 1, "document_type": "CONTENT",
                "content": (
                    "# Object-oriented programming\n"
                    "Encapsulation hides object state behind a public interface. Private fields "
                    "and controlled methods protect invariants, such as preventing a negative "
                    "bank account balance. Abstraction exposes essential operations and hides "
                    "implementation details. Inheritance allows a class to reuse and extend "
                    "another class. Polymorphism lets code call the same interface while "
                    "different implementations provide different behavior. For example, Circle "
                    "and Rectangle implement a Shape interface with different area methods. "
                    "Composition builds an object from collaborating objects instead of "
                    "requiring an inheritance relationship."
                ),
            },
            {
                "document_id": 2, "document_type": "LEARNING_OUTCOMES",
                "content": (
                    "Explain encapsulation and object invariants.\n"
                    "Compare abstraction and encapsulation.\n"
                    "Explain inheritance and polymorphism.\n"
                    "Apply interfaces to different shape implementations.\n"
                    "Compare composition and inheritance."
                ),
            },
        ], question_count=5, max_score=1.0,
    )
    result = asyncio.run(QuestionBankService(get_ai_client()).generate(request))
    assert len(result.questions) == 5
    for question in result.questions:
        assert 1 <= len(question.expected_keywords) <= 50
        assert all(any(char.isalnum() for char in word) for word in question.expected_keywords)
    print(json.dumps([question.expected_keywords for question in result.questions], ensure_ascii=True))
