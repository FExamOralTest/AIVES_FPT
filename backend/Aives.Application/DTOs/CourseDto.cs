namespace Aives.Application.DTOs;

/// <summary>Dữ liệu trả về cho Course.</summary>
public record CourseDto(
    int     CourseId,
    string  CourseCode,
    string  CourseName,
    int?    TeacherId,
    string? TeacherFullName,
    int?    CreatedBy,
    DateTime? CreatedAt
);

/// <summary>Admin tạo Course mới.</summary>
public record CreateCourseRequest(
    string CourseCode,
    string CourseName,
    int    CreatedBy
);

/// <summary>Admin cập nhật thông tin Course.</summary>
public record UpdateCourseRequest(
    string? CourseName
);

/// <summary>Admin phân công Teacher phụ trách Course.</summary>
public record AssignTeacherRequest(
    int TeacherId
);
