from typing import Annotated, TypeVar

from pydantic import (
    BaseModel,
    ConfigDict,
    StringConstraints,
    ValidationError,
    model_validator,
)


NonEmptyText = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1)]


class FollowUpOutput(BaseModel):
    model_config = ConfigDict(strict=True, extra="forbid")

    should_follow_up: bool
    follow_up_question: Annotated[str, StringConstraints(strip_whitespace=True)]
    reason: NonEmptyText

    @model_validator(mode="after")
    def validate_decision(self) -> "FollowUpOutput":
        if self.should_follow_up != bool(self.follow_up_question):
            raise ValueError("Follow-up question must match should_follow_up.")
        return self


Output = TypeVar("Output", bound=BaseModel)


def parse_ai_output(raw: str, model: type[Output]) -> Output:
    if not isinstance(raw, str):
        raise ValueError("AI output must be a JSON object as text.")
    try:
        return model.model_validate_json(raw)
    except ValidationError as exc:
        raise ValueError("AI output does not match the required response fields.") from exc
