# AIVES - Kiến trúc Hệ thống & Thiết kế Phần mềm (SWD392)

Tài liệu thiết kế kiến trúc chuẩn C4 (C0 System Context, C1 Container) và sơ đồ UML (Sequence, State Machine, Traceability Matrix) cho hệ thống **AIVES** (AI-powered Viva Examination System).

---

## 1. C4 Model - C0: System Context Diagram

Sơ đồ ngữ cảnh hệ thống định rõ ranh giới của AIVES và tương tác với các đối tượng người dùng (Actors) cũng như các hệ thống bên ngoài (External Services).

```mermaid
C4Context
    title C0 - System Context Diagram cho AIVES

    Person(student, "Sinh viên (Student)", "Tham gia ca thi vấn đáp trực tuyến, trả lời câu hỏi bằng giọng nói/văn bản và tra cứu kết quả.")
    Person(teacher, "Giảng viên (Teacher)", "Quản lý môn học, tải lên tài liệu .md, tạo đề thi, ngân hàng câu hỏi, duyệt và công bố điểm thi.")
    Person(admin, "Quản trị viên (Admin)", "Quản lý tài khoản người dùng, phân quyền hệ thống và quản trị danh mục môn học.")

    Enterprise_Boundary(aives_boundary, "Hệ sinh thái AIVES (AI-powered Viva Exam System)") {
        System(aives_system, "AIVES Platform", "Nền tảng thi vấn đáp AI: điều phối ca thi, sinh câu hỏi từ tài liệu chuẩn, thu âm và chấm điểm tự động.")
    }

    System_Ext(gemini_llm, "Google Gemini / OpenAI LLM", "Mô hình ngôn ngữ lớn: phân tích tài liệu chuẩn, sinh câu hỏi & rubric, chấm điểm câu trả lời và đề xuất follow-up.")
    System_Ext(whisper_stt, "Whisper STT Service", "Mô hình Speech-to-Text: nhận dạng giọng nói câu trả lời của thí sinh thành văn bản tiếng Việt/Anh.")
    System_Ext(edge_tts, "Edge-TTS Provider", "Mô hình Text-to-Speech: phát âm câu hỏi thi theo ngữ điệu tự nhiên.")

    Rel(admin, aives_system, "Quản lý người dùng & môn học", "HTTPS / JSON")
    Rel(teacher, aives_system, "Tải tài liệu, tạo đề, duyệt kết quả", "HTTPS / Multipart / JSON")
    Rel(student, aives_system, "Ghi danh ca thi, nộp bài, xem điểm", "HTTPS / Audio Streams / JSON")

    Rel(aives_system, gemini_llm, "Gửi prompt sinh câu hỏi & chấm điểm", "REST API / JSON")
    Rel(aives_system, whisper_stt, "Gửi file ghi âm để chuyển thành text", "HTTP / Multipart Audio")
    Rel(aives_system, edge_tts, "Tổng hợp âm thanh đọc câu hỏi", "REST API")
```

---

## 2. C4 Model - C1: Container Diagram

Sơ đồ Container thể hiện các thành phần phần mềm có thể deploy độc lập, trách nhiệm và giao thức giao tiếp giữa chúng.

```mermaid
C4Container
    title C1 - Container Diagram cho AIVES

    Person(user, "Người dùng (Admin/Teacher/Student)", "Truy cập hệ thống qua trình duyệt web")

    Container_Boundary(c1, "AIVES Core Architecture") {
        Container(spa, "Single Page Application (SPA)", "React 19, TypeScript, Vite, Tailwind CSS", "Giao diện người dùng: quản trị, ngân hàng câu hỏi, phòng thi Viva với bộ thu âm & nhận diện giọng nói.")
        
        Container(backend_api, "Backend Web API", "ASP.NET Core 8 (.NET 8), C#, EF Core", "Cung cấp RESTful API, kiểm soát JWT & RBAC, thực thi nghiệp vụ ca thi, chấm điểm, quản lý đề thi và tài liệu.")
        
        Container(ai_service, "AI Microservice", "Python 3.11+, FastAPI, LangChain / Direct LLM SDK", "Microservice xử lý chuyên biệt cho AI: sinh câu hỏi từ Markdown, chấm điểm viva câu hỏi & rubric, tích hợp STT/TTS.")
        
        ContainerDb(database, "Relational Database", "PostgreSQL 16", "Lưu trữ thông tin người dùng, môn học, tài liệu, câu hỏi, đề thi, ca thi, câu trả lời và kết quả chấm.")
        
        Container(file_storage, "Local Storage / Volume", "File System (wwwroot/uploads)", "Lưu trữ bền vững các tệp âm thanh câu trả lời (.webm/.wav) và tài liệu giảng dạy (.md).")
    }

    System_Ext(external_ai, "External AI Providers", "Google Gemini 1.5 Flash / OpenAI")

    Rel(user, spa, "Tương tác qua trình duyệt", "HTTPS")
    Rel(spa, backend_api, "Gọi API nghiệp vụ & xác thực", "JSON / Multipart HTTPS, JWT Bearer")
    Rel(backend_api, database, "Đọc / ghi dữ liệu", "EF Core / Npgsql TCP:5432")
    Rel(backend_api, file_storage, "Lưu file âm thanh & đọc tài liệu", "File I/O")
    Rel(backend_api, ai_service, "Yêu cầu sinh câu hỏi / chấm điểm", "HTTP / REST API TCP:8000")
    Rel(ai_service, external_ai, "Gọi inference", "HTTPS / REST API")
```

---

## 3. Sequence Diagram: Luồng Thi Vấn Đáp & Công Bố Điểm (Submit Answer -> Review -> Publish)

Quy trình khép kín từ lúc Sinh viên thi Viva, AI chấm điểm, Giảng viên phúc khảo/duyệt và công bố điểm cho Sinh viên.

```mermaid
sequenceDiagram
    autonumber
    actor Student as Sinh viên
    participant SPA as Web SPA (VivaRoomPage)
    participant API as Backend API (VivaSessionController)
    participant VivaService as VivaSessionService
    participant AISvc as AI Microservice (FastAPI)
    participant DB as PostgreSQL
    actor Teacher as Giảng viên
    participant ResultCtrl as ResultsController
    participant ResultSvc as ResultService

    %% Giai đoạn 1: Sinh viên nộp câu trả lời
    Note over Student, DB: Giai đoạn 1: Sinh viên trả lời câu hỏi Viva
    Student->>SPA: Kết thúc ghi âm & nhấn "Nộp câu trả lời"
    SPA->>API: POST /api/viva/sessions/{id}/submit-answer (Multipart: Audio + textAnswer)
    API->>VivaService: SubmitAnswerAsync(sessionId, studentId, request)
    VivaService->>DB: Lưu file audio vào wwwroot/uploads & tạo bản ghi Answer
    VivaService->>AISvc: POST /api/ai/evaluate-answer (question, rubric, studentAnswer)
    AISvc-->>VivaService: AI Result: score, feedback, followUpQuestion (nếu có)
    VivaService->>DB: Cập nhật Answer với AI Score & AI Feedback
    VivaService-->>API: SubmitVivaAnswerResponse (score, feedback, nextQuestion)
    API-->>SPA: 200 OK (Hiển thị kết quả tạm thời & câu tiếp theo)

    %% Giai đoạn 2: Hoàn thành bài thi
    Note over Student, DB: Giai đoạn 2: Kết thúc ca thi Viva
    Student->>SPA: Nhấn "Kết thúc phỏng vấn" / Hết giờ
    SPA->>API: POST /api/viva/sessions/{id}/finish
    API->>VivaService: FinishSessionAsync(sessionId, studentId)
    VivaService->>DB: Cập nhật Result status = PENDING_REVIEW, tổng điểm AI
    DB-->>VivaService: Saved
    VivaService-->>API: 200 OK
    API-->>SPA: Chuyển hướng về trang Kết quả (chưa công bố điểm)

    %% Giai đoạn 3: Giảng viên duyệt & công bố điểm
    Note over Teacher, DB: Giai đoạn 3: Giảng viên phúc khảo & công bố kết quả
    Teacher->>ResultCtrl: GET /api/teacher/results/{resultId}
    ResultCtrl->>ResultSvc: GetResultDetailForReviewAsync(resultId, teacherId)
    ResultSvc->>DB: Lấy chi tiết Result + Answers + AudioUrl + AI Rubric
    DB-->>ResultSvc: Dữ liệu bài thi
    ResultSvc-->>ResultCtrl: ResultReviewDto
    ResultCtrl-->>Teacher: Hiển thị bài làm, nghe file audio, xem điểm AI

    Teacher->>ResultCtrl: POST /api/teacher/results/{resultId}/publish (FinalScore, TeacherNotes)
    ResultCtrl->>ResultSvc: PublishResultAsync(resultId, teacherId, request)
    ResultSvc->>DB: Cập nhật Result status = PUBLISHED, FinalScore, PublishedAt
    DB-->>ResultSvc: Saved
    ResultSvc-->>ResultCtrl: 200 OK

    %% Giai đoạn 4: Sinh viên tra cứu điểm chính thức
    Note over Student, DB: Giai đoạn 4: Sinh viên xem điểm chính thức
    Student->>API: GET /api/student/results/{sessionId}
    API->>DB: Query Result với Status == PUBLISHED
    DB-->>API: Điểm số chính thức & nhận xét của Giảng viên
    API-->>Student: 200 OK (Hiển thị bảng điểm chính thức)
```

---

## 4. State Machine Diagram

### 4.1. Vòng đời Ca thi (`ExamSession`)
Quản lý trạng thái ca thi theo đúng ràng buộc `ValidateStatusTransition` tại `ExamSessionService.cs`:

```mermaid
stateDiagram-v2
    [*] --> NOT_STARTED: Teacher tạo ca thi (sinh SessionCode ESS-XXXXXXXX)
    
    NOT_STARTED --> IN_PROGRESS: Teacher mở ca thi (trong khung giờ StartTime - EndTime)
    NOT_STARTED --> CANCELLED: Teacher hủy ca thi trước khi bắt đầu
    
    IN_PROGRESS --> COMPLETED: Hết giờ thi hoặc tất cả thí sinh đã nộp bài
    IN_PROGRESS --> CANCELLED: Teacher hủy ca thi khẩn cấp
    
    COMPLETED --> [*]: Kết thúc ca thi, chuyển sang chấm & phúc khảo
    CANCELLED --> [*]: Ca thi bị vô hiệu hóa
```

### 4.2. Vòng đời Kết quả bài thi (`Result`)
Quản lý trạng thái công bố điểm thi theo đúng `ResultService.cs`:

```mermaid
stateDiagram-v2
    [*] --> PENDING_REVIEW: Sinh viên hoàn thành Viva (AI đã chấm sơ bộ)
    
    PENDING_REVIEW --> REVIEWED: Giảng viên mở xem xét và chỉnh sửa điểm từng câu
    PENDING_REVIEW --> PUBLISHED: Giảng viên công bố điểm trực tiếp (chấp nhận điểm AI)
    
    REVIEWED --> PUBLISHED: Giảng viên xác nhận điểm cuối và nhấn "Công bố kết quả"
    
    PUBLISHED --> [*]: Sinh viên có thể xem điểm chính thức & nhận xét
```

---

## 5. Bảng Truy vết Nghiệp vụ (Traceability Matrix)

| Business Rule | Sơ đồ liên quan | UI Component | API Route | Controller & Service | Entity & Table DB | Trạng thái kiểm chứng |
|---|---|---|---|---|---|---|
| **BR-01**: Admin quản lý tài khoản & phân quyền (RBAC) | C0, C1 | `UsersPage.tsx`, `UserFormDialog.tsx` | `GET /api/admin/users`<br>`POST /api/auth/admin/create-user`<br>`DELETE /api/admin/users/{id}` | `AdminUserController`, `AuthController`, `UserService` | `Users` (`users`) | Đã kiểm thử CRUD, xóa an toàn |
| **BR-02**: Admin phân công giảng viên phụ trách môn | C0, C1 | `AdminCoursesPage.tsx` | `POST /api/admin/courses`<br>`PUT /api/admin/courses/{id}`<br>`PUT /api/admin/courses/{id}/assign-teacher` | `AdminCourseController`, `CourseService` | `Courses` (`courses`) | Đã kiểm thử tạo, sửa tên & gán GV |
| **BR-03**: Giảng viên upload tài liệu chuẩn (.md) | C1, Sequence | `TeacherCoursesPage.tsx` | `POST /api/courses/{id}/documents`<br>`GET /api/courses/{id}/documents/{docId}` | `TeacherDocumentController`, `DocumentsController`, `DocumentService` | `MarkdownDocuments` (`markdown_documents`) | Upload Multipart, lưu nội dung vào DB & xem lại |
| **BR-04**: AI sinh câu hỏi từ tài liệu chuẩn | C1 | `TeacherQuestionsPage.tsx` | `POST /api/courses/{id}/documents/generate-questions` | `DocumentsController`, `DocumentService`, FastAPI AI Service | `QuestionBanks` (`question_banks`) | Hỗ trợ chọn số lượng, level và trọng tâm |
| **BR-05**: Giảng viên quản lý ngân hàng câu hỏi (CRUD) | C1 | `TeacherQuestionsPage.tsx` | `GET /api/courses/{id}/questions`<br>`DELETE /api/courses/{id}/questions/{id}` | `QuestionBankController`, `QuestionBankService` | `QuestionBanks` (`question_banks`) | Tìm kiếm, lọc theo level và xóa câu hỏi |
| **BR-06**: Giảng viên tạo đề thi từ ngân hàng | Sequence | `CreateExamDialog.tsx`, `ExamDetailDialog.tsx` | `POST /api/exams`<br>`POST /api/exams/suggest-questions`<br>`PUT /api/exams/{id}/questions` | `ExamsController`, `ExamService` | `Exams` (`exams`), `ExamQuestions` | AI gợi ý chọn câu, bắt buộc có câu hỏi |
| **BR-07**: Giảng viên lập lịch ca thi & sinh SessionCode | State ExamSession | `SessionsPage.tsx` | `POST /api/exam-sessions`<br>`PATCH /api/exam-sessions/{id}/status` | `ExamSessionsController`, `ExamSessionService` | `ExamSessions` (`exam_sessions`) | Mã `ESS-XXXXXXXX`, chặn tạo ca rỗng |
| **BR-08**: Sinh viên ghi danh bằng SessionCode | Sequence, State | `EnrollPage.tsx`, `StudentSessionsPage.tsx` | `POST /api/student/enrollments`<br>`GET /api/student/enrollments` | `StudentEnrollmentsController`, `EnrollController` | `SessionEnrollments` | Kiểm tra lịch, sĩ số tối đa |
| **BR-09**: Sinh viên trả lời Viva (âm thanh & văn bản) | C1, Sequence | `VivaRoomPage.tsx` | `POST /api/viva/sessions/{id}/start`<br>`POST /api/viva/sessions/{id}/submit-answer` | `VivaSessionController`, `VivaSessionService` | `Answers` (`answers`), File storage audio | Thu âm trực tiếp, STT, AI chấm điểm rubric |
| **BR-10**: Giảng viên phúc khảo & sửa điểm bài thi | Sequence, State Result | `TeacherResultsPage.tsx`, `ResultReviewModal.tsx` | `GET /api/teacher/results/{id}`<br>`PUT /api/teacher/results/{id}/review` | `ResultsController`, `ResultService` | `Results`, `Answers` | Nghe file ghi âm, xem gợi ý rubric AI |
| **BR-11**: Công bố kết quả & sinh viên tra cứu | Sequence, State Result | `StudentResultsPage.tsx` | `POST /api/teacher/results/{id}/publish`<br>`GET /api/student/results/{sessionId}` | `ResultsController`, `StudentResultController`, `ResultService` | `Results` (`results`) | Điểm chính thức chỉ hiển thị khi `PUBLISHED` |
