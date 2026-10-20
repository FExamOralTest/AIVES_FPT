class SttError(ValueError):
    _errors = {
        "EMPTY_AUDIO": (400, False, "Audio file is empty."),
        "UNSUPPORTED_FORMAT": (415, False, "Audio format is not supported."),
        "AUDIO_CORRUPTED": (400, False, "Audio could not be decoded."),
        "NO_SPEECH": (422, False, "No speech was detected in the audio."),
        "EMPTY_TRANSCRIPT": (422, False, "The STT provider returned no transcript."),
        "TIMEOUT": (504, True, "Audio transcription timed out."),
        "INTERNAL_ERROR": (500, True, "Audio transcription failed."),
    }

    def __init__(self, code: str, message: str | None = None, status_code: int | None = None) -> None:
        default_status, self.retryable, default_message = self._errors[code]
        self.code = code
        self.status_code = status_code or default_status
        super().__init__(message or default_message)
