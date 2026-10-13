# Neon PostgreSQL Database Architecture & Guidelines

## 1. Tổng quan Kiến trúc (Monorepo + Multi-Service)

Dự án AIVES sử dụng **Neon Serverless PostgreSQL** làm cơ sở dữ liệu quan hệ trung tâm.

```text
               +---------------------------------------+
               |        Neon Project (Cloud DB)        |
               |       Project: soft-hall-04238342     |
               |       Branch: production (default)    |
               +-------------------+-------------------+
                                   |
         +-------------------------+-------------------------+
         |                                                   |
         v (Pooled Connection: PgBouncer)                    v (Direct / Unpooled Connection)
  [ASP.NET Core Web API]                             [EF Core Migrations]
  - Runtime queries & transactions                   - `dotnet ef database update`
  - Connection pooling qua PgBouncer                 - Yêu cầu direct connection (DDL)
         |
         v
  [AI Service (FastAPI)] (Optional direct / vector access)
```

---

## 2. Chiến lược thiết lập (Root Workspace vs Backend)

### Tại sao quản lý Neon CLI ở Root Workspace (Tổng bên ngoài)?
1. **Quản lý tài nguyên hạ tầng tập trung (Infrastructure as Code)**:
   - File `neon.ts` và `.neon` context đại diện cho toàn bộ database project của hệ thống.
   - Quản lý phân nhánh (Branching) cho preview environments / CI-CD: Khi tạo một nhánh Git mới, có thể chạy `neon branches create` ngay tại root để tạo DB branch riêng biệt cho toàn bộ cụm service (Backend + AI Service + Frontend).
2. **AI Coding Agent Tooling (Skills & MCP)**:
   - Các công cụ như **Neon Agent Skills** (`.agents/skills/`) và **Neon MCP Server** cần được nhận diện ở cấp độ Workspace Root để Antigravity / Cursor / Claude Code có thể trực tiếp truy vấn schema, tạo test branch và inspect dữ liệu khi dev ở bất cứ service nào.
3. **Quản lý biến môi trường tập trung**:
   - `neon link` tự động sinh các biến `DATABASE_URL`, `DATABASE_URL_UNPOOLED`, `NEON_BRANCH` vào `.env.local` ở root, làm nguồn dữ liệu chuẩn cho `docker-compose.yml` và các service con.

---

## 3. Cấu hình kết nối cho từng Service

### 3.1. Backend (ASP.NET Core / Entity Framework Core)

> [!IMPORTANT]
> **Quy tắc vàng về Neon Pooling đối với .NET**:
> - **Runtime (`DefaultConnection`)**: Sử dụng **Pooled connection** (`-pooler` domain) để tận dụng PgBouncer quản lý connection linh hoạt, tối ưu chi phí serverless.
> - **Migrations (`MigrationConnection`)**: Bắt buộc sử dụng **Direct / Unpooled connection** vì PgBouncer ở chế độ transaction không hỗ trợ một số prepared statements và DDL locking của EF Core Migrations.

#### Định dạng Connection String (.NET / Npgsql):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<ep-pooler-domain>;Database=neondb;Username=<user>;Password=<pass>;SSL Mode=Require;Trust Server Certificate=true;",
    "MigrationConnection": "Host=<ep-direct-domain>;Database=neondb;Username=<user>;Password=<pass>;SSL Mode=Require;Trust Server Certificate=true;"
  }
}
```

### 3.2. AI Service (Python FastAPI)
Nếu AI Service cần truy cập trực tiếp Postgres (ví dụ `pgvector` cho vector search tài liệu thi):
- Sử dụng thư viện `asyncpg` hoặc `SQLAlchemy` (async).
- Đọc `DATABASE_URL` từ file `.env` hoặc truyền qua Docker environment.

### 3.3. Frontend (React / Vite)
- Frontend **tuyệt đối không** kết nối trực tiếp đến Neon.
- Tất cả truy vấn dữ liệu được định tuyến thông qua Backend REST API.

---

## 4. Các lệnh Neon CLI thường dùng

- **Kiểm tra trạng thái liên kết**:
  ```bash
  neon status
  ```
- **Tạo branch database mới để test tính năng**:
  ```bash
  neon branch create feat-exam-session
  ```
- **Chuyển branch làm việc hiện tại**:
  ```bash
  neon checkout <branch-name>
  ```
- **Lấy Connection String nhanh**:
  ```bash
  neon connection-string
  neon connection-string --unpooled
  ```
- **Đồng bộ cấu hình `neon.ts`**:
  ```bash
  neon deploy
  ```
