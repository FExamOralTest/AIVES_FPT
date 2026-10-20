from contextlib import closing
from functools import lru_cache

from app.core.config import get_settings
from app.services.stt_errors import SttError

settings = get_settings()


class AudioStorageError(Exception):
    def __init__(self, status_code: int, message: str) -> None:
        super().__init__(message)
        self.status_code = status_code


@lru_cache(maxsize=1)
def _get_r2_client():
    import boto3
    from botocore.config import Config

    endpoint = settings.r2_endpoint_url or (
        f"https://{settings.r2_account_id}.r2.cloudflarestorage.com"
        if settings.r2_account_id else ""
    )
    if not all((endpoint, settings.r2_access_key_id, settings.r2_secret_access_key, settings.r2_bucket_name)):
        raise AudioStorageError(503, "R2 audio storage is not configured.")

    return boto3.client(
        "s3",
        endpoint_url=endpoint,
        aws_access_key_id=settings.r2_access_key_id,
        aws_secret_access_key=settings.r2_secret_access_key,
        region_name="auto",
        config=Config(
            signature_version="s3v4",
            connect_timeout=10,
            read_timeout=30,
            retries={"mode": "standard", "total_max_attempts": 2},
        ),
    )


def download_audio(object_key: str) -> bytes:
    from botocore.exceptions import BotoCoreError, ClientError

    try:
        response = _get_r2_client().get_object(Bucket=settings.r2_bucket_name, Key=object_key)
        with closing(response["Body"]) as body:
            if response.get("ContentLength", 0) > settings.stt_max_audio_bytes:
                raise SttError("UNSUPPORTED_FORMAT", "Audio exceeds the configured size limit.", 413)
            audio = body.read(settings.stt_max_audio_bytes + 1)
        if len(audio) > settings.stt_max_audio_bytes:
            raise SttError("UNSUPPORTED_FORMAT", "Audio exceeds the configured size limit.", 413)
        if not audio:
            raise SttError("EMPTY_AUDIO")
        return audio
    except ClientError as exc:
        code = exc.response.get("Error", {}).get("Code")
        if code in {"NoSuchKey", "NotFound", "404"}:
            raise AudioStorageError(404, "Audio object was not found.") from exc
        raise AudioStorageError(503, "R2 audio download failed.") from exc
    except BotoCoreError as exc:
        raise AudioStorageError(503, "R2 audio download failed.") from exc
