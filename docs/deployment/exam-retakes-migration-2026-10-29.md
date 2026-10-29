# Biên bản migration exam_retakes — 29/10/2026

Yêu cầu: người dùng cho phép áp dụng migration thêm bảng exam_retakes trên Neon của dự án AIVES.

- Database: `neondb`, schema `public`, endpoint Neon đang cấu hình tại appsettings.Development.json.
- Script: `backend/Aives.Infrastructure/Persistence/Migrations/20261029_add_exam_retakes.sql`.
- SHA-256: `766AC9B53F71F7F3642A60D0D993916514365A3E9ECFC8C6FBD1702AD54FE4E5`.
- Driver: Npgsql 8.0.6 hiện có của dự án; dùng endpoint trực tiếp, TLS VerifyFull. Credentials chỉ được đọc trong tiến trình, không in ra hoặc lưu vào file mới.

## Kết quả

1. Kiểm tra trước chạy: đúng database `neondb`; bảng đích chưa tồn tại. Các khóa của results, exam_sessions và users đều là integer phù hợp với migration.
2. Tạo thử trong schema tạm với search_path trỏ tới schema đó/public; giữ nguyên nội dung DDL của script, chạy trong transaction do runner quản lý.
3. Kiểm tra đủ 10 cột, identity GENERATED ALWAYS, kiểu/nullability, 4 khóa ngoại ON DELETE RESTRICT, 2 unique, primary key, 2 check constraints và 4 index.
4. Thử input duration=0 và reason rỗng: PostgreSQL từ chối với check violation 23514 đúng constraint. Mỗi probe dùng savepoint; sau đó rollback toàn bộ schema thử. Đã xác minh schema thử không còn tồn tại.
5. Áp dụng DDL vào `public` trong transaction, có advisory lock cùng lock/statement timeout. Kết quả `committed=true`, `created=true`, `alreadyPresent=false`.
6. Mở kết nối mới để xác minh lại cấu trúc sau commit. `SELECT count(*) FROM public.exam_retakes` trả **0**.

Không INSERT/UPDATE/DELETE vào bảng bài thi, sinh viên, kết quả hoặc enrollment hiện có. Chưa cấp một quyền thi lại thật nào. Không thay đổi JWT, quyền truy cập ứng dụng hoặc cấu hình provider AI.

Lưu ý phạm vi xác minh: migration và cấu trúc/ràng buộc đã được kiểm tra trên PostgreSQL Neon thật. Các test toàn bộ workflow thi lại và race của ứng dụng trước đó dùng InMemory; chưa chạy tạo ca thi bù thật trên database này. Không coi kiểm tra DDL là xác minh toàn bộ workflow production.

Nếu ứng dụng còn chạy binary cũ, restart backend/frontend cùng phiên bản có tính năng thi lại trước khi sử dụng. Không chạy lại nguyên script CREATE TABLE trên database đã áp dụng; không DROP bảng để làm lại migration.
