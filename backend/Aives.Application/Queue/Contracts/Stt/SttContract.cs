using Aives.Application.AI.DTOs;

namespace Aives.Application.Queue.Contracts.Stt;

/// <summary>
/// Task 1.5 — STT Response Contract.
/// Chốt duy nhất format mà AI1 (Faster-Whisper) phải trả về cho Backend, gồm
/// <c>transcript</c>, <c>language</c>, <c>duration</c> và <c>error</c>.
/// </summary>
/// <remarks>
/// <para><b>Vì sao cần một contract riêng?</b></para>
/// <list type="bullet">
///   <item>AI1 đổi field hoặc đổi tên error code → Backend phải map về 1 taxonomy ổn định.</item>
///   <item>Backend tuyệt đối không phụ thuộc schema gốc của AI1: mọi mapping nằm ở đây.</item>
///   <item>Retry / dead-letter là quyết định <b>hậu quả</b> (mất điểm, tốn compute) → phải
///         quyết định từ code chuẩn hoá, không từ raw error của AI1.</item>
/// </list>
/// <para><b>Wire format chuẩn</b> (<c>POST /ai/speech/transcribe-object</c>):</para>
/// <code>
/// {
///   "success": true,
///   "message": null,
///   "transcript": "Nội dung sinh viên nói",
///   "language": "vi",
///   "duration": 12.34,
///   "error": null
/// }
/// </code>
/// Khi <c>success = false</c>, <c>error</c> BẮT BUỘC có:
/// <code>
/// { "code": "AUDIO_CORRUPTED", "message": "...", "retryable": false }
/// </code>
/// </remarks>
public static class SttContract
{
    /// <summary>
    /// Giá trị tối đa cho <c>message</c> của error — giữ log gọn và tránh lộ dữ liệu thô.
    /// </summary>
    public const int MaxErrorMessageLength = 500;

    /// <summary>
    /// Độ dài tối đa cho mã ngôn ngữ ISO-639-1 (vd: <c>vi</c>, <c>en</c>).
    /// </summary>
    public const int MaxLanguageLength = 20;

    /// <summary>
    /// Độ dài tối đa cho error code — chặn code rác quá dài (rò rỉ payload / log spam).
    /// </summary>
    public const int MaxErrorCodeLength = 64;

    /// <summary>
    /// Ngôn ngữ fallback khi AI1 không trả <c>language</c> — dự án chủ yếu chấm bài tiếng Việt.
    /// </summary>
    public const string DefaultLanguage = "vi";

    // ────────────────────────────────────────────────────────────────────
    //  Canonical error codes (wire format)
    // ────────────────────────────────────────────────────────────────────

    /// <summary>Audio hỏng / không decode được. Không retry.</summary>
    public const string CodeAudioCorrupted = "AUDIO_CORRUPTED";

    /// <summary>File audio rỗng (0 byte). Không retry — alias của <see cref="CodeAudioCorrupted"/>.</summary>
    public const string CodeEmptyAudio = "EMPTY_AUDIO";

    /// <summary>Whisper chạy xong nhưng transcript rỗng. Không retry.</summary>
    public const string CodeEmptyTranscript = "EMPTY_TRANSCRIPT";

    /// <summary>Audio không có tiếng nói. Không retry — alias của <see cref="CodeEmptyTranscript"/>.</summary>
    public const string CodeNoSpeech = "NO_SPEECH";

    /// <summary>Định dạng/sample rate/duration không được hỗ trợ. Không retry.</summary>
    public const string CodeUnsupportedFormat = "UNSUPPORTED_FORMAT";

    /// <summary>AI1 xử lý quá thời gian cho phép. CÓ retry.</summary>
    public const string CodeTimeout = "TIMEOUT";

    /// <summary>Lỗi nội bộ phía AI1 (GPU OOM, model load fail…). CÓ retry.</summary>
    public const string CodeInternalError = "INTERNAL_ERROR";

    /// <summary>Response của AI1 không khớp contract (thiếu field, sai kiểu, sai answerId). Không retry.</summary>
    public const string CodeInvalidResponse = "INVALID_RESPONSE";

    /// <summary>AI1 từ chối request vì internal token sai. Không retry (fail-fast).</summary>
    public const string CodeAuthFailed = "AUTH_FAILED";

    /// <summary>
    /// Tất cả error code hợp lệ trong wire format. AI1 không được trả code ngoài danh sách này —
    /// code lạ sẽ bị map về <see cref="SttErrorCode.Unknown"/> (mặc định retry được) và được log cảnh báo.
    /// </summary>
    public static readonly IReadOnlyCollection<string> AllCodes = new[]
    {
        CodeAudioCorrupted,
        CodeEmptyAudio,
        CodeEmptyTranscript,
        CodeNoSpeech,
        CodeUnsupportedFormat,
        CodeTimeout,
        CodeInternalError,
        CodeInvalidResponse,
        CodeAuthFailed
    };

    // ────────────────────────────────────────────────────────────────────
    //  Mapping
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Code lỗi có nằm trong taxonomy đã biết không?
    /// </summary>
    /// <remarks>
    /// Dùng để phân biệt "code đã biết" (retry decision chắc chắn) với "code mới"
    /// (forward-compatibility: chấp nhận nhưng cần cập nhật taxonomy).
    /// </remarks>
    public static bool IsKnownCode(string? code) =>
        code != null && AllCodes.Contains(code.Trim().ToUpperInvariant());

    /// <summary>
    /// Map error code của AI1 (hoặc HTTP status) về taxonomy nội bộ <see cref="SttErrorCode"/>.
    /// </summary>
    /// <param name="code">Error code trả về từ AI1 (không phân biệt hoa/thường).</param>
    public static SttErrorCode MapToErrorCode(string? code) =>
        code?.Trim().ToUpperInvariant() switch
        {
            CodeAudioCorrupted or CodeEmptyAudio => SttErrorCode.AudioCorrupted,
            CodeEmptyTranscript or CodeNoSpeech => SttErrorCode.EmptyTranscript,
            CodeUnsupportedFormat => SttErrorCode.UnsupportedFormat,
            CodeTimeout => SttErrorCode.Timeout,
            CodeInternalError => SttErrorCode.InternalError,
            // INVALID_RESPONSE / AUTH_FAILED / code lạ → Unknown (retry mặc định),
            // nhưng AnswerProcessingJob vẫn dead-letter được vì AI1 trả success=false
            // với code không retryable — xem IsRetryableCode.
            _ => SttErrorCode.Unknown
        };

    /// <summary>
    /// Error code có an toàn để retry không.
    /// </summary>
    /// <remarks>
    /// Retry chỉ hợp lý khi lỗi là <b>tạm thời</b>. Retry một lỗi kinh doán (audio hỏng,
    /// transcript rỗng) chỉ tốn thêm compute và vẫn ra cùng kết quả.
    /// <para>
    /// Code <b>không nhận diện được</b> được coi là retryable — an toàn: nếu AI1 vừa deploy
    /// code mới, mình không biết lỗi đó là tạm thời hay vĩnh viễn, nên thử lại 1 lần
    /// rẻ hơn là bỏ luôn bài. Ngược lại, các code biết rõ là lỗi vĩnh viễn
    /// (<c>INVALID_RESPONSE</c>, <c>AUTH_FAILED</c>) tuyệt đối không retry.
    /// </para>
    /// </remarks>
    public static bool IsRetryableCode(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
            return true;

        return normalized switch
        {
            CodeTimeout or CodeInternalError => true,
            CodeAudioCorrupted or CodeEmptyAudio or CodeEmptyTranscript or CodeNoSpeech
                or CodeUnsupportedFormat or CodeInvalidResponse or CodeAuthFailed => false,
            _ => true
        };
    }

    /// <summary>
    /// Map HTTP status trả về từ AI1 sang error code chuẩn.
    /// Dùng khi AI1 trả lỗi mà body không theo contract (proxy trả HTML, deploy cũ…).
    /// </summary>
    public static string MapHttpStatusToCode(int statusCode) => statusCode switch
    {
        400 or 404 => CodeAudioCorrupted,
        413 or 415 => CodeUnsupportedFormat,
        422 => CodeEmptyTranscript,
        408 or 504 => CodeTimeout,
        401 or 403 => CodeAuthFailed,
        _ => CodeInternalError
    };

    // ────────────────────────────────────────────────────────────────────
    //  Validation
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validate response của AI1 theo contract.
    /// </summary>
    /// <remarks>
    /// <b>Quy tắc:</b>
    /// <list type="number">
    ///   <item><c>success = true</c> → <c>transcript</c> phải có (có thể rỗng = audio im lặng),
    ///         <c>language</c> phải là mã ISO-639-1 hợp lệ, <c>duration</c> phải là số hữu hạn ≥ 0,
    ///         <c>confidence</c> (nếu có) phải nằm trong [0, 1].</item>
    ///   <item><c>success = false</c> → <c>error</c> BẮT BUỘC, có <c>code</c> thuộc
    ///         <see cref="AllCodes"/>, <c>message</c> không rỗng và ≤
    ///         <see cref="MaxErrorMessageLength"/> ký tự.</item>
    /// </list>
    /// </remarks>
    /// <param name="response">Response parse được từ AI1.</param>
    /// <param name="error">
    /// Khi contract vi phạm, trả về lỗi đã chuẩn hoá (code = <see cref="CodeInvalidResponse"/>).
    /// </param>
    /// <returns><c>true</c> nếu response hợp lệ.</returns>
    public static bool TryValidate(SpeechToTextResponse? response, out SttError? error)
    {
        error = null;

        if (response == null)
        {
            error = Invalid("STT returned an empty response.");
            return false;
        }

        if (!response.Success)
        {
            // 1. Error bắt buộc khi fail.
            if (response.Error == null)
            {
                error = Invalid("STT failure response is missing the error object.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(response.Error.Code))
            {
                error = Invalid("STT failure response is missing the error code.");
                return false;
            }

            if (response.Error.Code.Trim().Length > MaxErrorCodeLength)
            {
                error = Invalid($"STT error code exceeds {MaxErrorCodeLength} characters.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(response.Error.Message))
            {
                error = Invalid("STT failure response is missing the error message.");
                return false;
            }
            if (response.Error.Message.Length > MaxErrorMessageLength)
            {
                error = Invalid($"STT error message exceeds {MaxErrorMessageLength} characters.");
                return false;
            }

            // 2. Flag retryable phải khớp với taxonomy để caller không phải tự suy luận.
            //    Với code KHÔNG nhận diện được: contract chấp nhận forward-compatibility —
            //    AI1 có thể deploy code mới mà Backend chưa biết. Code lạ được coi là
            //    retryable (an toàn: thử lại 1 lần rẻ hơn bỏ luôn bài sinh viên), nhưng
            //    KHÔNG được để lọt nếu AI1 tự khai retryable=false cho một lỗi tạm thời
            //    mà mình chưa phân loại được → vẫn phải chờ Backend cập nhật taxonomy.
            var normalizedCode = response.Error.Code.Trim().ToUpperInvariant();
            var expectedRetryable = IsKnownCode(normalizedCode)
                ? IsRetryableCode(normalizedCode)
                : true;

            if (response.Error.Retryable != expectedRetryable)
            {
                error = Invalid(
                    $"STT error '{response.Error.Code}' declares retryable={response.Error.Retryable} " +
                    $"but the contract requires retryable={expectedRetryable}.");
                return false;
            }

            return true;
        }

        // 3. Success path — validate metadata.
        if (response.Transcript is null)
        {
            error = Invalid("STT success response is missing the transcript.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(response.LanguageDetected))
        {
            error = Invalid("STT success response is missing the detected language.");
            return false;
        }

        if (response.LanguageDetected.Length > MaxLanguageLength)
        {
            error = Invalid($"STT language exceeds {MaxLanguageLength} characters.");
            return false;
        }

        if (!IsSupportedLanguage(response.LanguageDetected))
        {
            error = Invalid($"STT returned an unsupported language code '{response.LanguageDetected}'.");
            return false;
        }

        if (!double.IsFinite(response.Duration) || response.Duration < 0)
        {
            error = Invalid("STT duration must be a finite non-negative number.");
            return false;
        }

        if (response.Confidence.HasValue
            && (!float.IsFinite(response.Confidence.Value)
                || response.Confidence.Value < 0f
                || response.Confidence.Value > 1f))
        {
            error = Invalid("STT confidence must be within [0, 1].");
            return false;
        }

        // 4. Success=true nhưng transcript rỗng → audio im lặng. KHÔNG phải contract violation,
        //    nhưng phải là lỗi nghiệp vụ (EmptyTranscript, không retry) — xem
        //    <see cref="Aives.Infrastructure.Queue.AnswerOrchestrator"/>.
        return true;
    }

    /// <summary>
    /// Validate response và ném exception nếu vi phạm contract.
    /// </summary>
    /// <exception cref="InvalidDataException">Response không hợp lệ.</exception>
    public static void Validate(SpeechToTextResponse? response)
    {
        if (!TryValidate(response, out var error))
            throw new InvalidDataException(error?.Message ?? "Invalid STT response.");
    }

    /// <summary>
    /// Ngôn ngữ có hợp lệ theo ISO-639-1 (2 chữ cái) hoặc BCP-47 dạng <c>vi-VN</c> không?
    /// </summary>
    /// <remarks>
    /// Không phân biệt hoa/thường — ISO-639-1 quy ưu lowercase nhưng <c>VI</c> và <c>vi</c>
    /// là cùng một ngôn ngữ, và AI1 có thể trả bất kỳ biến thể nào.
    /// </remarks>
    public static bool IsSupportedLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return false;

        var tag = language.Trim();
        var separator = tag.IndexOfAny(new[] { '-', '_' });

        // "vi" hoặc "vi-VN" / "vi_VN" → phần đầu là ISO-639-1 (2 chữ cái).
        var primary = separator > 0 ? tag[..separator] : tag;
        if (primary.Length != 2)
            return false;

        foreach (var ch in primary)
        {
            if (!char.IsAsciiLetter(ch))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Chuẩn hoá mã ngôn ngữ về dạng ISO-639-1 lowercase (vd: <c>VI-vn</c> → <c>vi</c>).
    /// Trả về <see cref="DefaultLanguage"/> nếu đầu vào không hợp lệ.
    /// </summary>
    public static string NormalizeLanguage(string? language)
    {
        if (!IsSupportedLanguage(language))
            return DefaultLanguage;

        var tag = language!.Trim();
        var separator = tag.IndexOfAny(new[] { '-', '_' });
        var primary = separator > 0 ? tag[..separator] : tag;
        return primary.ToLowerInvariant();
    }

    private static SttError Invalid(string message) => new()
    {
        Code = SttErrorCode.InternalError,
        Message = message.Length > MaxErrorMessageLength ? message[..MaxErrorMessageLength] : message,
        // Contract violation = AI trả sai → retry cũng ra kết quả y hệt → KHÔNG retry.
        Retryable = false
    };
}
