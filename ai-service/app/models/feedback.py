from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, StringConstraints


NonEmptyText = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1)]
SentenceJudgment = Literal["correct", "error", "omission", "irrelevant"]
CriterionJudgment = Literal["met", "partial", "not_met"]


class SentenceFeedback(BaseModel):
    model_config = ConfigDict(extra="forbid")

    sentence: NonEmptyText
    judgment: SentenceJudgment
    comment: NonEmptyText


class CriterionAnalysis(BaseModel):
    model_config = ConfigDict(extra="forbid")

    criterion_id: NonEmptyText
    judgment: CriterionJudgment
    awarded_score: float = Field(ge=0, allow_inf_nan=False)
    explanation: NonEmptyText
    errors: list[NonEmptyText]
    omissions: list[NonEmptyText]
    evidence_quotes: list[NonEmptyText]


class FeedbackAnalysisOutput(BaseModel):
    model_config = ConfigDict(extra="forbid")

    sentence_feedback: list[SentenceFeedback]
    criterion_analyses: list[CriterionAnalysis]
    strengths: list[NonEmptyText]
    weaknesses: list[NonEmptyText]
    missing_content: list[NonEmptyText]
    improvements: list[NonEmptyText]
    feedback: NonEmptyText
