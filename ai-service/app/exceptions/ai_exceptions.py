"""
AIVES AI Service - Custom Exceptions
Định nghĩa các exception chuyên biệt cho xử lý lỗi AI.
"""


class AIServiceException(Exception):
    """Base exception cho tất cả lỗi liên quan đến AI service."""
    
    def __init__(self, message: str, code: str = "AI_ERROR", retryable: bool = False):
        super().__init__(message)
        self.message = message
        self.code = code
        self.retryable = retryable


class AIValidationError(AIServiceException):
    """
    Exception khi AI response không hợp lệ.
    Ví dụ: thiếu field, sai format, score ngoài range.
    """
    
    def __init__(self, message: str, details: dict = None):
        super().__init__(
            message=message,
            code="AI_VALIDATION_ERROR",
            retryable=True  # Có thể retry vì AI có thể trả về đúng lần sau
        )
        self.details = details or {}


class AITimeoutError(AIServiceException):
    """
    Exception khi AI request vượt quá thời gian timeout.
    """
    
    def __init__(self, message: str = "AI request timed out", timeout_seconds: float = None):
        super().__init__(
            message=message,
            code="AI_TIMEOUT_ERROR",
            retryable=True
        )
        self.timeout_seconds = timeout_seconds


class AIProviderError(AIServiceException):
    """
    Exception khi AI provider trả về lỗi.
    Ví dụ: 429 Too Many Requests, 500 Internal Server Error.
    """
    
    def __init__(self, message: str, status_code: int = None, retryable: bool = True):
        super().__init__(
            message=message,
            code="AI_PROVIDER_ERROR",
            retryable=retryable
        )
        self.status_code = status_code


class AIParseError(AIServiceException):
    """
    Exception khi không parse được JSON response từ AI.
    """
    
    def __init__(self, message: str, raw_response: str = None):
        super().__init__(
            message=message,
            code="AI_PARSE_ERROR",
            retryable=True
        )
        self.raw_response = raw_response


class AIContractViolationError(AIServiceException):
    """
    Exception khi AI response vi phạm contract đã định nghĩa.
    """
    
    def __init__(self, message: str, contract_name: str = None, violations: list = None):
        super().__init__(
            message=message,
            code="AI_CONTRACT_VIOLATION",
            retryable=False  # Vi phạm contract nghiêm trọng, không nên retry
        )
        self.contract_name = contract_name
        self.violations = violations or []


class MaxRetriesExceededError(AIServiceException):
    """
    Exception khi đã retry quá số lần cho phép nhưng vẫn thất bại.
    """
    
    def __init__(self, message: str, max_retries: int = None, last_error: Exception = None):
        super().__init__(
            message=message,
            code="MAX_RETRIES_EXCEEDED",
            retryable=False
        )
        self.max_retries = max_retries
        self.last_error = last_error
