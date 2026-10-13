using Aives.Application.Exams.DTOs;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Interfaces;

/// <summary>
/// Repository interface cho thao tác dữ liệu Exam.
/// Tuân thủ Dependency Inversion – Application không phụ thuộc trực tiếp vào EF Core.
/// </summary>
public interface IExamRepository
{
    /// <summary>Tạo mới một Exam.</summary>
    Task<Domain.Entities.Exam> CreateAsync(Domain.Entities.Exam exam);

    /// <summary>Lấy Exam theo ID (kèm Course và CreatedBy).</summary>
    Task<Domain.Entities.Exam?> GetByIdAsync(int examId);

    /// <summary>Lấy Exam theo ID kèm danh sách câu hỏi.</summary>
    Task<Domain.Entities.Exam?> GetByIdWithQuestionsAsync(int examId);

    /// <summary>Kiểm tra ExamCode đã tồn tại chưa.</summary>
    Task<bool> ExamCodeExistsAsync(string examCode);

    /// <summary>Lấy tất cả Exam của một Teacher.</summary>
    Task<IEnumerable<Domain.Entities.Exam>> GetByTeacherIdAsync(int teacherId);

    /// <summary>Thêm câu hỏi vào đề thi (bulk insert).</summary>
    Task AddQuestionsAsync(IEnumerable<Domain.Entities.ExamQuestion> questions);

    /// <summary>Kiểm tra Teacher có quyền chỉnh sửa Exam này không (chính là người tạo).</summary>
    Task<bool> IsOwnerAsync(int examId, int teacherId);

    /// <summary>Lưu thay đổi vào database.</summary>
    Task SaveChangesAsync();
}
