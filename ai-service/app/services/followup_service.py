"""
AIVES AI Service - Follow-up Question Service
Service sinh câu hỏi hỏi xoáy/đào sâu dựa trên câu trả lời của học viên.

Chức năng:
- Phân tích transcript để tìm điểm thiếu/chưa rõ
- Sinh câu hỏi follow-up không trùng lặp
- Đảm bảo câu hỏi bám sát learning outcomes
"""

import json
from pathlib import Path
from typing import List, Optional
from loguru import logger

from app.core.ai_client import BaseAIClient, get_ai_client
from app.models.request import DocumentContext
from app.models.contracts import FollowUpQuestionContract
from app.services.response_validator import get_validator
from app.services.markdown_processor import MarkdownProcessor


_PROMPT_PATH = Path(__file__).parent.parent / "prompts" / "followup_prompt.txt"
_md_processor = MarkdownProcessor()


class FollowUpService:
    """
    Service sinh câu hỏi follow-up dựa trên:
    - Câu hỏi gốc
    - Câu trả lời của học viên
    - Learning outcomes
    - Lịch sử câu hỏi đã hỏi (để tránh trùng lặp)
    """
    
    def __init__(self, ai_client: Optional[BaseAIClient] = None):
        self.ai_client = ai_client or get_ai_client()
        self.validator = get_validator()
        
        # Load prompt template
        if _PROMPT_PATH.exists():
            self._prompt_template = _PROMPT_PATH.read_text(encoding="utf-8")
        else:
            self._prompt_template = self._get_default_prompt_template()
    
    def _get_default_prompt_template(self) -> str:
        """Default prompt template nếu file không tồn tại."""
        return """You are an expert interviewer evaluating student answers.

Course: {course_name}

Context:
{document_context}

Original Question: {original_question}
Student Answer: {student_answer}

Previously Asked Questions:
{previous_questions}

Your task:
1. Analyze the student's answer to identify missing points, unclear explanations, or incomplete coverage of the learning outcomes.
2. Determine if a follow-up question is needed.
3. If needed, generate a follow-up question that:
   - Probes deeper into missing or unclear points
   - Does NOT repeat any previously asked questions
   - Is specific and focused on gaps in the student's understanding
   - Relates to the learning outcomes

Return ONLY a JSON object with this structure:
{
  "shouldFollowUp": true/false,
  "followUpQuestion": "the follow-up question text (empty if shouldFollowUp is false)",
  "reason": "explanation of why follow-up is needed or not needed",
  "missingPoints": ["point 1", "point 2", ...]
}"""
    
    def _build_document_context(self, documents: List[DocumentContext]) -> str:
        """Build context từ documents."""
        result = _md_processor.process(documents)
        if result.is_valid and result.context_string:
            return result.context_string
        
        # Fallback
        parts = []
        for doc in documents:
            parts.append(f"### {doc.title}\n\n{doc.content}")
        return "\n\n---\n\n".join(parts)
    
    async def generate_follow_up(
        self,
        course_name: str,
        documents: List[DocumentContext],
        original_question: str,
        student_answer: str,
        previous_questions: Optional[List[str]] = None,
        learning_outcomes: Optional[List[str]] = None
    ) -> FollowUpQuestionContract:
        """
        Sinh câu hỏi follow-up.
        
        Args:
            course_name: Tên môn học
            documents: Course content và learning outcomes
            original_question: Câu hỏi gốc
            student_answer: Câu trả lời của học viên
            previous_questions: Danh sách câu hỏi đã hỏi (để tránh trùng)
            learning_outcomes: Learning outcomes cần đánh giá
            
        Returns:
            FollowUpQuestionContract với câu hỏi follow-up (nếu cần)
            
        Raises:
            AIValidationError, AIParseError nếu response không hợp lệ
        """
        logger.info(
            f"[FollowUpService] Generating follow-up for question in course: {course_name}"
        )
        
        # Build context
        document_context = self._build_document_context(documents)
        previous_q = previous_questions or []
        prev_q_text = "\n".join(f"- {q}" for q in previous_q) if previous_q else "None"
        
        # Build prompt
        system_prompt = self._prompt_template
        system_prompt = system_prompt.replace("{course_name}", course_name)
        system_prompt = system_prompt.replace("{document_context}", document_context)
        system_prompt = system_prompt.replace("{original_question}", original_question)
        system_prompt = system_prompt.replace("{student_answer}", student_answer)
        system_prompt = system_prompt.replace("{previous_questions}", prev_q_text)
        
        user_message = json.dumps(
            {
                "task": "generate_follow_up",
                "learning_outcomes": learning_outcomes or [],
            },
            ensure_ascii=False
        )
        
        # Call AI
        raw_response = await self.ai_client.complete_with_retry(system_prompt, user_message)
        
        # Validate response
        contract = self.validator.validate_followup_response(raw_response)
        
        logger.info(
            f"[FollowUpService] Follow-up generated: "
            f"shouldFollowUp={contract.should_follow_up}, "
            f"missing_points={len(contract.missing_points)}"
        )
        
        return contract
    
    async def should_ask_follow_up(
        self,
        course_name: str,
        documents: List[DocumentContext],
        original_question: str,
        student_answer: str,
        min_answer_length: int = 50
    ) -> bool:
        """
        Kiểm tra nhanh xem có nên hỏi thêm không dựa trên:
        - Độ dài câu trả lời
        - Nội dung câu trả lời
        
        Args:
            course_name: Tên môn học
            documents: Course content
            original_question: Câu hỏi gốc
            student_answer: Câu trả lời
            min_answer_length: Độ dài tối thiểu để không hỏi thêm
            
        Returns:
            True nếu nên hỏi thêm, False nếu không
        """
        # Quick check: câu trả lời quá ngắn -> cần hỏi thêm
        if len(student_answer.strip()) < min_answer_length:
            logger.info("[FollowUpService] Answer too short, follow-up recommended")
            return True
        
        # Call AI để phân tích chi tiết
        try:
            result = await self.generate_follow_up(
                course_name=course_name,
                documents=documents,
                original_question=original_question,
                student_answer=student_answer
            )
            return result.should_follow_up
        except Exception as exc:
            logger.error(f"[FollowUpService] Error checking follow-up: {exc}")
            # Mặc định không hỏi thêm nếu có lỗi
            return False


# Singleton instance
_service: Optional[FollowUpService] = None


def get_followup_service() -> FollowUpService:
    """Get singleton follow-up service instance."""
    global _service
    if _service is None:
        _service = FollowUpService()
    return _service
