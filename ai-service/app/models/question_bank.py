from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, StringConstraints, model_validator

Text = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1)]


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
    max_score: float = Field(default=1.0, ge=0.01, le=100, allow_inf_nan=False)

    @model_validator(mode="after")
    def validate_documents(self) -> "GenerateQuestionsRequest":
        if {doc.document_type for doc in self.documents} != {"CONTENT", "LEARNING_OUTCOMES"}:
            raise ValueError("Provide exactly one CONTENT and one LEARNING_OUTCOMES document")
        if len({doc.document_id for doc in self.documents}) != 2:
            raise ValueError("Document IDs must be distinct")
        return self


class GeneratedQuestion(BaseModel):
    model_config = ConfigDict(extra="forbid")

    question_text: Text
    topic: Text
    sample_answer: Text
    max_score: float = Field(gt=0, le=100, allow_inf_nan=False, strict=True)
    level: int = Field(ge=1, le=3, strict=True)
    is_follow_up_allowed: bool = Field(strict=True)


class GenerateQuestionsResponse(BaseModel):
    model_config = ConfigDict(extra="forbid")

    questions: list[GeneratedQuestion] = Field(min_length=1, max_length=200)
