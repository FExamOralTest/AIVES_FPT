# Thi lại do sự cố — AIVES

Triển khai ngày 29/10/2026. Chức năng cấp phép thi lại cho từng sinh viên bằng một ca thi bù riêng. Ứng dụng không tự chạy migration lúc startup. Migration đã được áp dụng riêng lên Neon ngày 29/10/2026 theo yêu cầu của người dùng; xem biên bản triển khai bên dưới.

## Cách dùng

1. EXAMINER mở **Review & publish results**, chọn bài của sinh viên. Bài đang làm cũng có thể được tìm theo Session ID vì dự án tạo Result ngay khi sinh viên bắt đầu.
2. Trong **Thi lại do sự cố**, chọn **Cho phép thi lại**; nhập lý do, thời gian được vào, hạn kết thúc và thời lượng (1–240 phút). Giao diện dùng lại đề hiện tại. Xác nhận rõ việc hủy hiệu lực bài cũ.
3. Backend tạo một ExamSession riêng, chỉ có sinh viên đó; hủy hiệu lực Result và enrollment cũ. Audio, text, transcript, điểm AI, điểm/nhận xét examiner của bài cũ giữ nguyên; ca gốc và sinh viên khác không đổi.
4. Sinh viên mở **My sessions** hoặc danh sách trong Viva room, Refresh nếu đang mở trang. Thấy thông báo được phép thi lại, lý do và thời lượng. Ca chỉ vào được trong khung giờ cấp phép, không cần STAFF mở riêng.
5. Bấm Start exam tạo một Result mới. Tải lại trang tiếp tục Result đó, không tạo thêm lượt. Hạn thật là min(thời điểm bắt đầu + thời lượng, giờ kết thúc được phép), được trả qua `endsAt` và kiểm tra ở backend. Vào muộn có thể bị rút ngắn thời gian; form cấp phép có giải thích.
6. Nộp bài thi bù chạy qua finish/chấm AI hiện có. Nếu AI chưa chấm được, examiner vẫn có thể chấm thủ công rồi công bố. Không cho review/publish lượt thi bù chưa nộp.
7. Examiner xem **Lịch sử thi bù**, liên kết tới ca mới, duyệt và công bố bài hợp lệ. Bài cũ CANCELLED chỉ còn dữ liệu lịch sử, không thể review/publish, nhận audio, retry AI hoặc bị worker cập nhật đến muộn.
8. Quyền chưa sử dụng đã hết hạn có thể dùng **Điều chỉnh lịch chưa sử dụng**. Mỗi lần đổi lịch có audit trước/sau và lý do; không đổi lịch một lượt đã bắt đầu. Nếu lượt thi bù mới lại gặp sự cố, có thể cấp phép từ Result mới, tạo chuỗi lịch sử; không ghi đè các lượt trước.

Đây là cấp quyền làm bài mới, khác với retry chấm AI trên câu trả lời đã có. Lỗi AI đơn thuần không bắt sinh viên làm lại bài.

## Quyền và giới hạn

- Endpoint mới yêu cầu JWT hiện có, role EXAMINER hoặc ADMIN; service kiểm tra lại role trong database.
- Phạm vi EXAMINER theo quyền review hiện tại của ResultService (review toàn hệ thống). Dự án chưa có bảng phân công examiner theo ca/lớp. Không tự mở quyền tạo/sửa ca thi thông thường của STAFF.
- Bài PUBLISHED bị từ chối ngay cả với ADMIN. Điều chỉnh kết quả đã công bố cần một workflow khác.
- Một original Result có tối đa một ExamRetake; POST lặp trả cùng quyền. Ca bù có đúng một enrollment, không join bằng code hoặc bulk-enroll thêm người qua API ca thi thường.
- Bài chưa từng bắt đầu không được cấp thi lại; dùng enrollment bình thường. Bài đã hủy mà không có quyền thi lại không được mở lại.
- Giao diện examiner triển khai trong trang Results; ADMIN sử dụng cùng API qua quyền hiện có (không thêm menu Results vào portal ADMIN).
- Không tự gửi email/thông báo ngoài hệ thống; thông báo được hiển thị trong danh sách ca thi của sinh viên.

## API mới

| API | Quyền | Hành vi |
|---|---|---|
| POST /api/results/{resultId}/retake | EXAMINER, ADMIN | Cấp phép; request lặp không tạo ca thứ hai |
| GET /api/results/{resultId}/retake-history | EXAMINER, ADMIN | Lấy toàn bộ chuỗi thi bù từ một Result bất kỳ trong chuỗi |
| PUT /api/retakes/{retakeId}/schedule | EXAMINER, ADMIN | Đổi lịch/thời lượng quyền chưa sử dụng, ghi audit |

Request POST/PUT:

```json
{
  "reason": "Lỗi upload audio ảnh hưởng đến bài làm",
  "startTime": "2026-10-29T07:00:00Z",
  "endTime": "2026-10-29T08:00:00Z",
  "durationMinutes": 30
}
```

Timestamp là UTC có hậu tố Z; ví dụ trên tương ứng 14:00–15:00 tại Việt Nam. Khi test phải dùng thời điểm phù hợp hiện tại. `examId` optional trên POST: bỏ trống dùng lại đề cũ; nếu có phải là đề active, có câu hỏi, cùng môn, không bị CANCELLED; đề khác ở trạng thái DRAFT bị từ chối. PUT không đổi đề. Giao diện hiện dùng lại đề cũ.

Response có `retakeId`, `originalResultId`, `retakeSessionId`, `retakeResultId`, student, người cấp phép, lý do, khung giờ, thời lượng, `consumedAt`, trạng thái lượt thi và điểm cuối nếu có. Trạng thái quyền được suy ra: GRANTED, IN_PROGRESS, COMPLETED, EXPIRED, INVALIDATED; không cần background task chỉ để hết hạn.

401 chưa đăng nhập; 403 không có quyền; 404 thiếu result/retake/exam; 400 dữ liệu lịch/lý do sai; 409 published/đã sử dụng/xung đột hoặc trạng thái không hợp lệ.

API cũ được bổ sung:

- GET /api/student/enrollments: `retakeId`, `originalResultId`, `retakeReason`, `retakeDurationMinutes`, `retakeEndsAt`, `replacementSessionId` nullable. Chỉ enrollment của người gọi được trả.
- POST /api/student/viva-sessions/{sessionId}/start: `durationMinutes` đúng quyền thi bù và `endsAt` từ server.
- Submit/get current question/finish chặn Result bị hủy. Sau hết giờ không nhận thêm answer mới, nhưng finish được phép nộp các câu đã lưu.

## Persistence và chống race

Entity `ExamRetake` liên kết Result gốc, ca mới, student, người cấp phép, lý do, ngày cấp, thời lượng, consumed time, revision. Unique indexes trên original_result_id và retake_session_id; foreign keys không cascade xóa lịch sử.

Grant/reschedule/start chạy trong transaction SERIALIZABLE với PostgreSQL. Result.Status, SessionEnrollment.Status/StartedAt và ExamRetake.Revision/ConsumedAt là optimistic concurrency guards. Service refresh entity trong transaction để không dùng lịch cũ đã được load trước đó. Conflict trả về 409 để client refresh.

Worker/AnswerRepository kiểm tra lại trạng thái Result ở thời điểm ghi và dùng Result.Status concurrency guard cùng SaveChanges. Finish kiểm tra lại hủy hiệu lực sau lời gọi AI. Không xóa job đang chạy; kết quả đến muộn bị từ chối ghi. Audit gồm RETAKE_GRANTED, RETAKE_STARTED, RETAKE_RESCHEDULED; ghi actor/thời gian/lý do và dữ liệu cần đối soát.

SubmittedAt được gửi null rõ ràng khi tạo lượt thi bù, chỉ ghi lúc finish; tránh default timestamp trong database làm bài vừa bắt đầu trông như đã nộp.

## Migration bắt buộc trước khi chạy bản backend mới

Script: `backend/Aives.Infrastructure/Persistence/Migrations/20261029_add_exam_retakes.sql`.

Repo checkout hiện không có EF migration history/model snapshot mặc dù tài liệu cũ nhắc tới; vì vậy bản này cung cấp migration SQL có phiên bản, chỉ thêm một bảng. Không tạo migration baseline để tránh lệnh tạo lại toàn bộ schema hiện có. EF model được cập nhật tương ứng. Script chạy trong transaction, không xóa/cập nhật các bảng hiện có; không tự chạy lúc startup.

1. Backup/chụp snapshot, chọn một database/Neon branch development cô lập và connection trực tiếp.
2. Cấu hình PGHOST, PGDATABASE, PGUSER, PGPASSWORD cục bộ cho đúng database đó; không đưa secret vào Git hoặc log.
3. Người có quyền triển khai chạy từ repo root:

```powershell
psql --set=ON_ERROR_STOP=1 --file=backend/Aives.Infrastructure/Persistence/Migrations/20261029_add_exam_retakes.sql
```

4. Kiểm tra bảng/constraints rồi restart backend và frontend cùng phiên bản. Cần áp dụng bảng trước vì các API enrollment sẽ query bảng mới.
5. Không DROP bảng rollback khi đã có quyền/lịch sử thi lại; cần kế hoạch bảo toàn dữ liệu riêng.

**Cập nhật 29/10/2026:** Đã chạy thử migration trong schema tạm trên Neon rồi rollback, áp dụng thành công vào `public.exam_retakes` của database `neondb` đang cấu hình trong dự án, và xác minh bằng kết nối mới. Đã kiểm tra cấu trúc, FK/unique/index và thử ràng buộc duration/reason. Bảng mới có 0 dòng; không UPDATE/DELETE dữ liệu bài thi cũ. Xem [biên bản triển khai](deployment/exam-retakes-migration-2026-10-29.md). Các test nghiệp vụ/concurrency ứng dụng trước đó vẫn là InMemory; chưa chạy đầy đủ luồng tạo ca thi bù trên dữ liệu Neon thật.

## Kiểm thử thực thi

- Backend nội bộ: 264 passed, 1 skipped; loại CloudflareR2IntegrationTests và AudioPipelineE2ETests vốn phụ thuộc storage/cấu hình ngoài. Không tuyên bố full suite green.
- Trong số trên: 22 test mới thuộc RetakeServiceTests/RetakeHttpTests, gồm role, published, bảo toàn bài cũ/sinh viên khác, duplicate, private enrollment, expiry, reload, reschedule, race, invalidation, late worker, finish/review/publish và HTTP 401/403/409.
- Frontend: 7 test pass / 2 files cho form cấp phép, chống gửi trùng, lỗi server, published, lịch chưa dùng/đã dùng, thông báo sinh viên và hết hạn riêng dù ca còn mở.
- TypeScript/Vite build và lint pass; còn cảnh báo bundle >500 kB.
- npm install báo 11 vulnerability trong dependency tree (1 low, 3 moderate, 7 high); không chạy audit fix ngoài phạm vi tính năng.
- Không gọi provider AI trả phí; test luồng finish mô phỏng AI unavailable rồi examiner chấm/publish thủ công. Không dùng kết quả này để khẳng định provider AI khỏe.
- git diff --check được kiểm tra sau sửa; không commit/push/deploy, không tự dừng API đang chạy.

Chạy lại từ repo root:

```powershell
dotnet test backend/Aives.Tests/Aives.Tests.csproj --no-restore --output backend/Aives.Tests/obj/retake-check --filter 'FullyQualifiedName!~CloudflareR2IntegrationTests&FullyQualifiedName!~AudioPipelineE2ETests' -m:1
```

Chỉ test tính năng/HTTP smoke cô lập:

```powershell
dotnet test backend/Aives.Tests/Aives.Tests.csproj --no-restore --output backend/Aives.Tests/obj/retake-check --filter 'FullyQualifiedName~Retake' -m:1
```

Frontend:

```powershell
Set-Location frontend/aives-web
npm test
npm run build
npm run lint
```

## Các phần chính trong code

- Domain/DTO/interface: ExamRetake.cs, RetakeDtos.cs, IRetakeService.cs.
- Backend nghiệp vụ: Infrastructure/Exams/RetakeService.cs và Api/Controllers/RetakesController.cs.
- Kết nối luồng thi: VivaSessionService, ResultService, ExamSessionRepository và các guard AnswerRepository/worker/retry/audio.
- UI: RetakePanel trong ResultReviewModal; StudentSessionsPage, VivaSessionPicker, VivaRoomPage dùng hạn server và thông báo nộp bài thực tế.

## Biên bản áp dụng migration

Ngày 29/10/2026, người dùng yêu cầu áp dụng migration lên Neon. Đã hoàn tất qua Npgsql 8.0.6 với kết nối trực tiếp và TLS VerifyFull; không lưu credentials vào source/log. Script SHA-256: `766AC9B53F71F7F3642A60D0D993916514365A3E9ECFC8C6FBD1702AD54FE4E5`. Các hướng dẫn chạy migration ở trên dành cho database khác chưa có bảng; không chạy lại CREATE TABLE trên database này.
