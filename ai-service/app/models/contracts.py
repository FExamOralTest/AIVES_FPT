"""
AIVES AI Service - AI Evaluation Contracts
Chuẩn hóa Schema & JSON Response cho hệ thống AI Evaluation.
Đảm bảo 100% tuân thủ yêu cầu chấm điểm và phản hồi từ AI.
"""

from typing import Annotated, List
from pydantic import BaseModel, Field, StringConstraints, model_validator

from app.models.request import AnswerRubricCriterion


NonEmptyText = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1)]


class QuestionGenerationContract(BaseModel):
    """
    Contract cho response sinh câu hỏi từ AI.
    Đảm bảo câu hỏi có đầy đủ thông tin cần thiết.
    """
    
    question_text: NonEmptyText = Field(
        ...,
        alias="questionText",
        description="Nội dung câu hỏi"
    )
    topic: NonEmptyText = Field(
        ...,
        description="Chủ đề của câu hỏi"
    )
    learning_outcome: NonEmptyText = Field(
        ...,
        alias="learningOutcome",
        description="Learning outcome tương ứng"
    )
    difficulty: Annotated[int, Field(ge=1, le=3)] = Field(
        ...,
        description="Độ khó: 1=easy, 2=medium, 3=hard"
    )
    expected_keywords: List[NonEmptyText] = Field(
        default_factory=list,
        alias="expectedKeywords",
        description="Từ khóa mong đợi trong câu trả lời"
    )
    sample_answer: NonEmptyText = Field(
        ...,
        alias="sampleAnswer",
        description="Câu trả lời mẫu"
    )
    rubric: List[AnswerRubricCriterion] = Field(
        default_factory=list,
        min_length=1,
        description="Rubric chấm điểm cho câu hỏi"
    )


class FollowUpQuestionContract(BaseModel):
    """
    Contract cho câu hỏi follow-up/hỏi xoáy.
    Đảm bảo câu hỏi không trùng lặp và có lý do rõ ràng.
    """
    
    should_follow_up: bool = Field(
        ...,
        alias="shouldFollowUp",
        description="Có cần hỏi thêm không"
    )
    follow_up_question: str = Field(
        default="",
        alias="followUpQuestion",
        description="Câu hỏi follow-up (rỗng nếu shouldFollowUp=False)"
    )
    reason: NonEmptyText = Field(
        ...,
        description="Lý do cần hỏi thêm hoặc không cần"
    )
    missing_points: List[str] = Field(
        default_factory=list,
        alias="missingPoints",
        description="Các điểm còn thiếu trong câu trả lời"
    )
    
    @model_validator(mode="after")
    def validate_follow_up_consistency(self) -> "FollowUpQuestionContract":
        """Kiểm tra tính nhất quán giữa shouldFollowUp và followUpQuestion."""
        if self.should_follow_up and not self.follow_up_question.strip():
            raise ValueError("followUpQuestion không được rỗng khi shouldFollowUp=True")
        if not self.should_follow_up and self.follow_up_question.strip():
            raise ValueError("followUpQuestion phải rỗng khi shouldFollowUp=False")
        return self
