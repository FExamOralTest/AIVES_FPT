using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Aives.Infrastructure.Exams;

public class RetakeService(AivesDbContext db, TimeProvider? clock = null) : IRetakeService
{
    private DateTime Now => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    private async Task RequireReviewer(int callerId, CancellationToken ct)
    {
        var role = await db.Users.Where(u => u.UserId == callerId).Select(u => (UserRole?)u.Role).SingleOrDefaultAsync(ct);
        // Same review scope as ResultService; no permission to create arbitrary normal sessions.
        if (role is not (UserRole.EXAMINER or UserRole.ADMIN)) throw new UnauthorizedAccessException("Only examiners or admins can grant retakes.");
    }

    public async Task<RetakeDto> GrantAsync(int resultId, GrantRetakeRequest request, int callerId, CancellationToken ct = default)
    {
        await RequireReviewer(callerId, ct);
        ValidateWindow(request);
        return await Atomic(async () => {
            var original = await db.Results.Include(r => r.Session).ThenInclude(s => s.Exam).Include(r => r.Answers)
                .SingleOrDefaultAsync(r => r.ResultId == resultId, ct) ?? throw new KeyNotFoundException("Result not found.");
            await db.Entry(original).ReloadAsync(ct);
            if (original.Status == SubmissionStatus.PUBLISHED) throw new InvalidOperationException("Published results require a separate correction procedure and cannot be reopened.");
            var existing = await db.ExamRetakes.Include(r => r.RetakeSession).SingleOrDefaultAsync(r => r.OriginalResultId == resultId, ct);
            if (existing is not null) return await Map(existing, ct); // An acknowledgement retry must not grant another attempt.
            if (original.Status == SubmissionStatus.CANCELLED) throw new InvalidOperationException("This attempt is already invalidated.");
            var enrollment = await db.SessionEnrollments.SingleOrDefaultAsync(e => e.SessionId == original.SessionId && e.StudentId == original.StudentId, ct)
                ?? throw new InvalidOperationException("The student is not enrolled in the original session.");
            if (enrollment.StartedAt is null && original.Answers.Count == 0) throw new InvalidOperationException("The student has not started this attempt. Use the normal enrollment instead.");
            var examId = request.ExamId ?? original.Session.ExamId;
            var exam = await db.Exams.Include(e => e.ExamQuestions).SingleOrDefaultAsync(e => e.ExamId == examId, ct)
                ?? throw new KeyNotFoundException("Exam not found.");
            if (exam.CourseId != original.Session.Exam.CourseId || !exam.IsActive || exam.ExamStatus == ExamStatus.CANCELLED || exam.ExamQuestions.Count == 0
                || examId != original.Session.ExamId && exam.ExamStatus == ExamStatus.DRAFT)
                throw new InvalidOperationException("The retake must use an active exam with questions from the same course.");
            var session = new ExamSession {
                ExamId = examId, ScheduledBy = callerId, SessionName = $"Thi bù - bài {original.ResultId}",
                StartTime = request.StartTime, EndTime = request.EndTime, CreatedAt = Now,
                // Time gates still apply; the examiner does not need a staff account to open this private session.
                Status = SessionStatus.IN_PROGRESS, SessionCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)),
                MaxCapacity = 1, CurrentEnrollment = 1,
                SessionEnrollments = new List<SessionEnrollment> { new() { StudentId = original.StudentId, EnrolledAt = Now, Status = SessionStatus.NOT_STARTED, CurrentQuestionIndex = 0 } }
            };
            var retake = new ExamRetake { OriginalResult = original, RetakeSession = session, StudentId = original.StudentId,
                GrantedBy = callerId, GrantedAt = Now, Reason = request.Reason.Trim(), DurationMinutes = request.DurationMinutes };
            var oldStatus = original.Status;
            original.Status = SubmissionStatus.CANCELLED;
            enrollment.Status = SessionStatus.CANCELLED;
            // Answer/audio/transcript/AI scores/examiner scores remain intact as the invalidated attempt's history.
            db.ExamRetakes.Add(retake);
            db.AuditLogs.Add(new AuditLog { EntityName = "Result", EntityId = resultId, Action = "RETAKE_GRANTED", PerformedBy = callerId,
                Reason = retake.Reason, PerformedAt = Now, OldValue = JsonSerializer.Serialize(new { Status = oldStatus }),
                NewValue = JsonSerializer.Serialize(new { Status = "CANCELLED", examId, request.StartTime, request.EndTime, request.DurationMinutes, original.StudentId }) });
            await db.SaveChangesAsync(ct);
            return await Map(retake, ct);
        }, ct);
    }

    public async Task<RetakeDto> RescheduleAsync(int retakeId, GrantRetakeRequest request, int callerId, CancellationToken ct = default)
    {
        await RequireReviewer(callerId, ct); ValidateWindow(request);
        return await Atomic(async () => {
            var retake = await db.ExamRetakes.Include(r => r.RetakeSession).Include(r => r.OriginalResult).SingleOrDefaultAsync(r => r.RetakeId == retakeId, ct)
                ?? throw new KeyNotFoundException("Retake not found.");
            await db.Entry(retake).ReloadAsync(ct);
            await db.Entry(retake.RetakeSession).ReloadAsync(ct);
            if (retake.ConsumedAt is not null) throw new InvalidOperationException("A started retake cannot be rescheduled.");
            if (request.ExamId is int examId && examId != retake.RetakeSession.ExamId) throw new InvalidOperationException("Rescheduling cannot change the exam.");
            // Update the grant as well as the session to conflict with a simultaneous start.
            var previous = JsonSerializer.Serialize(new { retake.RetakeSession.StartTime, retake.RetakeSession.EndTime, retake.DurationMinutes });
            retake.DurationMinutes = request.DurationMinutes; retake.Revision++;
            retake.RetakeSession.StartTime = request.StartTime; retake.RetakeSession.EndTime = request.EndTime;
            db.Entry(retake).Property(r => r.ConsumedAt).IsModified = true;
            db.AuditLogs.Add(new AuditLog { EntityName = "ExamRetake", EntityId = retakeId, Action = "RETAKE_RESCHEDULED", PerformedBy = callerId,
                Reason = request.Reason.Trim(), PerformedAt = Now, OldValue = previous, NewValue = JsonSerializer.Serialize(new { request.StartTime, request.EndTime, request.DurationMinutes }) });
            await db.SaveChangesAsync(ct); return await Map(retake, ct);
        }, ct);
    }

    public async Task<RetakeStart?> StartAsync(int sessionId, int studentId, CancellationToken ct = default)
    {
        if (!await db.ExamRetakes.AnyAsync(r => r.RetakeSessionId == sessionId, ct)) return null;
        return await Atomic<RetakeStart?>(async () => {
            var retake = await db.ExamRetakes.Include(r => r.RetakeSession).SingleAsync(r => r.RetakeSessionId == sessionId, ct);
            // Viva may have loaded the session before this transaction. Refresh tracked
            // values so a schedule changed in another request is checked consistently.
            await db.Entry(retake).ReloadAsync(ct);
            await db.Entry(retake.RetakeSession).ReloadAsync(ct);
            if (retake.StudentId != studentId) throw new UnauthorizedAccessException("This retake was granted to another student.");
            var session = retake.RetakeSession;
            if (session.Status != SessionStatus.IN_PROGRESS || Now < session.StartTime || Now >= session.EndTime)
                throw new InvalidOperationException("The retake is outside its permitted time window.");
            var enrollment = await db.SessionEnrollments.SingleAsync(e => e.SessionId == sessionId && e.StudentId == studentId, ct);
            await db.Entry(enrollment).ReloadAsync(ct);
            if (enrollment.Status is SessionStatus.COMPLETED or SessionStatus.CANCELLED) throw new InvalidOperationException("This retake is already completed or cancelled.");
            var result = await db.Results.SingleOrDefaultAsync(r => r.SessionId == sessionId && r.StudentId == studentId, ct);
            if (result?.Status is SubmissionStatus.CANCELLED or SubmissionStatus.REVIEWED or SubmissionStatus.PUBLISHED)
                throw new InvalidOperationException("This retake is no longer writable.");
            var started = enrollment.StartedAt ?? Now;
            var deadline = new[] { started.AddMinutes(retake.DurationMinutes), session.EndTime }.Min();
            if (Now >= deadline) throw new InvalidOperationException("The retake time has expired.");
            if (retake.ConsumedAt is null) {
                if (result is not null) throw new InvalidOperationException("The retake state is inconsistent. Contact the examiner.");
                retake.ConsumedAt = started; retake.Revision++; enrollment.StartedAt = started; enrollment.LastActivityAt = Now;
                enrollment.Status = SessionStatus.IN_PROGRESS; enrollment.CurrentQuestionIndex = 0;
                db.Results.Add(new Result { SessionId = sessionId, StudentId = studentId, Status = SubmissionStatus.PENDING_REVIEW, SubmittedAt = null });
                db.AuditLogs.Add(new AuditLog { EntityName = "ExamRetake", EntityId = retake.RetakeId, Action = "RETAKE_STARTED", PerformedBy = studentId,
                    PerformedAt = Now, Reason = "Student started the authorized retake", NewValue = JsonSerializer.Serialize(new { sessionId, started, deadline }) });
                await db.SaveChangesAsync(ct);
            }
            return new RetakeStart(retake.DurationMinutes, deadline);
        }, ct);
    }

    public async Task EnsureSubmittedAsync(int resultId, CancellationToken ct = default)
    {
        if (await db.Results.AnyAsync(result => result.ResultId == resultId && result.SubmittedAt == null
            && db.ExamRetakes.Any(grant => grant.RetakeSessionId == result.SessionId && grant.StudentId == result.StudentId), ct))
            throw new InvalidOperationException("The student must finish the retake before it can be reviewed or published.");
    }

    public async Task EnsureWritableAsync(int sessionId, int studentId, bool checkDeadline = true, CancellationToken ct = default)
    {
        if (await db.Results.AsNoTracking().AnyAsync(r => r.SessionId == sessionId && r.StudentId == studentId && (r.Status == SubmissionStatus.CANCELLED || r.Status == SubmissionStatus.PUBLISHED), ct))
            throw new InvalidOperationException("This attempt was invalidated or published and cannot be changed.");
        var retake = await db.ExamRetakes.AsNoTracking().Include(r => r.RetakeSession).SingleOrDefaultAsync(r => r.RetakeSessionId == sessionId, ct);
        if (retake is null) return;
        if (retake.StudentId != studentId) throw new UnauthorizedAccessException("This retake belongs to another student.");
        if (retake.ConsumedAt is null) throw new InvalidOperationException("Start the retake before submitting answers.");
        var enrollment = await db.SessionEnrollments.AsNoTracking().SingleAsync(e => e.SessionId == sessionId && e.StudentId == studentId, ct);
        var deadline = new[] { retake.ConsumedAt.Value.AddMinutes(retake.DurationMinutes), retake.RetakeSession.EndTime }.Min();
        if (enrollment.Status == SessionStatus.CANCELLED || retake.RetakeSession.Status == SessionStatus.CANCELLED
            || checkDeadline && (enrollment.Status == SessionStatus.COMPLETED || Now >= deadline))
            throw new InvalidOperationException("This retake is completed, cancelled or its time has expired.");
    }

    public async Task<IReadOnlyList<RetakeDto>> HistoryAsync(int resultId, int callerId, CancellationToken ct = default)
    {
        await RequireReviewer(callerId, ct);
        var result = await db.Results.AsNoTracking().SingleOrDefaultAsync(r => r.ResultId == resultId, ct) ?? throw new KeyNotFoundException("Result not found.");
        var seen = new HashSet<int>();
        while (seen.Add(result.ResultId)) {
            var parent = await db.ExamRetakes.AsNoTracking().SingleOrDefaultAsync(r => r.RetakeSessionId == result.SessionId && r.StudentId == result.StudentId, ct);
            if (parent is null) break;
            result = await db.Results.AsNoTracking().SingleAsync(r => r.ResultId == parent.OriginalResultId, ct);
        }
        var history = new List<RetakeDto>(); seen.Clear();
        while (seen.Add(result.ResultId)) {
            var grant = await db.ExamRetakes.AsNoTracking().Include(r => r.RetakeSession).Include(r => r.OriginalResult)
                .SingleOrDefaultAsync(r => r.OriginalResultId == result.ResultId, ct);
            if (grant is null) break;
            history.Add(await Map(grant, ct));
            var next = await db.Results.AsNoTracking().SingleOrDefaultAsync(r => r.SessionId == grant.RetakeSessionId && r.StudentId == grant.StudentId, ct);
            if (next is null) break; result = next;
        }
        return history;
    }

    private async Task<RetakeDto> Map(ExamRetake grant, CancellationToken ct)
    {
        var result = await db.Results.AsNoTracking().SingleOrDefaultAsync(r => r.SessionId == grant.RetakeSessionId && r.StudentId == grant.StudentId, ct);
        return new RetakeDto { RetakeId = grant.RetakeId, OriginalResultId = grant.OriginalResultId, OriginalStatus = "CANCELLED",
            RetakeSessionId = grant.RetakeSessionId, StudentId = grant.StudentId, GrantedBy = grant.GrantedBy, GrantedAt = grant.GrantedAt,
            Reason = grant.Reason, StartTime = grant.RetakeSession.StartTime, EndTime = grant.RetakeSession.EndTime, DurationMinutes = grant.DurationMinutes,
            ConsumedAt = grant.ConsumedAt, RetakeResultId = result?.ResultId, ResultStatus = result?.Status?.ToString(), FinalScore = result?.FinalScore,
            Status = result?.Status == SubmissionStatus.CANCELLED ? "INVALIDATED" : result?.SubmittedAt is not null ? "COMPLETED"
                : Now >= grant.RetakeSession.EndTime || grant.ConsumedAt is DateTime start && Now >= start.AddMinutes(grant.DurationMinutes) ? "EXPIRED"
                : grant.ConsumedAt is not null ? "IN_PROGRESS" : "GRANTED" };
    }
    private void ValidateWindow(GrantRetakeRequest request)
    {
        if (request.Reason is null || request.Reason.Trim().Length < 5 || request.Reason.Length > 1000) throw new ArgumentException("Provide a reason between 5 and 1000 characters.");
        if (request.StartTime.Kind != DateTimeKind.Utc || request.EndTime.Kind != DateTimeKind.Utc) throw new ArgumentException("Use UTC timestamps with a timezone for the retake window.");
        if (request.DurationMinutes is < 1 or > 240 || request.EndTime <= Now || request.StartTime >= request.EndTime
            || request.EndTime - request.StartTime < TimeSpan.FromMinutes(request.DurationMinutes)) throw new ArgumentException("The permitted window must contain the full exam duration and end in the future.");
    }
    private async Task<T> Atomic<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        try { var value = await action(); if (transaction is not null) await transaction.CommitAsync(ct); return value; }
        catch (Exception ex) {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            if (ex is DbUpdateConcurrencyException || FindPostgres(ex) is { SqlState: "23505" or "40001" or "40P01" })
                throw new InvalidOperationException("The retake changed concurrently. Refresh before trying again.");
            throw;
        }
    }
    private static PostgresException? FindPostgres(Exception? ex) => ex is null ? null : ex as PostgresException ?? FindPostgres(ex.InnerException);
}
