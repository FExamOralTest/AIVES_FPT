using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Exams.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Moq;
using Xunit;

namespace Aives.Tests;

public class ExamSessionServiceTests
{
    [Fact]
    public async Task EnrollStudent_RejectsEnrollmentBeforeSessionStart()
    {
        var sessionRepository = new Mock<IExamSessionRepository>();
        var examRepository = new Mock<IExamRepository>();
        var unitOfWork = ImmediateUnitOfWork();
        var session = NewSession(DateTime.UtcNow.AddMinutes(10), DateTime.UtcNow.AddHours(1));

        sessionRepository.Setup(r => r.GetBySessionCodeAsync("ESS-ABCD1234"))
            .ReturnsAsync(session);

        var service = new ExamSessionService(sessionRepository.Object, examRepository.Object, unitOfWork.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EnrollStudentAsync(new EnrollSessionRequest { SessionCode = " ess-abcd1234 " }, 7));

        Assert.Equal("Enrollment has not started yet.", exception.Message);
        sessionRepository.Verify(r => r.AddEnrollmentAsync(It.IsAny<SessionEnrollment>()), Times.Never);
    }

    [Fact]
    public async Task EnrollStudent_UsesStudentFromServiceArgumentAndCreatesEnrollment()
    {
        var sessionRepository = new Mock<IExamSessionRepository>();
        var examRepository = new Mock<IExamRepository>();
        var unitOfWork = ImmediateUnitOfWork();
        var session = NewSession(DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(1));
        var examId = session.ExamId!.Value;
        var exam = new Exam { ExamId = examId, Title = "OOP Viva" };

        sessionRepository.Setup(r => r.GetBySessionCodeAsync("ESS-ABCD1234"))
            .ReturnsAsync(session);
        sessionRepository.Setup(r => r.GetEnrollmentAsync(session.SessionId, 7))
            .ReturnsAsync((SessionEnrollment?)null);
        examRepository.Setup(r => r.GetByIdAsync(examId)).ReturnsAsync(exam);

        var service = new ExamSessionService(sessionRepository.Object, examRepository.Object, unitOfWork.Object);

        var result = await service.EnrollStudentAsync(
            new EnrollSessionRequest { SessionCode = " ess-abcd1234 " }, 7);

        Assert.Equal(session.SessionId, result.SessionId);
        sessionRepository.Verify(r => r.AddEnrollmentAsync(It.Is<SessionEnrollment>(e =>
            e.StudentId == 7 && e.SessionId == session.SessionId)), Times.Once);
        Assert.Equal(1, session.CurrentEnrollment);
    }

    private static ExamSession NewSession(DateTime start, DateTime end) => new()
    {
        SessionId = 12,
        ExamId = 20,
        SessionCode = "ESS-ABCD1234",
        SessionName = "Morning Viva",
        StartTime = start,
        EndTime = end,
        Status = SessionStatus.NOT_STARTED,
        MaxCapacity = 10,
        CurrentEnrollment = 0
    };

    private static Mock<IExamUnitOfWork> ImmediateUnitOfWork()
    {
        var unitOfWork = new Mock<IExamUnitOfWork>();
        unitOfWork
            .Setup(u => u.ExecuteAsync<EnrollSessionResponse>(It.IsAny<Func<Task<EnrollSessionResponse>>>()))
            .Returns((Func<Task<EnrollSessionResponse>> action) => action());
        return unitOfWork;
    }
}
