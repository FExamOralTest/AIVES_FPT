using System.ComponentModel.DataAnnotations;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.DTOs;

// ═══════════════════════════════════════════════════════════════════
//  STAFF — Quản lý phòng thi & lịch thu xếp lớp / ca thi
// ═══════════════════════════════════════════════════════════════════
//
// TODO (future): khi bổ sung bảng `rooms` riêng (RoomId, Name, Capacity, Location, IsActive)
// thì chuyển ExamSession.Room (string) thành ExamSession.RoomId (FK int) và CRUD Room
// qua StaffController. Hiện tại chỉ quản lý phòng thi thông qua trường `Room` (string)
// trên ExamSession — đủ dùng cho MVP.
//
// Staff được phép:
//   1. CRUD ca thi (ExamSession) — phòng, lịch, capacity, status
//   2. Xếp lớp / phân công giảng viên cho Course
//   3. Chọn đề thi random (xài ké POST /api/documents/generate-exam)
//   4. Quản lý sinh viên đăng ký vào ca (thêm/xoá/sửa enrollment)
//   5. Xem monitor tiến độ ca thi (xài ké GET /api/exam-sessions/{id}/monitor)
// Admin bypass mọi ownership check.
// ═══════════════════════════════════════════════════════════════════

/// <summary>Staff tạo ca thi (không cần là người tạo Exam).</summary>
public class StaffCreateSessionRequest
{
    [Required]
    public int ExamId { get; set; }

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

/// <summary>Staff cập nhật thông tin ca thi (kể cả phòng, lịch, capacity).</summary>
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
}

/// <summary>Staff xếp lớp / phân công giảng viên cho Course.</summary>
public class StaffAssignTeacherRequest
{
    [Required]
    public int TeacherId { get; set; }
}

/// <summary>Staff thêm một hoặc nhiều sinh viên vào ca thi (bulk insert trong 1 transaction).</summary>
public class StaffAddEnrollmentRequest
{
    /// <summary>Danh sách sinh viên cần thêm. Tối đa 500 / request.</summary>
    [Required, MinLength(1)]
    public List<int> StudentIds { get; set; } = new();
}

/// <summary>Kết quả trả về khi thêm nhiều sinh viên vào ca thi.</summary>
public class StaffBulkEnrollmentResult
{
    /// <summary>Danh sách enrollment đã tạo thành công.</summary>
    public List<SessionEnrollmentDto> Added { get; set; } = new();

    /// <summary>StudentId bị trùng (đã có enrollment).</summary>
    public List<int> AlreadyEnrolled { get; set; } = new();

    /// <summary>StudentId không tồn tại.</summary>
    public List<int> NotFound { get; set; } = new();

    /// <summary>StudentId không thuộc ca thi (không cùng course/exam).</summary>
    public List<int> Ineligible { get; set; } = new();
}

/// <summary>Staff xoá nhiều sinh viên khỏi ca thi (bulk, 1 transaction).</summary>
public class StaffRemoveEnrollmentRequest
{
    /// <summary>Danh sách student cần xoá. Tối đa 500 / request.</summary>
    [Required, MinLength(1)]
    public List<int> StudentIds { get; set; } = new();
}

/// <summary>Kết quả trả về khi xoá nhiều enrollment.</summary>
public class StaffBulkRemoveResult
{
    /// <summary>StudentId đã xoá enrollment thành công.</summary>
    public List<int> Removed { get; set; } = new();

    /// <summary>StudentId không có enrollment trong ca này.</summary>
    public List<int> NotEnrolled { get; set; } = new();
}

/// <summary>Lọc danh sách ca thi theo trạng thái.</summary>
public class StaffSessionQuery
{
    public SessionStatus? Status { get; set; }
}
