using System.Security.Cryptography;







using Aives.Application.DTOs;







using Aives.Application.Exams.DTOs;







using Aives.Application.Exams.Interfaces;







using Aives.Application.Interfaces.Repositories;







using Aives.Application.Interfaces.Services;







using Aives.Domain.Entities;







using Aives.Domain.Enums;















namespace Aives.Application.Exams.Services;















/// <summary>







/// Nghiệp vụ Staff: quản lý phòng thi, lịch thu xếp ca, xếp lớp / phân công giảng viên,







/// quản lý enrollment (bulk) và theo dõi tiến độ ca thi.







/// Admin vẫn dùng được (bypass ownership check).







/// TODO: khi có bảng `rooms` riêng → chuyển ExamSession.Room (string) thành FK int.







/// TODO: khi có bảng `classes` riêng → bật lại GetAllClassesAsync.







/// </summary>







public class StaffService : IStaffService







{







    private readonly IExamSessionRepository _sessionRepository;







    private readonly IExamRepository _examRepository;







    private readonly IExamUnitOfWork _unitOfWork;







    private readonly ICourseService _courseService;







    private readonly IUserRepository _userRepository;















    public StaffService(







        IExamSessionRepository sessionRepository,







        IExamRepository examRepository,







        IExamUnitOfWork unitOfWork,







        ICourseService courseService,







        IUserRepository userRepository)







    {







        _sessionRepository = sessionRepository;







        _examRepository = examRepository;







        _unitOfWork = unitOfWork;







        _courseService = courseService;







        _userRepository = userRepository;







    }















    // ═════════════════════════════════════════════════════════════







    //  SESSIONS / ROOMS







    // ═════════════════════════════════════════════════════════════















    public async Task<IEnumerable<ExamSessionSummaryDto>> GetAllSessionsAsync(StaffSessionQuery query, int staffId)







    {







        var sessions = await _sessionRepository.GetAllAsync(query?.Status);







        var actor = await _userRepository.GetByIdAsync(staffId)







            ?? throw new UnauthorizedAccessException("Staff account was not found.");















        if (actor.Role != UserRole.ADMIN)







            sessions = sessions.Where(s => s.Exam?.Course?.TeacherId == staffId);















        return sessions.Select(MapToSummary);







    }















    public async Task<ExamSessionDto> GetSessionByIdAsync(int sessionId, int staffId)







    {







        var session = await _sessionRepository.GetByIdAsync(sessionId)







            ?? throw new KeyNotFoundException($"ExamSession {sessionId} not found.");







        await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam);







        return MapToFull(session, session.Exam);







    }















    public Task<ExamSessionDto> CreateSessionAsync(







        StaffCreateSessionRequest request, int staffId)







        => _unitOfWork.ExecuteAsync(async () =>







        {







            if (string.IsNullOrWhiteSpace(request.SessionName))







                throw new ArgumentException("SessionName is required.");







            if (request.StartTime >= request.EndTime)







                throw new InvalidOperationException("StartTime must be earlier than EndTime.");







            if (request.StartTime < DateTime.UtcNow.AddMinutes(-5))







                throw new InvalidOperationException("StartTime cannot be in the past.");







            if (request.MaxCapacity < 1)







                throw new ArgumentException("MaxCapacity must be at least 1.");















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







                    ?? throw new KeyNotFoundException($"Exam {id} not found.");















                if (exam.ExamQuestions is null || exam.ExamQuestions.Count == 0)







                    throw new InvalidOperationException($"Exam {exam.ExamCode} has no questions. Add questions before scheduling.");















                if (courseId == 0)







                    courseId = exam.CourseId;







                else if (exam.CourseId != courseId)







                    throw new InvalidOperationException("All exams in a session must belong to the same course.");















                exams.Add(exam);







            }















            await EnsureStaffCanManageCourseAsync(examIds[0], staffId, exams[0], courseId);















            var sessionCode = await GenerateUniqueSessionCodeAsync();















            var session = new ExamSession







            {







                CourseId = courseId,







                ExamId = examIds[0],







                PeriodId = request.PeriodId,







                ScheduledBy = staffId,







                SessionName = request.SessionName.Trim(),







                Room = request.Room?.Trim(),







                StartTime = request.StartTime,







                EndTime = request.EndTime,







                MaxCapacity = request.MaxCapacity,







                CurrentEnrollment = 0,







                Status = SessionStatus.NOT_STARTED,







                SessionCode = sessionCode,







                CreatedAt = DateTime.UtcNow







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
                        SlotCode = GenerateSlotCode(created.SessionCode, template.SlotIndex),
                        StartTime = currentSlotStart,
                        EndTime = slotEnd,
                        MaxCapacity = template.MaxCapacity > 0 ? template.MaxCapacity : request.MaxCapacity,
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
                        SlotCode = GenerateSlotCode(created.SessionCode, slotIndex),
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        MaxCapacity = s.MaxCapacity ?? 10,
                        SlotTemplateId = s.SlotTemplateId
                    };
                }).ToList();
                await _sessionRepository.SetSessionSlotsAsync(created.SessionId, slotEntities, slotExamsMap);
            }







            await _sessionRepository.SaveChangesAsync();















            var full = await _sessionRepository.GetByIdAsync(created.SessionId)







                ?? throw new InvalidOperationException("Session was created but could not be retrieved.");







            return MapToFull(full, exams[0]);







        });















    public Task<ExamSessionDto> UpdateSessionAsync(







        int sessionId, StaffUpdateSessionRequest request, int staffId)







        => _unitOfWork.ExecuteAsync(async () =>







        {







            var session = await _sessionRepository.GetByIdAsync(sessionId)







                ?? throw new KeyNotFoundException($"ExamSession {sessionId} not found.");















            await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam, session.CourseId);















            if (request.SessionName is not null)







            {







                if (string.IsNullOrWhiteSpace(request.SessionName))







                    throw new ArgumentException("SessionName cannot be empty.");







                session.SessionName = request.SessionName.Trim();







            }







            if (request.Room is not null)







                session.Room = string.IsNullOrWhiteSpace(request.Room) ? null : request.Room.Trim();















            if (request.PeriodId.HasValue)







                session.PeriodId = request.PeriodId.Value > 0 ? request.PeriodId.Value : null;















            var newStart = request.StartTime ?? session.StartTime;







            var newEnd = request.EndTime ?? session.EndTime;







            if (newStart >= newEnd)







                throw new InvalidOperationException("StartTime must be earlier than EndTime.");







            session.StartTime = newStart;







            session.EndTime = newEnd;















            if (request.MaxCapacity.HasValue)







            {







                if (request.MaxCapacity.Value < session.CurrentEnrollment)







                    throw new InvalidOperationException(







                        $"MaxCapacity cannot be lower than current enrolled students ({session.CurrentEnrollment}).");







                session.MaxCapacity = request.MaxCapacity.Value;







            }















            if (request.Status.HasValue && request.Status.Value != session.Status)







                ValidateStatusTransition(session.Status, request.Status.Value);















            if (request.ExamIds != null && request.ExamIds.Count > 0)







            {







                if (session.Status != SessionStatus.NOT_STARTED)







                    throw new InvalidOperationException("Cannot change exams for a session that has already started.");















                var distinctExamIds = request.ExamIds.Distinct().ToList();







                foreach (var id in distinctExamIds)







                {







                    var exam = await _examRepository.GetByIdWithQuestionsAsync(id)







                        ?? throw new KeyNotFoundException($"Exam {id} not found.");















                    if (session.CourseId > 0 && exam.CourseId != session.CourseId)







                        throw new InvalidOperationException("All exams in a session must belong to the same course.");







                }















                await _sessionRepository.SetSessionExamsAsync(sessionId, distinctExamIds);







                session.ExamId = distinctExamIds[0];







            }















            if (request.Slots != null)







            {







                if (session.Status != SessionStatus.NOT_STARTED)







                    throw new InvalidOperationException("Cannot update time slot configuration for a session that has already started.");















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







                        SessionId = sessionId,







                        SlotIndex = slotIndex,







                        SlotName = string.IsNullOrWhiteSpace(s.SlotName) ? $"Slot {slotIndex}" : s.SlotName.Trim(),







                        SlotCode = GenerateSlotCode(session.SessionCode, slotIndex),







                        StartTime = s.StartTime,







                        EndTime = s.EndTime,







                        MaxCapacity = s.MaxCapacity ?? 10,







                        SlotTemplateId = s.SlotTemplateId







                    };







                }).ToList();







                await _sessionRepository.SetSessionSlotsAsync(sessionId, slotEntities, slotExamsMap);







            }















            await _sessionRepository.UpdateAsync(session);







            if (request.Status.HasValue)







                await _sessionRepository.UpdateStatusAsync(sessionId, request.Status.Value);







            await _sessionRepository.SaveChangesAsync();















            var updated = await _sessionRepository.GetByIdAsync(sessionId)







                ?? throw new InvalidOperationException("Session could not be retrieved after update.");







            return MapToFull(updated, updated.Exam);







        });















    public async Task DeleteSessionAsync(int sessionId, int staffId)







    {







        var session = await _sessionRepository.GetByIdAsync(sessionId)







            ?? throw new KeyNotFoundException($"ExamSession {sessionId} not found.");















        await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam);















        if (session.Status == SessionStatus.IN_PROGRESS)







            throw new InvalidOperationException("Cannot delete a session that is in progress.");







        if (session.Status == SessionStatus.COMPLETED)







            throw new InvalidOperationException("Cannot delete a completed session.");







        if (session.SessionEnrollments is { Count: > 0 })







            throw new InvalidOperationException(







                "Cannot delete a session that has enrolled students. Remove enrollments first.");















        await _sessionRepository.RemoveAsync(session);







    }















    // ═════════════════════════════════════════════════════════════







    //  CLASS / TEACHER ASSIGNMENT







    // ═════════════════════════════════════════════════════════════















    public async Task<CourseDto> AssignTeacherToClassAsync(







        int courseId, StaffAssignTeacherRequest request, int staffId)







    {







        var teacher = await _userRepository.GetByIdAsync(request.TeacherId)







            ?? throw new KeyNotFoundException($"User {request.TeacherId} not found.");







        if (teacher.Role != UserRole.STAFF)







            throw new InvalidOperationException(







                $"User {request.TeacherId} is not academic staff (role={teacher.Role}).");















        return await _courseService.AssignTeacherAsync(courseId, new AssignTeacherRequest(request.TeacherId));







    }















    // ═════════════════════════════════════════════════════════════







    //  ENROLLMENTS / MONITOR







    // ═════════════════════════════════════════════════════════════















    /// <summary>







    /// Bulk add nhiều sinh viên vào ca — 1 transaction.







    /// Hỗ trợ cả danh sách ID số (StudentIds) hoặc chuỗi định danh tự do (Identifiers: MSSV, Email, ID).







    /// Phân biệt trạng thái: added / already_enrolled / not_student / not_found.







    /// Trả về StaffBulkEnrollResponse để FE hiển thị chi tiết.







    /// </summary>







    public Task<StaffBulkEnrollResponse> AddStudentsToSessionAsync(







        int sessionId, StaffAddEnrollmentsRequest request, int staffId)







        => _unitOfWork.ExecuteAsync(async () =>







        {







            var session = await _sessionRepository.GetByIdAsync(sessionId)







                ?? throw new KeyNotFoundException($"Session {sessionId} not found.");















            await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam);















            if (session.Status == SessionStatus.COMPLETED || session.Status == SessionStatus.CANCELLED)







                throw new InvalidOperationException("Session is no longer open for enrollment.");















            var existingIds = await _sessionRepository.GetEnrolledStudentIdsAsync(sessionId);







            var results = new List<StaffEnrollResult>();







            var newEnrollments = new List<SessionEnrollment>();















            // Mode 1: Nhận danh sách Identifiers (MSSV / Username, Email, ID dạng text tự do)







            if (request.Identifiers != null && request.Identifiers.Length > 0)







            {







                var tokens = new List<string>();







                foreach (var raw in request.Identifiers)







                {







                    if (string.IsNullOrWhiteSpace(raw)) continue;







                    var parts = raw.Split(new[] { ',', ' ', '\t', '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);







                    tokens.AddRange(parts.Select(p => p.Trim()));







                }















                var distinctTokens = tokens.Distinct(StringComparer.OrdinalIgnoreCase).ToList();







                if (distinctTokens.Count == 0 && (request.StudentIds == null || request.StudentIds.Length == 0))







                    throw new ArgumentException("Identifiers is empty or invalid.");















                if (distinctTokens.Count > 0)







                {







                    var matchedUsers = await _sessionRepository.GetUsersByIdentifiersAsync(distinctTokens);







                    var usersById = matchedUsers.ToDictionary(u => u.UserId);







                    var usersByUsername = matchedUsers







                        .Where(u => !string.IsNullOrEmpty(u.Username))







                        .GroupBy(u => u.Username.ToLowerInvariant())







                        .ToDictionary(g => g.Key, g => g.First());







                    var usersByEmail = matchedUsers







                        .Where(u => !string.IsNullOrEmpty(u.Email))







                        .GroupBy(u => u.Email!.ToLowerInvariant())







                        .ToDictionary(g => g.Key, g => g.First());















                    var processedUserIds = new HashSet<int>();















                    foreach (var token in distinctTokens)







                    {







                        User? user = null;







                        if (int.TryParse(token, out var numId) && usersById.TryGetValue(numId, out var uById))







                        {







                            user = uById;







                        }







                        else if (usersByUsername.TryGetValue(token.ToLowerInvariant(), out var uByUsername))







                        {







                            user = uByUsername;







                        }







                        else if (usersByEmail.TryGetValue(token.ToLowerInvariant(), out var uByEmail))







                        {







                            user = uByEmail;







                        }















                        if (user == null)







                        {







                            int.TryParse(token, out var fallbackId);







                            results.Add(new StaffEnrollResult







                            {







                                Identifier = token,







                                StudentId = fallbackId,







                                Status = "not_found",







                                Reason = $"User not found with identifier '{token}'."







                            });







                            continue;







                        }















                        if (user.Role != UserRole.STUDENT)







                        {







                            results.Add(new StaffEnrollResult







                            {







                                Identifier = token,







                                StudentId = user.UserId,







                                StudentCode = user.Username,







                                StudentName = user.FullName,







                                Status = "not_student",







                                Reason = $"User '{user.Username}' has role '{user.Role}', expected STUDENT."







                            });







                            continue;







                        }















                        if (existingIds.Contains(user.UserId) || processedUserIds.Contains(user.UserId))







                        {







                            results.Add(new StaffEnrollResult







                            {







                                Identifier = token,







                                StudentId = user.UserId,







                                StudentCode = user.Username,







                                StudentName = user.FullName,







                                Status = "already_enrolled",







                                Reason = "Student is already enrolled in this session."







                            });







                            continue;







                        }















                        processedUserIds.Add(user.UserId);







                        newEnrollments.Add(new SessionEnrollment







                        {







                            SessionId = sessionId,







                            StudentId = user.UserId,







                            Status = SessionStatus.NOT_STARTED,







                            EnrolledAt = DateTime.UtcNow







                        });







                        results.Add(new StaffEnrollResult







                        {







                            Identifier = token,







                            StudentId = user.UserId,







                            StudentCode = user.Username,







                            StudentName = user.FullName,







                            Status = "added"







                        });







                    }







                }







            }







            else







            {







                // Mode 2: Nhận danh sách StudentIds (chuẩn cũ)







                if (request.StudentIds == null || request.StudentIds.Length == 0)







                    throw new ArgumentException("StudentIds is empty or invalid.");















                var distinctIds = request.StudentIds







                    .Where(id => id > 0)







                    .Distinct()







                    .ToList();







                if (distinctIds.Count == 0)







                    throw new ArgumentException("StudentIds is empty or invalid.");















                // 1. Check capacity trước (fail-fast)







                var toAddCount = distinctIds.Count(id => !existingIds.Contains(id));







                if (session.CurrentEnrollment + toAddCount > session.MaxCapacity)







                    throw new InvalidOperationException(







                        $"Adding {toAddCount} new student(s) would exceed MaxCapacity ({session.MaxCapacity}). " +







                        $"Current: {session.CurrentEnrollment}.");















                // 2. Bulk lookup user







                var users = await _sessionRepository.GetUsersByIdsAsync(distinctIds);







                var usersById = users.ToDictionary(u => u.UserId);















                foreach (var studentId in distinctIds)







                {







                    if (existingIds.Contains(studentId))







                    {







                        results.Add(new StaffEnrollResult







                        {







                            StudentId = studentId,







                            Status = "already_enrolled",







                            Reason = "Student is already enrolled in this session."







                        });







                        continue;







                    }















                    if (!usersById.TryGetValue(studentId, out var user))







                    {







                        results.Add(new StaffEnrollResult







                        {







                            StudentId = studentId,







                            Status = "not_found",







                            Reason = "User does not exist."







                        });







                        continue;







                    }















                    if (user.Role != UserRole.STUDENT)







                    {







                        results.Add(new StaffEnrollResult







                        {







                            StudentId = studentId,







                            StudentCode = user.Username,







                            StudentName = user.FullName,







                            Status = "not_student",







                            Reason = $"User has role '{user.Role}', not STUDENT."







                        });







                        continue;







                    }















                    var enrollment = new SessionEnrollment







                    {







                        SessionId = sessionId,







                        StudentId = studentId,







                        Status = SessionStatus.NOT_STARTED,







                        EnrolledAt = DateTime.UtcNow







                    };







                    newEnrollments.Add(enrollment);







                    results.Add(new StaffEnrollResult







                    {







                        StudentId = studentId,







                        StudentCode = user.Username,







                        StudentName = user.FullName,







                        Status = "added",







                        SessionEnrollmentId = null







                    });







                }







            }















            // Kiểm tra capacity sau khi lọc







            if (session.CurrentEnrollment + newEnrollments.Count > session.MaxCapacity)







            {







                throw new InvalidOperationException(







                    $"Adding {newEnrollments.Count} students would exceed MaxCapacity " +







                    $"({session.CurrentEnrollment}/{session.MaxCapacity}).");







            }















            // Chỉ thêm enrollment khi có sinh viên hợp lệ







            if (newEnrollments.Count > 0)







            {







                var examIds = session.SessionExams?.Select(se => se.ExamId).ToList() ?? new List<int>();







                if (examIds.Count == 0 && session.ExamId.HasValue)







                    examIds.Add(session.ExamId.Value);















                if (examIds.Count > 0)







                {







                    for (int i = 0; i < newEnrollments.Count; i++)







                    {







                        newEnrollments[i].AssignedExamId = examIds[i % examIds.Count];







                    }







                }















                await _sessionRepository.AddEnrollmentsRangeAsync(newEnrollments);















                var addedByStudentId = newEnrollments







                    .Where(e => e.SessionEnrollmentId > 0)







                    .ToDictionary(e => e.StudentId, e => e.SessionEnrollmentId);















                foreach (var r in results.Where(r => r.Status == "added"))







                {







                    r.SessionEnrollmentId =







                        addedByStudentId.GetValueOrDefault(r.StudentId);







                }















                session.CurrentEnrollment += newEnrollments.Count;







                await _sessionRepository.UpdateAsync(session);







            }















            await _sessionRepository.SaveChangesAsync();















            return new StaffBulkEnrollResponse







            {







                SessionId = sessionId,







                AddedCount = newEnrollments.Count,







                SkippedCount = results.Count(r => r.Status != "added"),







                CurrentEnrollment = session.CurrentEnrollment,







                Results = results







            };







        });















    public Task<StaffBulkEnrollResponse> ImportCourseStudentsToSessionAsync(int sessionId, int staffId)







        => _unitOfWork.ExecuteAsync(async () =>







        {







            var session = await _sessionRepository.GetByIdAsync(sessionId)







                ?? throw new KeyNotFoundException($"Session {sessionId} not found.");















            await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam);















            if (session.Status == SessionStatus.COMPLETED || session.Status == SessionStatus.CANCELLED)







                throw new InvalidOperationException("Session has ended or was cancelled; cannot add students.");















            var courseId = session.CourseId ?? session.Exam?.CourseId;







            if (!courseId.HasValue || courseId.Value <= 0)







            {







                throw new InvalidOperationException("Session is not linked to any course to retrieve enrolled students.");







            }















            var studentIds = await _sessionRepository.GetEnrolledStudentIdsByCourseIdAsync(courseId.Value);







            if (studentIds.Count == 0)







            {







                return new StaffBulkEnrollResponse







                {







                    SessionId = sessionId,







                    AddedCount = 0,







                    SkippedCount = 0,







                    CurrentEnrollment = session.CurrentEnrollment,







                    Results = new List<StaffEnrollResult>()







                };







            }















            return await AddStudentsToSessionAsync(sessionId, new StaffAddEnrollmentsRequest







            {







                StudentIds = studentIds.ToArray()







            }, staffId);







        });















    public async Task RemoveStudentFromSessionAsync(int sessionId, int studentId, int staffId)







    {







        var session = await _sessionRepository.GetByIdWithEnrollmentsAsync(sessionId)







            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");















        if (session.Status == SessionStatus.IN_PROGRESS)







            throw new InvalidOperationException(







                "Cannot remove a student from a session that is in progress.");















        var enrollment = await _sessionRepository.GetEnrollmentAsync(sessionId, studentId)







            ?? throw new KeyNotFoundException(







                $"Student {studentId} is not enrolled in session {sessionId}.");















        var toRemove = new[] { enrollment };







        foreach (var e in toRemove)







            await _sessionRepository.RemoveEnrollmentAsync(e);







        if (session.CurrentEnrollment > 0) session.CurrentEnrollment--;







        await _sessionRepository.UpdateAsync(session);







        await _sessionRepository.SaveChangesAsync();







    }















    public async Task<SessionMonitorDto> GetSessionMonitorAsync(int sessionId, int staffId)







    {







        var session = await _sessionRepository.GetByIdWithEnrollmentsAsync(sessionId)







            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");















        await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam, session.CourseId);















        var primaryExam = session.Exam ?? session.SessionExams?.FirstOrDefault()?.Exam;















        return new SessionMonitorDto







        {







            SessionId = session.SessionId,







            SessionName = session.SessionName,







            ExamTitle = primaryExam?.Title ?? string.Empty,







            ExamCode = primaryExam?.ExamCode ?? string.Empty,







            SessionCode = session.SessionCode,







            StartTime = session.StartTime,







            EndTime = session.EndTime,







            Status = session.Status?.ToString() ?? string.Empty,







            MaxCapacity = session.MaxCapacity,







            CurrentEnrollment = session.CurrentEnrollment,







            Exams = session.SessionExams?.Select(se => new SessionExamSummaryDto







            {







                ExamId = se.ExamId,







                ExamCode = se.Exam?.ExamCode ?? string.Empty,







                Title = se.Exam?.Title ?? string.Empty,







                DurationMinutes = se.Exam?.DurationMinutes ?? 0,







                TotalQuestions = se.Exam?.ExamQuestions?.Count ?? se.Exam?.MaxQuestions ?? 0,







                AssignedStudentsCount = session.SessionEnrollments.Count(e => e.AssignedExamId == se.ExamId)







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







                AssignedStudentsCount = session.SessionEnrollments.Count(e => e.AssignedSlotId == slot.SlotId),







                AllowedExams = slot.SlotExams?.Select(se => new SessionExamSummaryDto







                {







                    ExamId = se.ExamId,







                    ExamCode = se.Exam?.ExamCode ?? string.Empty,







                    Title = se.Exam?.Title ?? string.Empty,







                    DurationMinutes = se.Exam?.DurationMinutes ?? 0,







                    TotalQuestions = se.Exam?.ExamQuestions?.Count ?? se.Exam?.MaxQuestions ?? 0







                }).ToList() ?? new List<SessionExamSummaryDto>()







            }).ToList() ?? new List<ExamSessionSlotDto>(),







            Students = session.SessionEnrollments.Select(e =>







            {







                var studentResult = session.Results.FirstOrDefault(r => r.StudentId == e.StudentId);







                var answeredCount = studentResult?.Answers?.Count ?? 0;







                var assignedExam = e.AssignedExam ?? (e.AssignedExamId.HasValue ? session.SessionExams?.FirstOrDefault(se => se.ExamId == e.AssignedExamId.Value)?.Exam : primaryExam);







                var assignedSlot = e.AssignedSlot ?? (e.AssignedSlotId.HasValue ? session.Slots?.FirstOrDefault(sl => sl.SlotId == e.AssignedSlotId.Value) : null);







                return new StudentProgressDto







                {







                    SessionEnrollmentId = e.SessionEnrollmentId,







                    StudentId = e.StudentId,







                    StudentName = e.Student?.FullName ?? string.Empty,







                    StudentEmail = e.Student?.Email ?? string.Empty,







                    EnrollmentStatus = e.Status?.ToString() ?? string.Empty,







                    EnrolledAt = e.EnrolledAt,







                    StartedAt = e.StartedAt,







                    LastActivityAt = e.LastActivityAt,







                    AssignedExamId = e.AssignedExamId ?? assignedExam?.ExamId,







                    AssignedExamCode = assignedExam?.ExamCode,







                    AssignedExamTitle = assignedExam?.Title,







                    AssignedSlotId = e.AssignedSlotId,







                    AssignedSlotName = assignedSlot?.SlotName,







                    SlotStartTime = assignedSlot?.StartTime,







                    SlotEndTime = assignedSlot?.EndTime,







                    CurrentQuestionIndex = e.CurrentQuestionIndex ?? 0,







                    TotalQuestions = assignedExam?.ExamQuestions?.Count ?? assignedExam?.MaxQuestions ?? primaryExam?.MaxQuestions ?? 0,







                    AnsweredQuestions = answeredCount







                };







            }).ToList()







        };







    }















    public async Task<DistributeExamsResponse> DistributeExamsAsync(







        int sessionId, DistributeExamsRequest request, int staffId)







    {







        var session = await _sessionRepository.GetByIdWithEnrollmentsAsync(sessionId)







            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");















        await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam, session.CourseId);















        if (session.Status != SessionStatus.NOT_STARTED)







            throw new InvalidOperationException("Can only distribute exams when session is NOT_STARTED.");















        if (session.SessionEnrollments.Any(e => e.StartedAt != null || e.Status == SessionStatus.IN_PROGRESS || e.Status == SessionStatus.COMPLETED))







            throw new InvalidOperationException("Cannot re-distribute exams after students have started taking the test.");















        var exams = session.SessionExams?.Select(se => se.Exam).Where(e => e != null).ToList() ?? new List<Exam>();







        if (exams.Count == 0 && session.Exam != null)







            exams.Add(session.Exam);







        else if (exams.Count == 0 && session.ExamId.HasValue)







        {







            var fallbackExam = await _examRepository.GetByIdAsync(session.ExamId.Value);







            if (fallbackExam != null) exams.Add(fallbackExam);







        }















        if (exams.Count == 0)







            throw new InvalidOperationException("Session does not have any configured exams.");















        var enrollments = session.SessionEnrollments.ToList();







        if (enrollments.Count == 0)







        {







            return new DistributeExamsResponse







            {







                SessionId = sessionId,







                TotalStudents = 0,







                TotalExams = exams.Count,







                Allocations = exams.Select(e => new ExamAllocationCountDto







                {







                    ExamId = e.ExamId,







                    ExamCode = e.ExamCode,







                    Title = e.Title,







                    StudentCount = 0







                }).ToList()







            };







        }















        // Fisher-Yates Balanced Random Shuffle







        var rng = request?.RandomSeed.HasValue == true ? new Random(request.RandomSeed.Value) : new Random();







        var shuffled = enrollments.OrderBy(_ => rng.Next()).ToList();















        for (int i = 0; i < shuffled.Count; i++)







        {







            var assigned = exams[i % exams.Count];







            shuffled[i].AssignedExamId = assigned.ExamId;







        }















        await _sessionRepository.BulkUpdateEnrollmentsAsync(shuffled);















        return new DistributeExamsResponse







        {







            SessionId = sessionId,







            TotalStudents = enrollments.Count,







            TotalExams = exams.Count,







            Allocations = exams.Select(e => new ExamAllocationCountDto







            {







                ExamId = e.ExamId,







                ExamCode = e.ExamCode,







                Title = e.Title,







                StudentCount = shuffled.Count(s => s.AssignedExamId == e.ExamId)







            }).ToList()







        };







    }















    public async Task AssignStudentExamAsync(







        int sessionId, int studentId, StaffAssignExamRequest request, int staffId)







    {







        var session = await _sessionRepository.GetByIdWithEnrollmentsAsync(sessionId)







            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");















        await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam, session.CourseId);















        if (session.Status != SessionStatus.NOT_STARTED)







            throw new InvalidOperationException("Can only adjust assigned exam when session is NOT_STARTED.");















        var enrollment = session.SessionEnrollments.FirstOrDefault(e => e.StudentId == studentId)







            ?? throw new KeyNotFoundException($"Student {studentId} is not enrolled in session {sessionId}.");















        if (enrollment.StartedAt != null || enrollment.Status == SessionStatus.IN_PROGRESS)







            throw new InvalidOperationException("Cannot change exam for a student who has already started.");















        var validExamIds = session.SessionExams?.Select(se => se.ExamId).ToList() ?? new List<int>();







        if (session.ExamId.HasValue && !validExamIds.Contains(session.ExamId.Value))







            validExamIds.Add(session.ExamId.Value);















        if (!validExamIds.Contains(request.ExamId))







            throw new ArgumentException($"Exam ID {request.ExamId} is not configured for this session.");















        await _sessionRepository.UpdateEnrollmentAssignedExamAsync(sessionId, studentId, request.ExamId);







    }















    public async Task<DistributeSlotsResponse> DistributeSlotsAsync(







        int sessionId, DistributeSlotsRequest request, int staffId)







    {







        var session = await _sessionRepository.GetByIdWithEnrollmentsAsync(sessionId)







            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");















        await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam, session.CourseId);















        if (session.Status != SessionStatus.NOT_STARTED)







            throw new InvalidOperationException("Can only distribute time slots when session is NOT_STARTED.");















        var slots = session.Slots?.OrderBy(s => s.SlotIndex).ThenBy(s => s.StartTime).ToList() ?? new List<ExamSessionSlot>();







        if (slots.Count == 0)







            throw new InvalidOperationException("Session has no time slots configured. Please configure time slots before distributing.");















        var enrollments = session.SessionEnrollments.ToList();







        if (enrollments.Count == 0)







        {







            return new DistributeSlotsResponse







            {







                SessionId = sessionId,







                TotalStudents = 0,







                TotalSlots = slots.Count,







                Allocations = slots.Select(slot => new SlotAllocationCountDto







                {







                    SlotId = slot.SlotId,







                    SlotIndex = slot.SlotIndex,







                    SlotName = slot.SlotName,







                    StartTime = slot.StartTime,







                    EndTime = slot.EndTime,







                    StudentCount = 0







                }).ToList()







            };







        }















        // Available fallback exams for the session







        var sessionExams = session.SessionExams?.Select(se => se.Exam).Where(e => e != null).ToList() ?? new List<Exam>();







        if (sessionExams.Count == 0 && session.Exam != null)







            sessionExams.Add(session.Exam);















        // Edge Case 4: Do not overwrite slots/exams of students who have started, completed, or have answers/scores







        var activeStudentIds = session.Results







            .Where(r => (r.Answers != null && r.Answers.Count > 0) || r.FinalScore != null || r.AiScore != null || r.SubmittedAt != null)







            .Select(r => r.StudentId)







            .ToHashSet();















        var eligibleStudents = enrollments







            .Where(e => e.StartedAt == null







                     && e.Status == SessionStatus.NOT_STARTED







                     && !activeStudentIds.Contains(e.StudentId))







            .ToList();















        // Locked student counts by slot







        var lockedCountsBySlot = enrollments







            .Where(e => !eligibleStudents.Contains(e) && e.AssignedSlotId.HasValue)







            .GroupBy(e => e.AssignedSlotId!.Value)







            .ToDictionary(g => g.Key, g => g.Count());















        var rng = request?.RandomSeed.HasValue == true ? new Random(request.RandomSeed.Value) : new Random();







        var shuffledStudents = eligibleStudents.OrderBy(_ => rng.Next()).ToList();















        // Balanced round-robin slot assignment: student i assigned to slots[i % slots.Count]







        for (int i = 0; i < shuffledStudents.Count; i++)







        {







            var assignedSlot = slots[i % slots.Count];







            shuffledStudents[i].AssignedSlotId = assignedSlot.SlotId;







        }















        // Edge Case 5: MaxCapacity overflow handling: raise slot.MaxCapacity if assigned count exceeds capacity







        bool slotCapacityUpdated = false;







        foreach (var slot in slots)







        {







            int newlyAssigned = shuffledStudents.Count(s => s.AssignedSlotId == slot.SlotId);







            int locked = lockedCountsBySlot.GetValueOrDefault(slot.SlotId, 0);







            int totalInSlot = newlyAssigned + locked;







            if (totalInSlot > slot.MaxCapacity)







            {







                slot.MaxCapacity = totalInSlot;







                slotCapacityUpdated = true;







            }







        }







        if (slotCapacityUpdated)







        {







            await _sessionRepository.UpdateAsync(session);







        }















        // Exam assignment per slot (Edge Case 3: Fallback when SlotExams is empty)







        var mode = request?.ExamAssignmentMode?.ToUpperInvariant() ?? "EXAM_PER_SLOT";















        foreach (var slot in slots)







        {







            var slotStudents = shuffledStudents







                .Where(s => s.AssignedSlotId == slot.SlotId)







                .OrderBy(_ => rng.Next())







                .ToList();















            if (slotStudents.Count == 0) continue;















            // Get allowed exam IDs for this slot







            var allowedExamIds = slot.SlotExams?







                .Select(se => se.ExamId)







                .Distinct()







                .ToList() ?? new List<int>();















            // Fallback: If slot has no specific exams configured, fallback to session exams or primary ExamId







            if (allowedExamIds.Count == 0)







            {







                allowedExamIds = sessionExams.Select(e => e.ExamId).Distinct().ToList();







            }







            if (allowedExamIds.Count == 0 && session.ExamId.HasValue)







            {







                allowedExamIds.Add(session.ExamId.Value);







            }















            if (allowedExamIds.Count == 0)







            {







                continue; // No exam available anywhere







            }















            if (mode == "SHUFFLE_IN_SLOT")







            {







                for (int j = 0; j < slotStudents.Count; j++)







                {







                    slotStudents[j].AssignedExamId = allowedExamIds[j % allowedExamIds.Count];







                }







            }







            else // EXAM_PER_SLOT







            {







                var chosenExamId = allowedExamIds[(slot.SlotIndex - 1) % allowedExamIds.Count];







                foreach (var student in slotStudents)







                {







                    student.AssignedExamId = chosenExamId;







                }







            }







        }















        await _sessionRepository.BulkUpdateEnrollmentsAsync(shuffledStudents);















        return new DistributeSlotsResponse







        {







            SessionId = sessionId,







            TotalStudents = enrollments.Count,







            TotalSlots = slots.Count,







            Allocations = slots.Select(slot => new SlotAllocationCountDto







            {







                SlotId = slot.SlotId,







                SlotIndex = slot.SlotIndex,







                SlotName = slot.SlotName,







                StartTime = slot.StartTime,







                EndTime = slot.EndTime,







                StudentCount = enrollments.Count(s => s.AssignedSlotId == slot.SlotId)







            }).ToList()







        };







    }















    public async Task AssignStudentSlotAsync(







        int sessionId, int studentId, StaffAssignSlotRequest request, int staffId)







    {







        var session = await _sessionRepository.GetByIdWithEnrollmentsAsync(sessionId)







            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");















        await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam, session.CourseId);















        if (session.Status != SessionStatus.NOT_STARTED)







            throw new InvalidOperationException("Can only adjust time slot assignment when session is NOT_STARTED.");















        var enrollment = session.SessionEnrollments.FirstOrDefault(e => e.StudentId == studentId)







            ?? throw new KeyNotFoundException($"Student {studentId} is not enrolled in session {sessionId}.");















        if (enrollment.StartedAt != null || enrollment.Status == SessionStatus.IN_PROGRESS)







            throw new InvalidOperationException("Cannot change time slot for a student who has already started.");















        var validSlot = session.Slots?.FirstOrDefault(s => s.SlotId == request.SlotId)







            ?? throw new ArgumentException($"Time slot ID {request.SlotId} does not belong to this session.");















        await _sessionRepository.UpdateEnrollmentAssignedSlotAsync(sessionId, studentId, request.SlotId);







    }















    // ═════════════════════════════════════════════════════════════







    //  Helpers







    // ═════════════════════════════════════════════════════════════















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







            SessionStatus.COMPLETED => Array.Empty<SessionStatus>(),







            SessionStatus.CANCELLED => Array.Empty<SessionStatus>(),







            null => new[] { SessionStatus.NOT_STARTED, SessionStatus.IN_PROGRESS, SessionStatus.CANCELLED },







            _ => Array.Empty<SessionStatus>()







        };















        if (!allowed.Contains(target))







            throw new InvalidOperationException(







                $"Cannot transition from '{current}' to '{target}'. " +







                $"Allowed transitions: [{string.Join(", ", allowed)}].");







    }















    private async Task EnsureStaffCanManageCourseAsync(







        int? examId, int staffId, Exam? examAlreadyLoaded = null, int? courseId = null)







    {







        var actor = await _userRepository.GetByIdAsync(staffId)







            ?? throw new UnauthorizedAccessException("Staff account was not found.");















        if (actor.Role == UserRole.ADMIN)







            return;















        int targetCourseId;







        if (courseId.HasValue && courseId.Value > 0)







        {







            targetCourseId = courseId.Value;







        }







        else







        {







            var exam = examAlreadyLoaded







                ?? (examId.HasValue ? await _examRepository.GetByIdAsync(examId.Value) : null)







                ?? throw new KeyNotFoundException($"Exam {examId} not found.");







            targetCourseId = exam.CourseId;







        }















        var course = await _courseService.GetByIdAsync(targetCourseId);







        if ((actor.Role != UserRole.STAFF && actor.Role != UserRole.EXAMINER) || course.TeacherId != staffId)







            throw new UnauthorizedAccessException(







                "You are not assigned to the course of this exam.");







    }















    private static ExamSessionDto MapToFull(ExamSession s, Exam? exam)







    {







        var primaryExam = exam ?? s.Exam ?? s.SessionExams?.FirstOrDefault()?.Exam;







        return new ExamSessionDto







        {







            SessionId = s.SessionId,







            ExamId = s.ExamId ?? primaryExam?.ExamId,







            ExamTitle = primaryExam?.Title ?? string.Empty,







            ExamCode = primaryExam?.ExamCode ?? string.Empty,







            CourseId = s.CourseId ?? primaryExam?.CourseId,







            CourseName = s.Course?.CourseName ?? primaryExam?.Course?.CourseName ?? string.Empty,







            PeriodId = s.PeriodId,







            PeriodName = s.Period?.PeriodName,







            SessionName = s.SessionName,







            Room = s.Room,







            StartTime = s.StartTime,







            EndTime = s.EndTime,







            DurationMinutes = (int)(s.EndTime - s.StartTime).TotalMinutes,







            Status = s.Status?.ToString() ?? SessionStatus.NOT_STARTED.ToString(),







            ScheduledBy = s.ScheduledBy,







            ScheduledByName = s.ScheduledByNavigation?.FullName,







            CreatedAt = s.CreatedAt,







            EnrolledStudents = s.SessionEnrollments?.Count ?? 0,







            SessionCode = s.SessionCode,







            MaxCapacity = s.MaxCapacity,







            Exams = s.SessionExams?.Select(se => new SessionExamSummaryDto







            {







                ExamId = se.ExamId,







                ExamCode = se.Exam?.ExamCode ?? string.Empty,







                Title = se.Exam?.Title ?? string.Empty,







                DurationMinutes = se.Exam?.DurationMinutes ?? 0,







                TotalQuestions = se.Exam?.ExamQuestions?.Count ?? se.Exam?.MaxQuestions ?? 0,







                AssignedStudentsCount = s.SessionEnrollments?.Count(e => e.AssignedExamId == se.ExamId) ?? 0







            }).ToList() ?? new List<SessionExamSummaryDto>(),







            Slots = s.Slots?.OrderBy(slot => slot.SlotIndex).Select(slot => new ExamSessionSlotDto







            {







                SlotId = slot.SlotId,







                SessionId = slot.SessionId,







                SlotIndex = slot.SlotIndex,







                SlotName = slot.SlotName,







                SlotCode = slot.SlotCode ?? $"{s.SessionCode}-S{slot.SlotIndex}",







                StartTime = slot.StartTime,







                EndTime = slot.EndTime,







                MaxCapacity = slot.MaxCapacity,







                SlotTemplateId = slot.SlotTemplateId,







                AssignedStudentsCount = s.SessionEnrollments?.Count(e => e.AssignedSlotId == slot.SlotId) ?? 0,







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















    private static ExamSessionSummaryDto MapToSummary(ExamSession s)







    {







        var primaryExam = s.Exam ?? s.SessionExams?.FirstOrDefault()?.Exam;







        return new ExamSessionSummaryDto







        {







            SessionId = s.SessionId,







            SessionName = s.SessionName,







            ExamId = s.ExamId ?? primaryExam?.ExamId,







            ExamTitle = primaryExam?.Title ?? string.Empty,







            ExamCode = primaryExam?.ExamCode ?? string.Empty,







            CourseId = s.CourseId ?? primaryExam?.CourseId,







            CourseName = s.Course?.CourseName ?? primaryExam?.Course?.CourseName ?? string.Empty,







            PeriodId = s.PeriodId,
            PeriodName = s.Period?.PeriodName,
            Room = s.Room,







            StartTime = s.StartTime,







            EndTime = s.EndTime,







            Status = s.Status?.ToString() ?? SessionStatus.NOT_STARTED.ToString(),







            EnrolledStudents = s.SessionEnrollments?.Count ?? 0,







            Exams = s.SessionExams?.Select(se => new SessionExamSummaryDto







            {







                ExamId = se.ExamId,







                ExamCode = se.Exam?.ExamCode ?? string.Empty,







                Title = se.Exam?.Title ?? string.Empty,







                DurationMinutes = se.Exam?.DurationMinutes ?? 0,







                TotalQuestions = se.Exam?.ExamQuestions?.Count ?? se.Exam?.MaxQuestions ?? 0,







                AssignedStudentsCount = s.SessionEnrollments?.Count(e => e.AssignedExamId == se.ExamId) ?? 0







            }).ToList() ?? new List<SessionExamSummaryDto>(),







            Slots = s.Slots?.OrderBy(slot => slot.SlotIndex).Select(slot => new ExamSessionSlotDto







            {







                SlotId = slot.SlotId,







                SessionId = slot.SessionId,







                SlotIndex = slot.SlotIndex,







                SlotName = slot.SlotName,







                SlotCode = slot.SlotCode ?? $"{s.SessionCode}-S{slot.SlotIndex}",







                StartTime = slot.StartTime,







                EndTime = slot.EndTime,







                MaxCapacity = slot.MaxCapacity,







                SlotTemplateId = slot.SlotTemplateId,







                AssignedStudentsCount = s.SessionEnrollments?.Count(e => e.AssignedSlotId == slot.SlotId) ?? 0,







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















    private static string GenerateSlotCode(string sessionCode, int slotIndex)







    {







        var randomDigits = RandomNumberGenerator.GetInt32(100000, 1000000);







        return $"{sessionCode}-S{slotIndex}-{randomDigits}";







    }







}







