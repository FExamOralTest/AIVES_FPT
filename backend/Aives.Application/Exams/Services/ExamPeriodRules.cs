using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Services;

public static class ExamPeriodRules
{
    // Period start/end are inclusive calendar dates. Sessions use UTC instants; school time is UTC+7.
    public static void ValidateSession(ExamPeriod period, int courseId, DateTime start, DateTime end)
    {
        if (period.CourseId != courseId)
            throw new InvalidOperationException("The exam period must belong to the session's course.");
        if (period.Status != ExamPeriodStatus.ACTIVE)
            throw new InvalidOperationException("Only an active exam period can receive new or rescheduled sessions.");
        var firstInstant = DateTime.SpecifyKind(period.StartDate.Date.AddHours(-7), DateTimeKind.Utc);
        var lastInstant = DateTime.SpecifyKind(period.EndDate.Date.AddDays(1).AddHours(-7), DateTimeKind.Utc);
        if (start >= end || start < firstInstant || end > lastInstant)
            throw new InvalidOperationException("Session times must be within the exam period's dates (Vietnam time).");
    }
}
