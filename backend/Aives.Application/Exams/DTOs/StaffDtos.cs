using System.ComponentModel.DataAnnotations;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.DTOs;

// ─────────────────────────────────────────────────────────────────
//  STAFF — Quản lý phòng thi & lịch thu xếp lớp / ca thi
// ─────────────────────────────────────────────────────────────────
//
// TODO (future): khi bổ sung bảng `rooms` riêng (RoomId, Name, Capacity, Location, IsActive)
// thì chuyển ExamSession.Room (string) thành ExamSession.RoomId (FK int) và CRUD Room
// qua StaffController. Hiện tại chỉ quản lý phòng thi thông qua trường `Room` (string)
// trên ExamSession — đủ dùng cho MVP.
//
// TODO (future): khi bổ sung bảng `classes` riêng (ClassId, Name, CourseId, Semester, ...)
// thì bật lại GetAllClassesAsync trỏ vào bảng mới. Hiện tại staff dùng
// GET /api/courses có sẵn nên tạm ẩn.
//
// Staff được phép:
//   1. CRUD ca thi (ExamSession) — phòng, lịch, capacity, status
//   2. Xếp lớp / phân công giảng viên cho Course
//   3. Chọn đề thi random (xài ké POST /api/documents/generate-exam)
//   4. Quản lý sinh viên đăng ký vào ca (thêm nhiều SV một lần / xoá)
//   5. Xem monitor tiến độ ca thi (xài ké GET /api/exam-sessions/{id}/monitor)
// Admin bypass mọi ownership check.
// ─────────────────────────────────────────────────────────────────

/// <summary>Staff tạo ca thi (không cần là người tạo Exam).</summary>
public class StaffCreateSessionRequest
{
    public int? ExamId { get; set; }

    public int? CourseId { get; set; }

    public int? PeriodId { get; set; }

    public List<int>? ExamIds { get; set; }

    public List<CreateSessionSlotInput>? Slots { get; set; }

    [Required, MaxLength(150)]
    public string SessionName { get; set; } = null!;

    [MaxLength(150)]
    public string? Room { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    [Range(1, 500)]
    public int MaxCapacity { get; set; } = 50;
}

/// <summary>Staff cập nhật thông tin ca thi (kể cả phòng, lịch, capacity, danh sách đề).</summary>
public class StaffUpdateSessionRequest
{
    [MaxLength(150)]
    public string? SessionName { get; set; }

    [MaxLength(150)]
    public string? Room { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Range(1, 500)]
    public int? MaxCapacity { get; set; }

    public SessionStatus? Status { get; set; }

    public int? PeriodId { get; set; }

    public List<int>? ExamIds { get; set; }

    public List<CreateSessionSlotInput>? Slots { get; set; }
}

public class DistributeExamsRequest
{
    public int? RandomSeed { get; set; }
    public string? Strategy { get; set; } = "BALANCED_RANDOM";
}

public class DistributeSlotsRequest
{
    public int? RandomSeed { get; set; }
    public string? Strategy { get; set; } = "SLOT_BALANCED";
    public string? ExamAssignmentMode { get; set; } = "EXAM_PER_SLOT"; // "EXAM_PER_SLOT" | "SHUFFLE_IN_SLOT"
}

public class SlotAllocationCountDto
{
    public int SlotId { get; set; }
    public int SlotIndex { get; set; }
    public string SlotName { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int StudentCount { get; set; }
}

public class DistributeSlotsResponse
{
    public int SessionId { get; set; }
    public int TotalStudents { get; set; }
    public int TotalSlots { get; set; }
    public List<SlotAllocationCountDto> Allocations { get; set; } = new();
}

public class StaffAssignSlotRequest
{
    [Required]
    public int SlotId { get; set; }
}

public class ExamAllocationCountDto
{
    public int ExamId { get; set; }
    public string ExamCode { get; set; } = null!;
    public string Title { get; set; } = null!;
    public int StudentCount { get; set; }
}

public class DistributeExamsResponse
{
    public int SessionId { get; set; }
    public int TotalStudents { get; set; }
    public int TotalExams { get; set; }
    public List<ExamAllocationCountDto> Allocations { get; set; } = new();
}

public class StaffAssignExamRequest
{
    [Required]
    public int ExamId { get; set; }
}

/// <summary>Staff xếp lớp / phân công giảng viên cho Course.</summary>
public class StaffAssignTeacherRequest
{
    [Required]
    public int TeacherId { get; set; }
}

/// <summary>Staff thêm nhiều sinh viên vào ca thi (1 transaction).</summary>
public class StaffAddEnrollmentsRequest
{
    /// <summary>Danh sách studentId cần thêm (≤ 500/lần để tránh quá tải request).</summary>
    public int[]? StudentIds { get; set; }

    /// <summary>Danh sách chuỗi nhận diện: Mã SV (MSSV / Username), Email, hoặc ID số.</summary>
    public string[]? Identifiers { get; set; }
}

/// <summary>Kết quả bulk enroll — chi tiết từng sinh viên (added / skipped).</summary>
public class StaffBulkEnrollResponse
{
    public int SessionId { get; set; }
    public int AddedCount { get; set; }
    public int SkippedCount { get; set; }
    public int CurrentEnrollment { get; set; }
    public List<StaffEnrollResult> Results { get; set; } = new();
}

public class StaffEnrollResult
{
    public int StudentId { get; set; }
    public string? Identifier { get; set; }
    public string? StudentCode { get; set; }
    public string? StudentName { get; set; }
    public string Status { get; set; } = "added";   // "added" | "already_enrolled" | "not_student" | "not_found"
    public int? SessionEnrollmentId { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Lọc danh sách ca thi theo trạng thái.</summary>
public class StaffSessionQuery
{
    public SessionStatus? Status { get; set; }
}
