using System.Security.Cryptography;







using Aives.Application.Exams.DTOs;







using Aives.Application.Exams.Interfaces;







using Aives.Domain.Entities;







using Aives.Domain.Enums;















namespace Aives.Application.Exams.Services;







public class ExamSessionService : IExamSessionService







{







    private readonly IExamSessionRepository _sessionRepository;







    private readonly IExamRepository _examRepository;







    private readonly IExamUnitOfWork _unitOfWork;















    public ExamSessionService(







        IExamSessionRepository sessionRepository,







        IExamRepository examRepository, IExamUnitOfWork unitOfWork)







    {







        _sessionRepository = sessionRepository;







        _examRepository = examRepository;







        _unitOfWork = unitOfWork;







    }







    public Task<ExamSessionDto> CreateSessionAsync(







        CreateExamSessionRequest request, int teacherId)







        => _unitOfWork.ExecuteAsync(async () =>







        {







            var examIds = request.ExamIds?.Distinct().ToList() ?? new List<int>();







            if (examIds.Count == 0 && request.ExamId.HasValue && request.ExamId.Value > 0)







                examIds.Add(request.ExamId.Value);















            if (examIds.Count == 0)







                throw new ArgumentException("At least one exam must be selected for the session.");















            var exams = new List<Exam>();







            int courseId = request.CourseId ?? 0;















            foreach (var id in examIds)







            {







                var exam = await _examRepository.GetByIdWithQuestionsAsync(id)







                    ?? throw new KeyNotFoundException($"Exam with ID {id} not found.");















                if (exam.CreatedBy != teacherId)







                    throw new UnauthorizedAccessException(







                        $"You do not have permission to create a session for exam {exam.ExamCode}.");















                if (exam.ExamQuestions is null || exam.ExamQuestions.Count == 0)







                    throw new InvalidOperationException($"Add questions to exam {exam.ExamCode} before creating a session.");















                if (courseId == 0)







                    courseId = exam.CourseId;







                else if (exam.CourseId != courseId)







                    throw new InvalidOperationException("All exams in a session must belong to the same course.");















                exams.Add(exam);







            }















            if (request.StartTime >= request.EndTime)







                throw new InvalidOperationException(







                    "StartTime must be earlier than EndTime.");















            if (request.StartTime < DateTime.UtcNow.AddMinutes(-5))







                throw new InvalidOperationException(







                    "StartTime cannot be in the past.");







            var sessionCode = await GenerateUniqueSessionCodeAsync();















            var session = new ExamSession







            {







                CourseId = courseId,







                ExamId = examIds[0],







                PeriodId = request.PeriodId,







                ScheduledBy = teacherId,







                SessionName = request.SessionName,







                Room = request.Room,







                StartTime = request.StartTime,







                EndTime = request.EndTime,







                Status = SessionStatus.NOT_STARTED,







                CreatedAt = DateTime.UtcNow,







                SessionCode = sessionCode,







                MaxCapacity = request.MaxCapacity ?? 50,







                CurrentEnrollment = 0







            };















            var created = await _sessionRepository.CreateAsync(session);







            await _sessionRepository.SetSessionExamsAsync(created.SessionId, examIds);







            if (exams[0].SlotTemplates != null && exams[0].SlotTemplates.Count > 0)







            {







                var slotExamsMap = new Dictionary<int, List<int>>();







                var templates = exams[0].SlotTemplates.OrderBy(t => t.SlotIndex).ToList();







                var currentSlotStart = request.StartTime;







                var slotEntities = new List<ExamSessionSlot>();















                foreach (var template in templates)







                {







                    var slotEnd = currentSlotStart.AddMinutes(template.DurationMinutes);







                    var slot = new ExamSessionSlot







                    {







                        SessionId = created.SessionId,







                        SlotIndex = template.SlotIndex,







                        SlotName = template.SlotName,







                        SlotCode = $"{created.SessionCode}-S{template.SlotIndex}-{System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000)}",







                        StartTime = currentSlotStart,







                        EndTime = slotEnd,







                        MaxCapacity = template.MaxCapacity > 0 ? template.MaxCapacity : session.MaxCapacity,







                        SlotTemplateId = template.SlotTemplateId







                    };







                    slotEntities.Add(slot);















                    if (template.TemplateExams != null && template.TemplateExams.Count > 0)







                    {







                        slotExamsMap[template.SlotIndex] = template.TemplateExams.Select(te => te.ExamPaperId).ToList();







                    }















                    currentSlotStart = slotEnd;







                }















                                if (session.EndTime != currentSlotStart)
                {
                    session.EndTime = currentSlotStart;
                    await _sessionRepository.UpdateAsync(session);
                }















                await _sessionRepository.SetSessionSlotsAsync(created.SessionId, slotEntities, slotExamsMap);
            }
            else if (request.Slots != null && request.Slots.Count > 0)
            {
                var slotExamsMap = new Dictionary<int, List<int>>();
                var slotEntities = request.Slots.OrderBy(s => s.SlotIndex).ThenBy(s => s.StartTime).Select((s, idx) =>
                {
                    var slotIndex = s.SlotIndex > 0 ? s.SlotIndex : (idx + 1);
                    if (s.AllowedExamIds != null && s.AllowedExamIds.Count > 0)
                    {
                        slotExamsMap[slotIndex] = s.AllowedExamIds;
                    }
                    return new ExamSessionSlot
                    {
                        SessionId = created.SessionId,
                        SlotIndex = slotIndex,
                        SlotName = string.IsNullOrWhiteSpace(s.SlotName) ? $"Slot {slotIndex}" : s.SlotName.Trim(),
                        SlotCode = $"{created.SessionCode}-S{slotIndex}-{RandomNumberGenerator.GetInt32(100000, 1000000)}",
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        MaxCapacity = s.MaxCapacity ?? session.MaxCapacity,
                        SlotTemplateId = s.SlotTemplateId
                    };
                }).ToList();
                await _sessionRepository.SetSessionSlotsAsync(created.SessionId, slotEntities, slotExamsMap);
            }

            await _sessionRepository.SaveChangesAsync();















            var fullSession = await _sessionRepository.GetByIdAsync(created.SessionId)







                ?? throw new InvalidOperationException("Session was created but could not be retrieved.");















            return MapToSessionDto(fullSession, exams[0]);







        });







    public async Task<IEnumerable<ExamSessionSummaryDto>> GetSessionsByTeacherAsync(int teacherId)







    {







        var sessions = await _sessionRepository.GetByTeacherIdAsync(teacherId);







        return sessions.Select(MapToSessionSummaryDto);







    }







    public async Task<ExamSessionDto> GetSessionByIdAsync(int sessionId, int teacherId)







    {







        var session = await _sessionRepository.GetByIdAsync(sessionId)







            ?? throw new KeyNotFoundException($"ExamSession with ID {sessionId} not found.");















        if (session.ScheduledBy != teacherId)







            throw new UnauthorizedAccessException(







                "You do not have permission to view this session.");















        return MapToSessionDto(session, session.Exam);







    }







    public async Task<ExamSessionDto> UpdateSessionStatusAsync(







        int sessionId, UpdateSessionStatusRequest request, int teacherId)







    {







        var session = await _sessionRepository.GetByIdAsync(sessionId)







            ?? throw new KeyNotFoundException($"ExamSession with ID {sessionId} not found.");















        if (session.ScheduledBy != teacherId)







            throw new UnauthorizedAccessException(







                "You do not have permission to update this session.");















        ValidateStatusTransition(session.Status, request.Status);















        await _sessionRepository.UpdateStatusAsync(sessionId, request.Status);







        await _sessionRepository.SaveChangesAsync();















        var updated = await _sessionRepository.GetByIdAsync(sessionId)







            ?? throw new InvalidOperationException("Session could not be retrieved after update.");















        return MapToSessionDto(updated, updated.Exam);







    }







    public Task<EnrollSessionResponse> EnrollStudentAsync(EnrollSessionRequest request, int studentId)







        => _unitOfWork.ExecuteAsync(async () =>







    {







        if (request is null || string.IsNullOrWhiteSpace(request.SessionCode))







            throw new ArgumentException("SessionCode is required.");















        var normalizedSessionCode = request.SessionCode.Trim().ToUpperInvariant();







        var (session, slot) = await _sessionRepository.GetBySessionOrSlotCodeAsync(normalizedSessionCode);







        if (session is null)







        {







            session = await _sessionRepository.GetBySessionCodeAsync(normalizedSessionCode);







        }







        if (session is null)







            throw new KeyNotFoundException("Invalid or expired Enroll Code.");















        if (session.Status != SessionStatus.NOT_STARTED && session.Status != SessionStatus.IN_PROGRESS)







            throw new InvalidOperationException("This session is not open for enrollment.");















        var now = DateTime.UtcNow;







        if (now < session.StartTime)







            throw new InvalidOperationException("Enrollment has not started yet.");







        if (now > session.EndTime)







            throw new InvalidOperationException("Enrollment has ended.");















        if (session.CurrentEnrollment >= session.MaxCapacity)







            throw new InvalidOperationException("Session has reached maximum capacity.");















        var existing = await _sessionRepository.GetEnrollmentAsync(session.SessionId, studentId);







        if (existing != null)







            throw new InvalidOperationException("You are already enrolled in this session.");















        int? assignedExamId = null;







        if (session.SessionExams != null && session.SessionExams.Count > 0)







        {







            var examIds = session.SessionExams.Select(se => se.ExamId).ToList();







            var counts = session.SessionEnrollments







                .Where(e => e.AssignedExamId.HasValue && examIds.Contains(e.AssignedExamId.Value))







                .GroupBy(e => e.AssignedExamId!.Value)







                .ToDictionary(g => g.Key, g => g.Count());















            var minCount = examIds.Select(id => counts.GetValueOrDefault(id, 0)).Min();







            var candidates = examIds.Where(id => counts.GetValueOrDefault(id, 0) == minCount).ToList();







            assignedExamId = candidates[Random.Shared.Next(candidates.Count)];







        }







        else if (session.ExamId.HasValue)







        {







            assignedExamId = session.ExamId.Value;







        }















        var enrollment = new SessionEnrollment







        {







            SessionId = session.SessionId,







            StudentId = studentId,







            AssignedExamId = assignedExamId,







            AssignedSlotId = slot?.SlotId,







            Status = SessionStatus.NOT_STARTED,







            EnrolledAt = DateTime.UtcNow







        };















        await _sessionRepository.AddEnrollmentAsync(enrollment);















        session.CurrentEnrollment++;







        await _sessionRepository.UpdateAsync(session);







        await _sessionRepository.SaveChangesAsync();















        var exam = assignedExamId.HasValue







            ? await _examRepository.GetByIdAsync(assignedExamId.Value)







            : (session.ExamId.HasValue ? await _examRepository.GetByIdAsync(session.ExamId.Value) : null);















        return new EnrollSessionResponse







        {







            SessionEnrollmentId = enrollment.SessionEnrollmentId,







            SessionId = session.SessionId,







            SessionName = session.SessionName,







            ExamTitle = exam?.Title ?? "",







            StartTime = session.StartTime,







            EndTime = session.EndTime,







            Status = session.Status?.ToString() ?? SessionStatus.NOT_STARTED.ToString()







        };







    });















    public Task<IEnumerable<StudentSessionEnrollmentDto>> GetStudentEnrollmentsAsync(







        int studentId, CancellationToken cancellationToken = default)







    {







        return _sessionRepository.GetStudentEnrollmentsAsync(studentId, cancellationToken);







    }







    public async Task<IEnumerable<SessionEnrollmentDto>> GetSessionEnrollmentsAsync(int sessionId, int teacherId)







    {







        var session = await _sessionRepository.GetByIdWithEnrollmentsAsync(sessionId)







            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");















        if (session.ScheduledBy != teacherId)







            throw new UnauthorizedAccessException("Not your session.");















        return session.SessionEnrollments.Select(e => new SessionEnrollmentDto







        {







            SessionEnrollmentId = e.SessionEnrollmentId,







            SessionId = e.SessionId,







            StudentId = e.StudentId,







            StudentName = e.Student?.FullName ?? "",







            StudentEmail = e.Student?.Email ?? "",







            Status = e.Status?.ToString() ?? "",







            EnrolledAt = e.EnrolledAt,







            StartedAt = e.StartedAt,







            CurrentQuestionIndex = e.CurrentQuestionIndex,







            TotalQuestions = e.Session?.Exam?.MaxQuestions ?? 0







        }).ToList();







    }







    public async Task<SessionMonitorDto> GetSessionMonitorAsync(int sessionId, int teacherId)







    {







        var session = await _sessionRepository.GetByIdWithEnrollmentsAsync(sessionId)







            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");















        if (session.ScheduledBy != teacherId)







            throw new UnauthorizedAccessException("Not your session.");















        var exam = session.ExamId.HasValue ? await _examRepository.GetByIdAsync(session.ExamId.Value) : null;







        if (exam == null && session.SessionExams?.Count > 0)







        {







            exam = session.SessionExams.First().Exam;







        }















        var sessionExams = session.SessionExams?.Select(se => new SessionExamSummaryDto







        {







            ExamId = se.ExamId,







            ExamCode = se.Exam?.ExamCode ?? "",







            Title = se.Exam?.Title ?? "",







            TotalQuestions = se.Exam?.ExamQuestions?.Count ?? se.Exam?.MaxQuestions ?? 0,







            DurationMinutes = se.Exam?.DurationMinutes ?? 0,







            AssignedStudentsCount = session.SessionEnrollments.Count(en => en.AssignedExamId == se.ExamId)







        }).ToList() ?? new List<SessionExamSummaryDto>();















        return new SessionMonitorDto







        {







            SessionId = session.SessionId,







            SessionName = session.SessionName,







            ExamTitle = exam?.Title ?? (sessionExams.Count > 0 ? $"{sessionExams.Count} exam papers" : ""),







            ExamCode = exam?.ExamCode ?? (sessionExams.Count > 0 ? string.Join(", ", sessionExams.Select(x => x.ExamCode)) : ""),







            SessionCode = session.SessionCode,







            StartTime = session.StartTime,







            EndTime = session.EndTime,







            Status = session.Status?.ToString() ?? "",







            MaxCapacity = session.MaxCapacity,







            CurrentEnrollment = session.CurrentEnrollment,







            Exams = sessionExams,







            Slots = session.Slots?.OrderBy(slot => slot.SlotIndex).Select(slot => new ExamSessionSlotDto







            {







                SlotId = slot.SlotId,







                SessionId = slot.SessionId,







                SlotIndex = slot.SlotIndex,







                SlotName = slot.SlotName,







                SlotCode = slot.SlotCode ?? $"{session.SessionCode}-S{slot.SlotIndex}",







                StartTime = slot.StartTime,







                EndTime = slot.EndTime,







                MaxCapacity = slot.MaxCapacity,







                SlotTemplateId = slot.SlotTemplateId,







                AssignedStudentsCount = session.SessionEnrollments.Count(en => en.AssignedSlotId == slot.SlotId),







                AllowedExams = slot.SlotExams?.Select(se => new SessionExamSummaryDto







                {







                    ExamId = se.ExamId,







                    ExamCode = se.Exam?.ExamCode ?? "",







                    Title = se.Exam?.Title ?? "",







                    DurationMinutes = se.Exam?.DurationMinutes ?? 0,







                    TotalQuestions = se.Exam?.ExamQuestions?.Count ?? se.Exam?.MaxQuestions ?? 0







                }).ToList() ?? new List<SessionExamSummaryDto>()







            }).ToList() ?? new List<ExamSessionSlotDto>(),







            Students = session.SessionEnrollments.Select(e =>







            {







                var studentResult = session.Results.FirstOrDefault(r => r.StudentId == e.StudentId);







                var answeredCount = studentResult?.Answers?.Count ?? 0;







                var studentExam = e.AssignedExam ?? (e.AssignedExamId.HasValue ? session.SessionExams?.FirstOrDefault(se => se.ExamId == e.AssignedExamId)?.Exam : null) ?? session.Exam;







                var assignedSlot = e.AssignedSlot ?? (e.AssignedSlotId.HasValue ? session.Slots?.FirstOrDefault(sl => sl.SlotId == e.AssignedSlotId.Value) : null);







                return new StudentProgressDto







                {







                    SessionEnrollmentId = e.SessionEnrollmentId,







                    StudentId = e.StudentId,







                    StudentName = e.Student?.FullName ?? "",







                    StudentEmail = e.Student?.Email ?? "",







                    EnrollmentStatus = e.Status?.ToString() ?? "",







                    EnrolledAt = e.EnrolledAt,







                    StartedAt = e.StartedAt,







                    LastActivityAt = e.LastActivityAt,







                    CurrentQuestionIndex = e.CurrentQuestionIndex ?? 0,







                    TotalQuestions = studentExam?.MaxQuestions ?? session.Exam?.MaxQuestions ?? 0,







                    AnsweredQuestions = answeredCount,







                    AssignedExamId = e.AssignedExamId,







                    AssignedExamCode = studentExam?.ExamCode,







                    AssignedExamTitle = studentExam?.Title,







                    AssignedSlotId = e.AssignedSlotId,







                    AssignedSlotName = assignedSlot?.SlotName,







                    SlotStartTime = assignedSlot?.StartTime,







                    SlotEndTime = assignedSlot?.EndTime







                };







            }).ToList()







        };







    }















    private async Task<string> GenerateUniqueSessionCodeAsync()







    {







        const int maxAttempts = 10;







        for (var attempt = 0; attempt < maxAttempts; attempt++)







        {







            var code = GenerateSessionCode();







            if (!await _sessionRepository.SessionCodeExistsAsync(code))







                return code;







        }







        throw new InvalidOperationException(







            "Unable to generate a unique Session Code after multiple attempts.");







    }















    private static string GenerateSessionCode()







    {







        var randomBytes = new byte[4];







        RandomNumberGenerator.Fill(randomBytes);







        return "ESS-" + Convert.ToHexString(randomBytes);







    }















    private static void ValidateStatusTransition(SessionStatus? current, SessionStatus target)







    {







        var allowed = current switch







        {







            SessionStatus.NOT_STARTED => new[] { SessionStatus.IN_PROGRESS, SessionStatus.CANCELLED },







            SessionStatus.IN_PROGRESS => new[] { SessionStatus.COMPLETED, SessionStatus.CANCELLED },







            SessionStatus.COMPLETED   => Array.Empty<SessionStatus>(),







            SessionStatus.CANCELLED   => Array.Empty<SessionStatus>(),







            null                      => new[] { SessionStatus.NOT_STARTED, SessionStatus.IN_PROGRESS, SessionStatus.CANCELLED },







            _ => Array.Empty<SessionStatus>()







        };















        if (!allowed.Contains(target))







        {







            throw new InvalidOperationException(







                $"Cannot transition from '{current}' to '{target}'. " +







                $"Allowed transitions: [{string.Join(", ", allowed)}].");







        }







    }















    private static ExamSessionDto MapToSessionDto(ExamSession session, Domain.Entities.Exam? exam)







    {







        var primaryExam = exam ?? session.Exam ?? session.SessionExams?.FirstOrDefault()?.Exam;







        return new ExamSessionDto







        {







            SessionId = session.SessionId,







            ExamId = session.ExamId ?? primaryExam?.ExamId,







            ExamTitle = primaryExam?.Title ?? string.Empty,







            ExamCode = primaryExam?.ExamCode ?? string.Empty,







            CourseId = session.CourseId ?? primaryExam?.CourseId,







            CourseName = session.Course?.CourseName ?? primaryExam?.Course?.CourseName ?? string.Empty,







            PeriodId = session.PeriodId,







            PeriodName = session.Period?.PeriodName,







            SessionName = session.SessionName,







            Room = session.Room,







            StartTime = session.StartTime,







            EndTime = session.EndTime,







            DurationMinutes = (int)(session.EndTime - session.StartTime).TotalMinutes,







            Status = session.Status?.ToString() ?? SessionStatus.NOT_STARTED.ToString(),







            ScheduledBy = session.ScheduledBy,







            ScheduledByName = session.ScheduledByNavigation?.FullName,







            CreatedAt = session.CreatedAt,







            EnrolledStudents = session.SessionEnrollments?.Count ?? 0,







            SessionCode = session.SessionCode,







            MaxCapacity = session.MaxCapacity,







            Exams = session.SessionExams?.Select(se => new SessionExamSummaryDto







            {







                ExamId = se.ExamId,







                ExamCode = se.Exam?.ExamCode ?? string.Empty,







                Title = se.Exam?.Title ?? string.Empty,







                DurationMinutes = se.Exam?.DurationMinutes ?? 0,







                TotalQuestions = se.Exam?.ExamQuestions?.Count ?? se.Exam?.MaxQuestions ?? 0,







                AssignedStudentsCount = session.SessionEnrollments?.Count(e => e.AssignedExamId == se.ExamId) ?? 0







            }).ToList() ?? new List<SessionExamSummaryDto>(),







            Slots = session.Slots?.OrderBy(slot => slot.SlotIndex).Select(slot => new ExamSessionSlotDto







            {







                SlotId = slot.SlotId,







                SessionId = slot.SessionId,







                SlotIndex = slot.SlotIndex,







                SlotName = slot.SlotName,







                SlotCode = slot.SlotCode ?? $"{session.SessionCode}-S{slot.SlotIndex}",







                StartTime = slot.StartTime,







                EndTime = slot.EndTime,







                MaxCapacity = slot.MaxCapacity,







                SlotTemplateId = slot.SlotTemplateId,







                AssignedStudentsCount = session.SessionEnrollments?.Count(e => e.AssignedSlotId == slot.SlotId) ?? 0,







                AllowedExams = slot.SlotExams?.Select(se => new SessionExamSummaryDto







                {







                    ExamId = se.ExamId,







                    ExamCode = se.Exam?.ExamCode ?? string.Empty,







                    Title = se.Exam?.Title ?? string.Empty,







                    DurationMinutes = se.Exam?.DurationMinutes ?? 0,







                    TotalQuestions = se.Exam?.ExamQuestions?.Count ?? se.Exam?.MaxQuestions ?? 0







                }).ToList() ?? new List<SessionExamSummaryDto>()







            }).ToList() ?? new List<ExamSessionSlotDto>()







        };







    }















    private static ExamSessionSummaryDto MapToSessionSummaryDto(ExamSession session)







    {







        var primaryExam = session.Exam ?? session.SessionExams?.FirstOrDefault()?.Exam;







        return new ExamSessionSummaryDto







        {







            SessionId = session.SessionId,







            SessionName = session.SessionName,







            ExamId = session.ExamId ?? primaryExam?.ExamId,







            ExamTitle = primaryExam?.Title ?? string.Empty,







            ExamCode = primaryExam?.ExamCode ?? string.Empty,







            CourseId = session.CourseId ?? primaryExam?.CourseId,







            CourseName = session.Course?.CourseName ?? primaryExam?.Course?.CourseName ?? string.Empty,







            PeriodId = session.PeriodId,
            PeriodName = session.Period?.PeriodName,
            Room = session.Room,







            StartTime = session.StartTime,







            EndTime = session.EndTime,







            Status = session.Status?.ToString() ?? SessionStatus.NOT_STARTED.ToString(),







            EnrolledStudents = session.SessionEnrollments?.Count ?? 0,







            Exams = session.SessionExams?.Select(se => new SessionExamSummaryDto







            {







                ExamId = se.ExamId,







                ExamCode = se.Exam?.ExamCode ?? string.Empty,







                Title = se.Exam?.Title ?? string.Empty,







                DurationMinutes = se.Exam?.DurationMinutes ?? 0,







                TotalQuestions = se.Exam?.ExamQuestions?.Count ?? se.Exam?.MaxQuestions ?? 0,







                AssignedStudentsCount = session.SessionEnrollments?.Count(e => e.AssignedExamId == se.ExamId) ?? 0







            }).ToList() ?? new List<SessionExamSummaryDto>(),







            Slots = session.Slots?.OrderBy(slot => slot.SlotIndex).Select(slot => new ExamSessionSlotDto







            {







                SlotId = slot.SlotId,







                SessionId = slot.SessionId,







                SlotIndex = slot.SlotIndex,







                SlotName = slot.SlotName,







                SlotCode = slot.SlotCode ?? $"{session.SessionCode}-S{slot.SlotIndex}",







                StartTime = slot.StartTime,







                EndTime = slot.EndTime,







                MaxCapacity = slot.MaxCapacity,







                SlotTemplateId = slot.SlotTemplateId,







                AssignedStudentsCount = session.SessionEnrollments?.Count(e => e.AssignedSlotId == slot.SlotId) ?? 0,







                AllowedExams = slot.SlotExams?.Select(se => new SessionExamSummaryDto







                {







                    ExamId = se.ExamId,







                    ExamCode = se.Exam?.ExamCode ?? string.Empty,







                    Title = se.Exam?.Title ?? string.Empty,







                    DurationMinutes = se.Exam?.DurationMinutes ?? 0,







                    TotalQuestions = se.Exam?.ExamQuestions?.Count ?? se.Exam?.MaxQuestions ?? 0







                }).ToList() ?? new List<SessionExamSummaryDto>()







            }).ToList() ?? new List<ExamSessionSlotDto>()







        };







    }







}







