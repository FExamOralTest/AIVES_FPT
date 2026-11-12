using Aives.Domain.Enums;

namespace Aives.Application.Exams;

/// <summary>
/// Exam lifecycle: DRAFT → SCHEDULED → OPEN → CLOSED, with CANCELLED reachable before the exam opens.
/// Only a DRAFT exam can be edited, so leaving DRAFT locks the questions.
/// </summary>
public static class ExamStatusTransitions
{
    private static readonly Dictionary<ExamStatus, ExamStatus[]> Allowed = new()
    {
        [ExamStatus.DRAFT] = [ExamStatus.SCHEDULED, ExamStatus.OPEN, ExamStatus.CANCELLED],
        [ExamStatus.SCHEDULED] = [ExamStatus.DRAFT, ExamStatus.OPEN, ExamStatus.CANCELLED],
        [ExamStatus.OPEN] = [ExamStatus.CLOSED],
        [ExamStatus.CLOSED] = [],
        [ExamStatus.CANCELLED] = [],
    };

    /// <summary>A missing status is treated as DRAFT, the column's database default.</summary>
    public static IReadOnlyList<ExamStatus> NextFrom(ExamStatus? current)
        => Allowed[current ?? ExamStatus.DRAFT];

    public static bool CanMove(ExamStatus? current, ExamStatus target)
        => NextFrom(current).Contains(target);

    /// <summary>Statuses where students may take the exam, so it needs questions first.</summary>
    public static bool RequiresQuestions(ExamStatus target)
        => target is ExamStatus.SCHEDULED or ExamStatus.OPEN;
}
