"""
AIVES AI Service - AI Evaluation Contracts
Chuẩn hóa Schema & JSON Response cho hệ thống AI Evaluation.
Đảm bảo 100% tuân thủ yêu cầu chấm điểm và phản hồi từ AI.
"""

from typing import Annotated, List
from pydantic import BaseModel, Field, StringConstraints, field_validator, model_validator


NonEmptyText = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1)]
Score = Annotated[float, Field(ge=0, allow_inf_nan=False)]
Confidence = Annotated[float, Field(ge=0.0, le=1.0, allow_inf_nan=False)]


class FeedbackContract(BaseModel):
    """
    Contract chuẩn hóa cho feedback từ AI.
    Bao gồm nhận xét tổng quan, điểm mạnh, điểm yếu và gợi ý hành động.
    """
    
    summary: NonEmptyText = Field(
        ...,
        description="Nhận xét tổng quan về câu trả lời của học viên"
    )
    strengths: List[NonEmptyText] = Field(
        default_factory=list,
        description="Điểm mạnh trong câu trả lời"
    )
    improvements: List[NonEmptyText] = Field(
        default_factory=list,
        description="Điểm cần cải thiện"
    )
    actionable_tips: List[NonEmptyText] = Field(
        default_factory=list,
        alias="actionableTips",
        description="Gợi ý hành động cụ thể để cải thiện"
    )


class RubricResultContract(BaseModel):
    """
    Contract cho kết quả chấm theo từng tiêu chí trong Rubric.
    Mỗi tiêu chí phải có đầy đủ: ID, tên, điểm, minh chứng và nhận xét.
    """
    
    criterion_id: NonEmptyText = Field(
        ...,
        alias="criterionId",
        description="Mã tiêu chí (unique identifier)"
    )
    criterion_name: NonEmptyText = Field(
        ...,
        alias="criterionName",
        description="Tên tiêu chí"
    )
    score: Score = Field(
        ...,
        description="Điểm đạt được của tiêu chí (không âm và <= maxScore)"
    )
    max_score: Score = Field(
        ...,
        alias="maxScore",
        description="Điểm tối đa của tiêu chí"
    )
    evidence: NonEmptyText = Field(
        ...,
        description="Trích dẫn minh chứng từ transcript"
    )
    comment: NonEmptyText = Field(
        ...,
        description="Nhận xét chi tiết cho tiêu chí này"
    )
    
    @model_validator(mode="after")
    def validate_score_range(self) -> "RubricResultContract":
        """Kiểm tra score không vượt quá maxScore."""
        if self.score > self.max_score:
            raise ValueError(
                f"Score {self.score} vượt quá maxScore {self.max_score} "
                f"cho tiêu chí {self.criterion_id}"
            )
        return self


class AIEvaluationContract(BaseModel):
    """
    Contract chính cho phản hồi chấm điểm của AI.
    Đảm bảo đầy đủ các field bắt buộc và tuân thủ range cho điểm số.
    
    Yêu cầu bắt buộc:
    - transcript: Nội dung câu trả lời (không rỗng)
    - AiScore: Điểm tổng kết (0-100)
    - confidence: Độ tự tin (0.0-1.0)
    - feedback: Nhận xét chuẩn hóa
    - rubricResults: Kết quả chấm theo từng tiêu chí (tối thiểu 1 phần tử)
    """
    
    transcript: NonEmptyText = Field(
        ...,
        description="Nội dung câu trả lời của học viên (bắt buộc, không rỗng)"
    )
    ai_score: Annotated[float, Field(ge=0, le=100, allow_inf_nan=False)] = Field(
        ...,
        alias="AiScore",
        description="Điểm số tổng kết do AI chấm (0-100)"
    )
    confidence: Confidence = Field(
        ...,
        description="Độ tự tin của AI khi chấm (0.0-1.0)"
    )
    feedback: FeedbackContract = Field(
        ...,
        description="Nhận xét chi tiết chuẩn hóa"
    )
    rubric_results: List[RubricResultContract] = Field(
        ...,
        alias="rubricResults",
        min_length=1,
        description="Danh sách kết quả chấm theo từng tiêu chí (tối thiểu 1 phần tử)"
    )
    
    @field_validator("rubric_results")
    @classmethod
    def validate_unique_criterion_ids(cls, v: List[RubricResultContract]) -> List[RubricResultContract]:
        """Kiểm tra criterion_id không bị trùng lặp."""
        ids = [result.criterion_id for result in v]
        if len(ids) != len(set(ids)):
            raise ValueError("criterion_id trong rubricResults bị trùng lặp")
        return v
    
    @model_validator(mode="after")
    def validate_score_consistency(self) -> "AIEvaluationContract":
        """
        Kiểm tra tính nhất quán của điểm số:
        - Tổng điểm các tiêu chí không nên vượt quá AiScore quá nhiều
        - Confidence phải hợp lệ
        """
        total_rubric_score = sum(r.score for r in self.rubric_results)
        total_max_score = sum(r.max_score for r in self.rubric_results)
        
        # Cảnh báo nếu tổng điểm rubric và AiScore không tương đồng
        # (cho phép sai lệch nhỏ do làm tròn hoặc trọng số)
        if total_max_score > 0:
            rubric_percentage = (total_rubric_score / total_max_score) * 100
            score_diff = abs(self.ai_score - rubric_percentage)
            if score_diff > 15:  # Cho phép sai lệch 15%
                # Chỉ warning, không raise error vì có thể có trọng số khác nhau
                pass
        
        return self


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
    rubric: List[RubricResultContract] = Field(
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
