using System.ComponentModel.DataAnnotations;

namespace Aives.Application.DTOs;
public record CourseDto(
    int     CourseId,
    string  CourseCode,
    string  CourseName,
    int?    TeacherId,
    string? TeacherFullName,
    int?    CreatedBy,
    DateTime? CreatedAt
);
public record CreateCourseRequest(
    [Required, MaxLength(20)] string CourseCode,
    [Required, MaxLength(150)] string CourseName,
    int?   CreatedBy = null,
    int?   TeacherId = null
);
public record UpdateCourseRequest(
    [Required, MaxLength(150)] string CourseName,
    [MaxLength(20)] string? CourseCode = null
);
public record AssignTeacherRequest(
    int TeacherId
);
