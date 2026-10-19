using System.Collections.Generic;
using System.Threading.Tasks;
using Aives.Domain.Entities;

namespace Aives.Application.Interfaces.Repositories;

public interface IAuditLogRepository
{
    Task LogAsync(AuditLog log);
    Task LogAsync(string entityName, int entityId, string action, string? oldValue, string? newValue, string? reason, int? performedBy, string? ipAddress = null);
    Task<IEnumerable<AuditLog>> GetLogsForEntityAsync(string entityName, int entityId);
}
