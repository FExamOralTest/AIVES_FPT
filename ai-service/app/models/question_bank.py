from decimal import Decimal
import re
import unicodedata
from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, StringConstraints, field_validator, model_validator

Text = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1)]


def score_decimal(value: float) -> Decimal:
    score = Decimal(str(value))
    if score != score.quantize(Decimal("0.0001")):
        raise ValueError("Scores must have at most four decimal places")
    return score


class RubricCriterion(BaseModel):
    model_config = ConfigDict(extra="forbid")

    description: Text = Field(max_length=2000)
    max_score: float = Field(gt=0, le=100, allow_inf_nan=False, strict=True)


class QuestionBankDocument(BaseModel):
    model_config = ConfigDict(extra="forbid")

    document_id: int = Field(gt=0)
    document_type: Literal["CONTENT", "LEARNING_OUTCOMES"]
    content: Text


class GenerateQuestionsRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    documents: list[QuestionBankDocument] = Field(min_length=2, max_length=2)
    question_count: int = Field(ge=5, le=200, strict=True)
    focus_topics: list[Text] = Field(default_factory=list)
    max_score: float = Field(default=1.0, ge=0.01, le=100, allow_inf_nan=False, strict=True)

    @model_validator(mode="after")
    def validate_documents(self) -> "GenerateQuestionsRequest":
        score_decimal(self.max_score)
        if {doc.document_type for doc in self.documents} != {"CONTENT", "LEARNING_OUTCOMES"}:
            raise ValueError("Provide exactly one CONTENT and one LEARNING_OUTCOMES document")
        if len({doc.document_id for doc in self.documents}) != 2:
            raise ValueError("Document IDs must be distinct")
        return self


class GeneratedQuestion(BaseModel):
    model_config = ConfigDict(extra="forbid")

    question_text: Text = Field(max_length=10000)
    topic: Text = Field(max_length=1000)
    sample_answer: Text = Field(max_length=10000)
    learning_outcome: Text = Field(max_length=2000)
    rubric: list[RubricCriterion] = Field(min_length=1, max_length=20)
    max_score: float = Field(gt=0, le=100, allow_inf_nan=False, strict=True)
    level: int = Field(ge=1, le=3, strict=True)
    is_follow_up_allowed: bool = Field(strict=True)
    expected_keywords: list[Text] = Field(min_length=1, max_length=50)

    @field_validator("expected_keywords")
    @classmethod
    def validate_keywords(cls, keywords: list[str]) -> list[str]:
        unique = []
        seen = set()
        for keyword in keywords:
            keyword = " ".join(keyword.split())
            key = unicodedata.normalize("NFKC", keyword).casefold()
            if not any(character.isalnum() for character in key):
                raise ValueError("Expected keywords must contain meaningful concepts")
            if key not in seen:
                unique.append(keyword)
                seen.add(key)
        return unique

    @model_validator(mode="after")
    def validate_rubric(self) -> "GeneratedQuestion":
        scores = [score_decimal(criterion.max_score) for criterion in self.rubric]
        if sum(scores) != score_decimal(self.max_score):
            raise ValueError("Rubric scores must sum to max_score")
        seen = set()
        for criterion in self.rubric:
            key = " ".join(re.sub(r"[^\w\s]", " ", unicodedata.normalize(
                "NFKC", criterion.description
            ).casefold()).split())
            if not key or key in seen:
                raise ValueError("Rubric criteria must be meaningful and distinct")
            seen.add(key)
        return self


class GenerateQuestionsResponse(BaseModel):
    model_config = ConfigDict(extra="forbid")

    questions: list[GeneratedQuestion] = Field(min_length=1, max_length=200)
