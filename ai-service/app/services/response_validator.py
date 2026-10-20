"""
AIVES AI Service - Response Validator
Validator chặt chẽ cho AI responses trước khi trả về Client.

Chức năng:
- Sanitize markdown fence blocks (```json)
- Parse JSON hợp lệ
- Kiểm tra 100% fields bắt buộc
- Kiểm tra range: 0 <= score <= maxScore
- Kiểm tra range: 0 <= AiScore <= 100
- Kiểm tra range: 0.0 <= confidence <= 1.0
"""

import json
import re
from typing import TypeVar, Type, Optional
from pydantic import BaseModel, ValidationError
from loguru import logger

from app.models.contracts import (
    AIEvaluationContract,
    QuestionGenerationContract,
    FollowUpQuestionContract,
)
from app.exceptions.ai_exceptions import (
    AIValidationError,
    AIParseError,
    AIContractViolationError,
)


T = TypeVar("T", bound=BaseModel)


class ResponseValidator:
    """
    Service validate AI responses với các bước:
    1. Sanitize (làm sạch markdown)
    2. Parse JSON
    3. Validate contract với Pydantic
    4. Kiểm tra business rules (range, consistency)
    """
    
    @staticmethod
    def sanitize_markdown(raw_response: str) -> str:
        """
        Làm sạch chuỗi markdown thừa nếu AI trả về dạng ```json ... ```.
        
        Args:
            raw_response: Response thô từ AI
            
        Returns:
            JSON string đã làm sạch
        """
        if not isinstance(raw_response, str):
            raise AIParseError("AI response must be a string")
        
        raw = raw_response.strip()
        
        # Pattern 1: ```json\n{...}\n```
        if raw.startswith("```json") or raw.startswith("```JSON"):
            raw = re.sub(r'^```json\s*\n?', '', raw, flags=re.IGNORECASE)
            raw = re.sub(r'\n?```\s*$', '', raw)
        
        # Pattern 2: ```{...}```
        elif raw.startswith("```") and raw.endswith("```"):
            raw = raw[3:-3]
        
        # Pattern 3: Có thể còn markdown fence ở giữa
        raw = re.sub(r'```json\s*\n?', '', raw, flags=re.IGNORECASE)
        raw = re.sub(r'\n?```', '', raw)
        
        return raw.strip()
    
    @staticmethod
    def parse_json(json_string: str, context: str = "AI response") -> dict:
        """
        Parse JSON string với error handling chi tiết.
        
        Args:
            json_string: JSON string cần parse
            context: Context để log (ví dụ: "evaluation response")
            
        Returns:
            Parsed dict
            
        Raises:
            AIParseError: Khi parse JSON thất bại
        """
        try:
            data = json.loads(json_string)
            if not isinstance(data, dict):
                raise AIParseError(
                    f"{context} must be a JSON object, got {type(data).__name__}",
                    raw_response=json_string
                )
            return data
        except json.JSONDecodeError as exc:
            logger.error(f"[ResponseValidator] JSON parse error: {exc}")
            logger.debug(f"[ResponseValidator] Raw response: {json_string[:500]}")
            raise AIParseError(
                f"Failed to parse {context}: {str(exc)}",
                raw_response=json_string
            ) from exc
    
    @staticmethod
    def validate_contract(data: dict, contract_class: Type[T]) -> T:
        """
        Validate data theo Pydantic contract.
        
        Args:
            data: Dict data cần validate
            contract_class: Pydantic model class
            
        Returns:
            Validated contract instance
            
        Raises:
            AIContractViolationError: Khi data vi phạm contract
        """
        try:
            return contract_class.model_validate(data)
        except ValidationError as exc:
            violations = []
            for error in exc.errors():
                field = " -> ".join(str(loc) for loc in error["loc"])
                violations.append(f"{field}: {error['msg']}")
            
            logger.error(f"[ResponseValidator] Contract violation: {contract_class.__name__}")
            for violation in violations:
                logger.error(f"  - {violation}")
            
            raise AIContractViolationError(
                message=f"AI response violates {contract_class.__name__}",
                contract_name=contract_class.__name__,
                violations=violations
            ) from exc
    
    @classmethod
    def validate_evaluation_response(
        cls,
        raw_response: str,
        expected_max_score: Optional[float] = None
    ) -> AIEvaluationContract:
        """
        Validate response chấm điểm từ AI.
        
        Args:
            raw_response: Response thô từ AI
            expected_max_score: Max score mong đợi (để kiểm tra consistency)
            
        Returns:
            Validated AIEvaluationContract
            
        Raises:
            AIParseError, AIContractViolationError, AIValidationError
        """
        # Step 1: Sanitize markdown
        cleaned = cls.sanitize_markdown(raw_response)
        
        # Step 2: Parse JSON
        data = cls.parse_json(cleaned, context="evaluation response")
        
        # Step 3: Validate contract
        contract = cls.validate_contract(data, AIEvaluationContract)
        
        # Step 4: Business rules validation
        if expected_max_score is not None:
            # Kiểm tra tổng max_score của rubric
            total_max_score = sum(r.max_score for r in contract.rubric_results)
            if abs(total_max_score - expected_max_score) > 0.01:
                logger.warning(
                    f"[ResponseValidator] Max score mismatch: "
                    f"expected {expected_max_score}, got {total_max_score}"
                )
        
        logger.info(
            f"[ResponseValidator] Evaluation validated: "
            f"AiScore={contract.ai_score}, confidence={contract.confidence}, "
            f"rubric_count={len(contract.rubric_results)}"
        )
        
        return contract
    
    @classmethod
    def validate_question_generation_response(
        cls,
        raw_response: str
    ) -> QuestionGenerationContract:
        """
        Validate response sinh câu hỏi từ AI.
        
        Args:
            raw_response: Response thô từ AI
            
        Returns:
            Validated QuestionGenerationContract
        """
        cleaned = cls.sanitize_markdown(raw_response)
        data = cls.parse_json(cleaned, context="question generation response")
        contract = cls.validate_contract(data, QuestionGenerationContract)
        
        logger.info(
            f"[ResponseValidator] Question validated: "
            f"topic={contract.topic}, difficulty={contract.difficulty}"
        )
        
        return contract
    
    @classmethod
    def validate_followup_response(
        cls,
        raw_response: str
    ) -> FollowUpQuestionContract:
        """
        Validate response câu hỏi follow-up từ AI.
        
        Args:
            raw_response: Response thô từ AI
            
        Returns:
            Validated FollowUpQuestionContract
        """
        cleaned = cls.sanitize_markdown(raw_response)
        data = cls.parse_json(cleaned, context="follow-up response")
        contract = cls.validate_contract(data, FollowUpQuestionContract)
        
        logger.info(
            f"[ResponseValidator] Follow-up validated: "
            f"shouldFollowUp={contract.should_follow_up}"
        )
        
        return contract
    
    @staticmethod
    def check_required_fields(data: dict, required_fields: list[str]) -> None:
        """
        Kiểm tra các field bắt buộc có tồn tại không.
        
        Args:
            data: Dict data cần kiểm tra
            required_fields: Danh sách field bắt buộc
            
        Raises:
            AIValidationError: Khi thiếu field bắt buộc
        """
        missing = [field for field in required_fields if field not in data]
        if missing:
            raise AIValidationError(
                f"Missing required fields: {', '.join(missing)}",
                details={"missing_fields": missing}
            )
    
    @staticmethod
    def check_score_range(
        score: float,
        min_score: float,
        max_score: float,
        field_name: str = "score"
    ) -> None:
        """
        Kiểm tra score nằm trong range hợp lệ.
        
        Args:
            score: Điểm cần kiểm tra
            min_score: Điểm tối thiểu
            max_score: Điểm tối đa
            field_name: Tên field (để log)
            
        Raises:
            AIValidationError: Khi score ngoài range
        """
        if score < min_score or score > max_score:
            raise AIValidationError(
                f"{field_name} must be between {min_score} and {max_score}, got {score}",
                details={
                    "field": field_name,
                    "value": score,
                    "min": min_score,
                    "max": max_score
                }
            )


# Singleton instance
_validator = ResponseValidator()


def get_validator() -> ResponseValidator:
    """Get singleton validator instance."""
    return _validator
