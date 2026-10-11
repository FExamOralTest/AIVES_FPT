using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    private const string AdminUsername = "admin";
    private const string AdminEmail    = "admin@aives.edu.vn";
    private const string AdminPassword = "Admin@123";

    /// <summary>
    /// Tự động seed 1 admin mặc định khi app khởi động.
    /// Idempotent — bỏ qua nếu username đã tồn tại.
    /// </summary>
    public static async Task SeedAdminAsync(AivesDbContext db)
    {
        var exists = await db.Users.AnyAsync(u => u.Username == AdminUsername);
        if (exists) return;

        var admin = new User
        {
            Username     = AdminUsername,
            FullName     = "System Admin",
            Email        = AdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword, workFactor: 12),
            Role         = UserRole.ADMIN,
            CreatedAt    = DateTime.UtcNow
        };

        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seed 3 courses + 12 câu hỏi mỗi course (4 câu × level 1/2/3).
    /// Dùng raw SQL để ép CourseId = 1, 2, 3 (OVERRIDING SYSTEM VALUE).
    /// Idempotent — bỏ qua nếu courseCode đã tồn tại.
    /// </summary>
    public static async Task SeedQuestionBankAsync(AivesDbContext db)
    {
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Username == AdminUsername);
        if (admin is null) return;

        // ── Định nghĩa 3 courses với ID cố định ──────────────────────────
        var courseDefs = new[]
        {
            new { Id = 1, Code = "CS101", Name = "Nhập môn Lập trình" },
            new { Id = 2, Code = "SE201", Name = "Kỹ nghệ Phần mềm"   },
            new { Id = 3, Code = "AI301", Name = "Trí tuệ Nhân tạo"   }
        };

        // ── Định nghĩa câu hỏi theo course và level ──────────────────────
        var questionDefs = new Dictionary<string, Dictionary<int, string[]>>
        {
            ["CS101"] = new()
            {
                [1] = new[]
                {
                    "Biến là gì? Hãy giải thích khái niệm biến trong lập trình.",
                    "Kiểu dữ liệu int và float khác nhau như thế nào?",
                    "Vòng lặp for được dùng khi nào? Cho ví dụ đơn giản.",
                    "Hàm trong lập trình là gì? Tại sao chúng ta cần dùng hàm?"
                },
                [2] = new[]
                {
                    "Giải thích sự khác biệt giữa vòng lặp for và while. Khi nào nên dùng cái nào?",
                    "Đệ quy là gì? Hãy giải thích cách hoạt động của đệ quy qua ví dụ tính giai thừa.",
                    "Mảng (array) và danh sách liên kết (linked list) khác nhau như thế nào về cấu trúc và hiệu suất?",
                    "Hãy giải thích khái niệm scope (phạm vi) của biến trong một chương trình."
                },
                [3] = new[]
                {
                    "Phân tích độ phức tạp thuật toán Big-O của thuật toán sắp xếp nổi bọt (bubble sort) và giải thích tại sao.",
                    "So sánh lập trình hướng đối tượng (OOP) với lập trình hướng thủ tục (procedural). Ưu nhược điểm của từng paradigm?",
                    "Giải thích khái niệm con trỏ trong C/C++. Tại sao con trỏ vừa mạnh mẽ vừa nguy hiểm?",
                    "Thiết kế thuật toán tìm kiếm nhị phân. Phân tích độ phức tạp thời gian và không gian."
                }
            },
            ["SE201"] = new()
            {
                [1] = new[]
                {
                    "Phần mềm là gì? Phân biệt phần mềm hệ thống và phần mềm ứng dụng.",
                    "Mô hình thác nước (Waterfall) trong phát triển phần mềm gồm những giai đoạn nào?",
                    "Git là gì? Commit và Branch trong Git có ý nghĩa gì?",
                    "Unit test là gì? Tại sao cần viết unit test?"
                },
                [2] = new[]
                {
                    "So sánh mô hình Waterfall và Agile. Dự án nào phù hợp với từng mô hình?",
                    "Design pattern là gì? Hãy giải thích pattern Singleton và ứng dụng thực tế của nó.",
                    "Giải thích nguyên tắc SOLID trong lập trình hướng đối tượng.",
                    "Code review là gì? Quy trình code review nên bao gồm những bước nào?"
                },
                [3] = new[]
                {
                    "Phân tích trade-off giữa microservices và monolithic architecture. Khi nào nên chọn từng cách?",
                    "Giải thích CI/CD pipeline. Các thành phần chính và lợi ích mà nó mang lại là gì?",
                    "Kỹ thuật refactoring là gì? Mô tả một tình huống thực tế cần refactor và cách tiếp cận.",
                    "Hãy thiết kế sơ lược hệ thống quản lý thư viện. Nêu các entity, quan hệ và API chính cần có."
                }
            },
            ["AI301"] = new()
            {
                [1] = new[]
                {
                    "Trí tuệ nhân tạo (AI) là gì? Nêu 3 ứng dụng thực tế của AI trong cuộc sống.",
                    "Machine learning khác gì so với lập trình truyền thống?",
                    "Supervised learning và unsupervised learning khác nhau như thế nào? Cho ví dụ.",
                    "Neural network là gì? Mô tả cấu trúc cơ bản của một mạng nơ-ron nhân tạo."
                },
                [2] = new[]
                {
                    "Giải thích thuật toán Gradient Descent. Tại sao nó quan trọng trong huấn luyện mô hình?",
                    "Overfitting là gì? Các kỹ thuật nào giúp giảm overfitting trong machine learning?",
                    "So sánh Decision Tree và Random Forest. Ưu nhược điểm của từng phương pháp?",
                    "Giải thích quá trình backpropagation trong deep learning."
                },
                [3] = new[]
                {
                    "Transformer architecture đã thay đổi lĩnh vực NLP như thế nào? Giải thích cơ chế Self-Attention.",
                    "Phân tích ethical concerns trong AI: bias, fairness, transparency. Cho ví dụ cụ thể và cách giảm thiểu.",
                    "Giải thích sự khác biệt giữa Generative AI và Discriminative AI. Ứng dụng của từng loại?",
                    "Thiết kế pipeline cho bài toán phân loại văn bản từ bước thu thập dữ liệu đến deploy model."
                }
            }
        };

        var maxScoreByLevel = new Dictionary<int, float> { [1] = 1.0f, [2] = 2.0f, [3] = 3.0f };

        foreach (var courseDef in courseDefs)
        {
            // Kiểm tra theo courseCode — không dùng ID để tránh conflict
            var course = await db.Courses.FirstOrDefaultAsync(c => c.CourseCode == courseDef.Code);

            if (course is null)
            {
                // INSERT OVERRIDING SYSTEM VALUE — ép PostgreSQL chấp nhận ID tùy chọn
                // dù column là GENERATED ALWAYS AS IDENTITY
                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO courses (course_id, course_code, course_name, created_by, created_at)
                    OVERRIDING SYSTEM VALUE
                    VALUES ({0}, {1}, {2}, {3}, NOW())
                    ON CONFLICT (course_id) DO NOTHING
                    """,
                    courseDef.Id, courseDef.Code, courseDef.Name, admin.UserId);

                // Reset sequence về max(course_id) để tránh conflict khi EF tự tăng sau
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT setval(pg_get_serial_sequence('courses','course_id'), MAX(course_id)) FROM courses");

                course = await db.Courses.FirstOrDefaultAsync(c => c.CourseCode == courseDef.Code);
            }

            if (course is null) continue;

            // Bỏ qua nếu đã có câu hỏi
            var hasQuestions = await db.QuestionBanks.AnyAsync(q => q.CourseId == course.CourseId);
            if (hasQuestions) continue;

            // Tạo 12 câu: 4 câu × 3 level
            foreach (var (level, texts) in questionDefs[courseDef.Code])
            {
                foreach (var text in texts)
                {
                    db.QuestionBanks.Add(new QuestionBank
                    {
                        CourseId          = course.CourseId,
                        QuestionText      = text,
                        Level             = level,
                        MaxScore          = maxScoreByLevel[level],
                        IsFollowUpAllowed = true,
                        CreatedAt         = DateTime.UtcNow
                    });
                }
            }

            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Seed đầy đủ luồng thi để test GET exam history:
    ///   - 1 teacher, 2 students
    ///   - 1 exam (CS101), 1 session, cả 2 student tham gia
    ///   - Mỗi student có Result với Answers đầy đủ (aiScore, finalScore, feedback)
    /// Idempotent — bỏ qua nếu exam "CS101-EXAM-01" đã tồn tại.
    /// </summary>
    public static async Task SeedExamDataAsync(AivesDbContext db)
    {
        // Bỏ qua nếu đã seed
        if (await db.Exams.AnyAsync(e => e.ExamCode == "CS101-EXAM-01")) return;

        var admin = await db.Users.FirstOrDefaultAsync(u => u.Username == AdminUsername);
        if (admin is null) return;

        var course = await db.Courses.FirstOrDefaultAsync(c => c.CourseCode == "CS101");
        if (course is null) return;

        // ── 1. Teacher ────────────────────────────────────────────────────
        var teacher = await db.Users.FirstOrDefaultAsync(u => u.Username == "teacher_cs101");
        if (teacher is null)
        {
            teacher = new User
            {
                Username     = "teacher_cs101",
                FullName     = "Trần Văn Giảng",
                Email        = "teacher_cs101@aives.edu.vn",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Teacher@123", workFactor: 12),
                Role         = UserRole.TEACHER,
                CreatedAt    = DateTime.UtcNow
            };
            db.Users.Add(teacher);
            await db.SaveChangesAsync();
        }

        // Assign teacher vào course CS101
        if (course.TeacherId is null)
        {
            course.TeacherId = teacher.UserId;
            db.Courses.Update(course);
            await db.SaveChangesAsync();
        }

        // ── 2. Students ───────────────────────────────────────────────────
        var studentDefs = new[]
        {
            new { Username = "student_001", FullName = "Nguyễn Văn An",  Email = "student001@aives.edu.vn" },
            new { Username = "student_002", FullName = "Lê Thị Bình",    Email = "student002@aives.edu.vn" }
        };

        var students = new List<User>();
        foreach (var s in studentDefs)
        {
            var student = await db.Users.FirstOrDefaultAsync(u => u.Username == s.Username);
            if (student is null)
            {
                student = new User
                {
                    Username     = s.Username,
                    FullName     = s.FullName,
                    Email        = s.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Student@123", workFactor: 12),
                    Role         = UserRole.STUDENT,
                    CreatedAt    = DateTime.UtcNow
                };
                db.Users.Add(student);
                await db.SaveChangesAsync();
            }
            students.Add(student);
        }

        // ── 3. Lấy 6 câu hỏi CS101 (2 câu mỗi level) ────────────────────
        var questions = await db.QuestionBanks
            .Where(q => q.CourseId == course.CourseId)
            .OrderBy(q => q.Level)
            .ThenBy(q => q.QuestionId)
            .Take(6)
            .ToListAsync();

        if (questions.Count < 6) return; // cần seed question bank trước

        // ── 4. Exam ───────────────────────────────────────────────────────
        var exam = new Exam
        {
            CourseId        = course.CourseId,
            CreatedBy       = teacher.UserId,
            Title           = "Kiểm tra Nhập môn Lập trình - Giữa kỳ",
            ExamCode        = "CS101-EXAM-01",
            DurationMinutes = 45,
            MaxQuestions    = 6,
            ExamStatus      = ExamStatus.CLOSED,
            CreatedAt       = DateTime.UtcNow.AddDays(-7)
        };
        db.Exams.Add(exam);
        await db.SaveChangesAsync();

        // ExamQuestions — gán câu hỏi vào đề
        foreach (var q in questions)
        {
            db.ExamQuestions.Add(new ExamQuestion
            {
                ExamId        = exam.ExamId,
                QuestionId    = q.QuestionId,
                QuestionScore = q.MaxScore ?? 1.0f
            });
        }
        await db.SaveChangesAsync();

        // ── 5. ExamSession ────────────────────────────────────────────────
        var session = new ExamSession
        {
            ExamId      = exam.ExamId,
            ScheduledBy = teacher.UserId,
            SessionName = "Ca thi ngày 27/09/2026 - Phòng A101",
            Room        = "A101",
            StartTime   = DateTime.UtcNow.AddDays(-7),
            EndTime     = DateTime.UtcNow.AddDays(-7).AddMinutes(45),
            Status      = SessionStatus.COMPLETED,
            CreatedAt   = DateTime.UtcNow.AddDays(-7)
        };
        db.ExamSessions.Add(session);
        await db.SaveChangesAsync();

        // SessionEnrollment
        foreach (var student in students)
        {
            db.SessionEnrollments.Add(new SessionEnrollment
            {
                SessionId  = session.SessionId,
                StudentId  = student.UserId,
                Status     = SessionStatus.COMPLETED,
                EnrolledAt = DateTime.UtcNow.AddDays(-8)
            });
        }
        await db.SaveChangesAsync();

        // ── 6. Results + Answers cho từng student ─────────────────────────
        var studentScenarios = new[]
        {
            new { AiScore = 7.5f,  FinalScore = 8.0f,  Feedback = "Nắm vững kiến thức cơ bản, trình bày rõ ràng." },
            new { AiScore = 5.0f,  FinalScore = 5.5f,  Feedback = "Cần bổ sung thêm kiến thức về kiểu dữ liệu và vòng lặp." }
        };

        for (var i = 0; i < students.Count; i++)
        {
            var student  = students[i];
            var scenario = studentScenarios[i];

            var result = new Result
            {
                SessionId     = session.SessionId,
                StudentId     = student.UserId,
                ReviewedBy    = teacher.UserId,
                AiScore       = scenario.AiScore,
                AiFeedback    = "AI đánh giá tự động: " + scenario.Feedback,
                FinalScore    = scenario.FinalScore,
                FinalFeedback = "Giáo viên nhận xét: " + scenario.Feedback,
                Status        = SubmissionStatus.PUBLISHED,
                SubmittedAt   = session.StartTime.AddMinutes(40),
                ReviewedAt    = DateTime.UtcNow.AddDays(-6),
                PublishedAt   = DateTime.UtcNow.AddDays(-5)
            };
            db.Results.Add(result);
            await db.SaveChangesAsync();

            // Answers — mỗi câu hỏi có 1 answer
            var answerSamples = new[]
            {
                "Biến là vùng nhớ được đặt tên để lưu trữ giá trị có thể thay đổi trong quá trình thực thi.",
                "int lưu số nguyên (không có phần thập phân), float lưu số thực (có phần thập phân).",
                "Vòng lặp for dùng khi biết trước số lần lặp. Ví dụ: for(int i=0; i<10; i++) in số 0 đến 9.",
                "Hàm là khối lệnh được đặt tên, thực hiện một nhiệm vụ cụ thể. Dùng để tái sử dụng code.",
                "for dùng khi biết số lần lặp, while dùng khi lặp đến khi điều kiện sai.",
                "Đệ quy là hàm tự gọi lại chính nó. Ví dụ factorial(n) = n * factorial(n-1)."
            };

            for (var j = 0; j < questions.Count; j++)
            {
                var aiScore    = (float)Math.Round(scenario.AiScore / questions.Count, 1);
                var finalScore = (float)Math.Round(scenario.FinalScore / questions.Count, 1);

                db.Answers.Add(new Answer
                {
                    ResultId       = result.ResultId,
                    QuestionId     = questions[j].QuestionId,
                    StudentAnswer  = answerSamples[j % answerSamples.Length],
                    Transcript     = answerSamples[j % answerSamples.Length],
                    IsFollowUp     = false,
                    AiScore        = aiScore,
                    AiFeedback     = $"AI: câu trả lời đạt {aiScore * 10:F0}% yêu cầu.",
                    FinalScore     = finalScore,
                    TeacherComment = j % 2 == 0 ? "Đúng hướng, cần giải thích thêm." : null,
                    AnsweredAt     = session.StartTime.AddMinutes(5 + j * 5)
                });
            }

            await db.SaveChangesAsync();
        }
    }
}

