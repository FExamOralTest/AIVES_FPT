using System;
using System.ComponentModel.DataAnnotations;

namespace Aives.Application.Exams.DTOs;

public class ExamPeriodDto
{
    public int PeriodId { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string PeriodName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public int SessionsCount { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class CreateExamPeriodRequest
{
    [Required]
    public int CourseId { get; set; }

    [Required]
    [MaxLength(150)]
    public string PeriodName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}

