using Aives.Domain.Entities;

namespace Aives.Application.Interfaces.Repositories;

public interface IQuestionBankRepository
{
    /// <summary>
    /// Lấy ngẫu nhiên đúng số câu hỏi theo courseId và level.
    /// Dùng EF.Functions.Random() để random tại DB, tránh load toàn bộ bảng.
    /// </summary>
    Task<IEnumerable<QuestionBank>> GetRandomByLevelAsync(int courseId, int level, int count);
}
