namespace Aives.Application.Courses.DTOs;

public record CourseDto(
    int       CourseId,
    string    CourseCode,
    string    CourseName,
    int?      TeacherId,
    string?   TeacherFullName,
    int?      CreatedBy,
    DateTime? CreatedAt
);

public record CreateCourseRequest(
    string CourseCode,
    string CourseName,
    int    CreatedBy
);

public record UpdateCourseRequest(
    string? CourseName
);

public record AssignTeacherRequest(
    int TeacherId
);
