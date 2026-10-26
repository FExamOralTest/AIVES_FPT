# AIVES Queue Job Contract (1.4) & STT Response Contract (1.5)

**Version:** 1.0
**Last Updated:** 2026-10-25
**Owner:** Thịnh (BE3)

Tài liệu này chốt 2 hợp đồng giữa các thành phần trong pipeline AI:

| Task | Hợp đồng | Bên sở hữu | Bên tiêu thụ |
|---|---|---|---|
| **1.4** | Queue Job Contract | Backend (Hangfire) | Backend (worker), Hangfire dashboard |
| **1.5** | STT Response Contract | AI1 (Faster-Whisper) | Backend (`AiServiceClient`, `AnswerOrchestrator`) |

**Nguồn sự thật trong code:**

| Hợp đồng | File |
|---|---|
| Queue Job Contract | `backend/Aives.Application/Queue/Contracts/QueueJobContract.cs` |
| Queue metadata | `backend/Aives.Application/Queue/Contracts/QueueJobMetadata.cs` |
| Queue limits | `backend/Aives.Application/Queue/Contracts/QueueLimits.cs` |
| STT Response Contract | `backend/Aives.Application/Queue/Contracts/Stt/SttContract.cs` |
| STT error taxonomy | `backend/Aives.Application/Queue/Contracts/Stt/SttErrorCode.cs` |

---

## 1. Queue Job Contract (Task 1.4)

### 1.1. Metadata chuẩn

`QueueJobMetadata` là payload được serialize vào Hangfire storage. Nó **không** chứa entity EF.

| Field | Kiểu | Bắt buộc | Ràng buộc | Mô tả |
|---|---|---|---|---|
| `JobId` | `string` | ✅ (sau enqueue) | ≤ 128 ký tự | Hangfire JobId. Gán tự động sau khi enqueue; lưu thêm vào `answers.job_id` |
| `AnswerId` | `int` | ✅ | > 0 | PK của `Answer` đang xử lý |
| `ResultId` | `int?` | — | > 0 nếu có | PK của `Result`. `null` khi chưa tạo row result |
| `ObjectKey` | `string` | — | ≤ 512 ký tự | Key audio trong R2. Rỗng nếu answer chỉ có text |
| `Attempt` | `int` | ✅ | ≥ 1 (1-based) | Lần thử hiện tại — **chỉ để log/audit**, không phải nguồn sự thật |
| `Status` | `QueueJobStatus` | ✅ | phải là giá trị hợp lệ | Trạng thái vòng đời job |
| `EnqueuedAt` | `DateTime` | ✅ | UTC | Thời điểm enqueue |
| `LastError` | `string?` | — | ≤ 500 ký tự | Message ngắn của lần fail gần nhất (không phải stack trace) |

### 1.2. Nguồn sự thật của "lần thử thứ mấy"

> **Bắt buộc:** `answers.attempt_count` trong database. **KHÔNG** phải `metadata.Attempt`.

Lý do: Hangfire serialize lại toàn bộ metadata ở mỗi lần retry, nên `metadata.Attempt` **không tự tăng**. Job thứ 3 vẫn mang `Attempt = 1` trong payload nếu job đầu enqueue với `Attempt = 1`.

```csharp
// Nguồn sự thật — ghi trong DB mỗi lần job chạy
var attempt = await _answerRepository.IncrementAttemptAsync(answerId, jobId, ct);
```

`metadata.Attempt` được đồng bộ lại từ DB sau khi increment (`metadata.Attempt = attempt`) chỉ để log và hiển thị trên dashboard.

### 1.3. Ngân sách attempt (BR-17)

| Hằng số | Giá trị | Ý nghĩa |
|---|---|---|
| `QueueLimits.MaxTotalAttempts` | `3` | Tổng số lần thử tối đa (attempt đầu + 2 lần retry) |
| `QueueLimits.RetryDelay1Seconds` | `30` | Backoff sau attempt 1 |
| `QueueLimits.RetryDelay2Seconds` | `120` | Backoff sau attempt 2 |
| `QueueLimits.RetryDelay3Seconds` | `300` | Backoff sau attempt 3 |

`QueueLimits.IsExhausted(attemptCount)` → `true` khi `attemptCount >= 3`.

**Quy tắc reset:**
- `ResetForRetryAsync` (retry thủ công từ UI) → **giữ nguyên** `AttemptCount`. Retry thủ công phải nằm trong cùng ngân sách 3 lần thử.
- `UpdateAudioUrlAsync` (upload audio mới) → **reset** `AttemptCount = 0`. Nội dung audio đã thay đổi hoàn toàn → vòng xử lý mới.

### 1.4. State machine

```
                  enqueue
                     │
                     ▼
                 ┌────────┐
                 │Pending │◄── Enqueued / Scheduled / Created (Hangfire)
                 └────┬───┘
                      │ worker nhận job
                      ▼
              ┌─────────────┐
              │ Processing  │◄── Processing (Hangfire)
              └──┬───────┬──┘
                 │       │
      success    │       │  failure
                 ▼       ▼
          ┌──────────┐  ┌────────┐
          │Completed │  │ Failed │◄── Failed (Hangfire) — còn retry được
          └──────────┘  └───┬────┘
                             │ hết ngân sách attempt
                             │ hoặc lỗi không retryable
                             ▼
                     ┌────────────┐
                     │ DeadLetter │◄── Deleted (Hangfire)
                     └────────────┘
```

**Mapping trạng thái Hangfire → `QueueJobStatus`:**

| Hangfire state | `QueueJobStatus` |
|---|---|
| `Enqueued`, `Scheduled`, `Created`, `Waiting` | `Pending` |
| `Processing` | `Processing` |
| `Succeeded` | `Completed` |
| `Failed` | `Failed` |
| `Deleted` | `DeadLetter` |
| (không xác định) | `Pending` (fallback an toàn) |

**Quyết định retry:**

| Điều kiện | Kết quả |
|---|---|
| `Status == Failed` && `attemptCount < 3` | Retry (throw `RetryableJobException`) |
| `Status == Failed` && `attemptCount >= 3` | Dead-letter (throw `NonRetryableJobException`) |
| Lỗi không retryable (audio hỏng, transcript rỗng, lỗi kinh doán) | Dead-letter ngay, **không** retry |

### 1.5. Validate

`QueueJobContract.TryValidate(metadata, out error)` kiểm tra trước khi enqueue. Được áp dụng ở **3 điểm**:

| Điểm | Hành vi khi vi phạm |
|---|---|
| `HangfireQueueService.EnqueueAnswerProcessing` | Throw `ArgumentException` — job **không** được tạo |
| `AnswerProcessingJob.ExecuteAsync` | `DeadLetter` + throw `NonRetryableJobException` — pipeline **không** chạy |
| `QueueJobContract.Create` | Throw `ArgumentException` — chặn lỗi từ call site |

Metadata vi phạm = job vô nghĩa, chỉ đốt compute và gây nhiễu dashboard.

### 1.6. Dedup

`IQueueService.HasPendingOrProcessingJob(answerId)` query Hangfire `ProcessingJobs` + `ScheduledJobs` + `EnqueuedJobs` (cả queue `default` và `answers`) và match theo `AnswerId` trong chuỗi args.

> ⚠️ Match theo ranh giới số: `AnswerId = 77` **không** được khớp `AnswerId = 777`.

### 1.7. Ràng buộc bắt buộc cho Hangfire

Job **phải** được enqueue qua **concrete class** `AnswerProcessingJob`, **không** qua interface `IAnswerProcessingJob`:

```csharp
// ✅ Đúng
Expression<Func<AnswerProcessingJob, Task>> method = j => j.ExecuteAsync(metadata, default);
var jobId = _jobClient.Enqueue(method);

// ❌ Sai — attribute [AutomaticRetry] trên class sẽ KHÔNG được áp dụng
Expression<Action<IAnswerProcessingJob>> method = j => j.ExecuteAsync(metadata, default);
```

Hangfire đọc job filter từ `Job.Method.DeclaringType`. Nếu expression khai báo trên interface, `DeclaringType` là interface → attribute job-level bị bỏ qua → Hangfire fallback về policy mặc định (`Attempts = 10`, retry **mọi** exception) → vi phạm BR-17.

Dùng `Expression<Func<T, Task>>` (không phải `Action<T>`) vì `ExecuteAsync` trả về `Task` — `Action<T>` bỏ qua `Task` và sinh cảnh báo CS4014 (fire-and-forget).

---

## 2. STT Response Contract (Task 1.5)

### 2.1. Endpoint

```
POST /ai/speech/transcribe-object
Header: X-Internal-Token: <AI_SERVICE_SECRET_KEY>
Content-Type: application/json
```

**Request**

| Field | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `answerId` | `int` | ✅ | ID câu trả lời |
| `objectKey` | `string` | ✅ | Key object trong bucket R2 |

### 2.2. Response thành công

```json
{
  "success": true,
  "message": null,
  "answerId": 42,
  "transcript": "Tôi trả lời về kiến trúc phần mềm",
  "language": "vi",
  "duration": 12.34,
  "error": null
}
```

| Field | Kiểu | Ràng buộc | Ghi chú |
|---|---|---|---|
| `success` | `bool` | — | `true` |
| `transcript` | `string` | không null | Rỗng = audio im lặng (hợp lệ về contract, xem 2.5) |
| `language` | `string` | ISO-639-1 (2 chữ cái), ≤ 20 ký tự | Chuẩn hoá về `vi` / `en`… |
| `duration` | `number` | hữu hạn, ≥ 0 | Giây |
| `error` | `object?` | `null` | Chỉ set khi `success = false` |

### 2.3. Response thất bại

```json
{
  "success": false,
  "message": "Audio file is corrupted",
  "answerId": 42,
  "transcript": "",
  "language": "",
  "duration": 0,
  "error": {
    "code": "AUDIO_CORRUPTED",
    "message": "Audio file is corrupted",
    "retryable": false
  }
}
```

**`error` BẮT BUỘC** khi `success = false`:

| Field | Kiểu | Ràng buộc |
|---|---|---|
| `code` | `string` | ≤ 64 ký tự, phải thuộc taxonomy (xem 2.4) |
| `message` | `string` | Không rỗng, ≤ 500 ký tự |
| `retryable` | `bool` | Phải khớp với taxonomy |

### 2.4. Taxonomy error code

| Code | HTTP tương ứng | `SttErrorCode` | Retry? | Nghĩa |
|---|---|---|---|---|
| `AUDIO_CORRUPTED` | 400, 404 | `AudioCorrupted` | ❌ | File hỏng / không decode được |
| `EMPTY_AUDIO` | 400 | `AudioCorrupted` | ❌ | File 0 byte (alias) |
| `EMPTY_TRANSCRIPT` | 422 | `EmptyTranscript` | ❌ | Whisper chạy xong, không có lời |
| `NO_SPEECH` | 422 | `EmptyTranscript` | ❌ | Audio không có tiếng nói (alias) |
| `UNSUPPORTED_FORMAT` | 413, 415 | `UnsupportedFormat` | ❌ | Định dạng / sample rate không hỗ trợ |
| `TIMEOUT` | 408, 504 | `Timeout` | ✅ | AI1 xử lý quá thời gian |
| `INTERNAL_ERROR` | 500, 503 | `InternalError` | ✅ | Lỗi nội bộ AI1 (GPU OOM, model load fail…) |
| `INVALID_RESPONSE` | 502 | `Unknown` | ❌ | AI1 trả sai contract |
| `AUTH_FAILED` | 401, 403 | `Unknown` | ❌ | Internal token sai |

**Mapping HTTP → code** dùng khi AI1 trả lỗi mà body không theo contract (proxy trả HTML, deploy cũ):

```csharp
SttContract.MapHttpStatusToCode(statusCode)
```

### 2.5. Retry decision

Retry chỉ hợp lý khi lỗi là **tạm thời**. Retry một lỗi kinh doán (audio hỏng, transcript rỗng) chỉ tốn thêm compute và vẫn ra cùng kết quả.

| Tình huống | Kết quả |
|---|---|
| `TIMEOUT`, `INTERNAL_ERROR` | Retry (tối đa 3 lần thử tổng cộng) |
| `AUDIO_CORRUPTED`, `EMPTY_AUDIO`, `EMPTY_TRANSCRIPT`, `NO_SPEECH`, `UNSUPPORTED_FORMAT` | Dead-letter ngay |
| `INVALID_RESPONSE`, `AUTH_FAILED` | Dead-letter ngay |
| Code lạ (chưa có trong taxonomy) | **Retry** — forward-compatibility, xem 2.6 |
| `success = true` nhưng `transcript` rỗng | `EmptyTranscript`, không retry — lỗi nghiệp vụ, **không** phải contract violation |
| Response vi phạm contract (thiếu field, sai kiểu, `duration` âm…) | `InternalError`, **không** retry — AI trả sai thì retry cũng ra kết quả y hệt |

### 2.6. Forward-compatibility

AI1 có thể deploy error code mới mà Backend chưa biết. Contract **không reject** code lại:

- Code lạ → mặc định `retryable = true` (thử lại 1 lần rẻ hơn bỏ luôn bài sinh viên).
- Nếu AI1 khai `retryable = false` cho code lạ → contract **reject**, vì Backend chưa phân loại được lỗi đó là tạm thời hay vĩnh viễn. Cần cập nhật taxonomy trước.
- Dùng `SttContract.IsKnownCode(code)` để phân biệt và log cảnh báo để bổ sung taxonomy.

### 2.7. Validate

`SttContract.TryValidate(response, out error)` kiểm tra response của AI1. Được áp dụng ở 2 tầng:

| Tầng | Hành vi khi vi phạm |
|---|---|
| `AiServiceClient.TranscribeStoredAudioAsync` | Trả `INVALID_RESPONSE` (không retry) |
| `AnswerOrchestrator.TranscribeFromR2Async` | Trả `SttError` `InternalError`, `retryable = false` |

**Quy tắc validate:**

*Success path:*
1. `transcript` không null
2. `language` không rỗng, ≤ 20 ký tự, là ISO-639-1 hợp lệ (`vi`, `en`, `vi-VN`, `vi_VN` — không phân biệt hoa/thường)
3. `duration` hữu hạn, ≥ 0
4. `confidence` (nếu có) ∈ [0, 1]

*Failure path:*
1. `error` phải tồn tại
2. `error.code` không rỗng, ≤ 64 ký tự
3. `error.message` không rỗng, ≤ 500 ký tự
4. `error.retryable` khớp với taxonomy

### 2.8. Chuẩn hoá ngôn ngữ

`SttContract.NormalizeLanguage(language)` thu gọn mọi biến thể về ISO-639-1 lowercase:

| Input | Output |
|---|---|
| `vi` | `vi` |
| `VI` | `vi` |
| `vi-VN` | `vi` |
| `en_US` | `en` |
| `vietnamese` | `vi` (fallback `SttContract.DefaultLanguage`) |
| `null` | `vi` (fallback) |

Giá trị đã chuẩn hoá được persist vào `answers.detected_language`.

---

## 3. Cập nhật taxonomy

Khi AI1 thêm error code mới:

1. Thêm hằng số vào `SttContract` (`CodeXxx`) và vào `AllCodes`.
2. Cập nhật `MapToErrorCode` nếu code cần map về taxonomy nội bộ khác `Unknown`.
3. Cập nhật `IsRetryableCode` — **bắt buộc**, vì contract reject khi cờ `retryable` của AI1 lệch với taxonomy.
4. Thêm test vào `SttContractTests.cs`.
5. Cập nhật bảng ở mục 2.4 của tài liệu này.

---

## 4. Test

| File | Phạm vi |
|---|---|
| `QueueJobContractTests.cs` | Validate metadata, factory, mapping Hangfire state, retry decision, budget BR-17 |
| `SttContractTests.cs` | Validate response, taxonomy mapping, HTTP mapping, retry decision, language helpers |
| `ContractEnforcementTests.cs` | Contract được **thực thi** trên đường chạy thật (enqueue path, execution path, STT path) |
| `HangfireQueueRetryTests.cs` | Retry pipeline thật qua Hangfire MemoryStorage |
| `SttIntegrationTests.cs` | STT error taxonomy end-to-end |
