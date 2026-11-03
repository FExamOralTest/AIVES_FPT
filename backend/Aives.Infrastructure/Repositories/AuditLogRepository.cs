using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aives.Application.Interfaces.Repositories;
using Aives.Domain.Entities;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AivesDbContext _context;

    public AuditLogRepository(AivesDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(AuditLog log)
    {
        await _context.AuditLogs.AddAsync(log);
        await _context.SaveChangesAsync();
    }

    public async Task LogAsync(
        string entityName,
        int entityId,
        string action,
        string? oldValue,
        string? newValue,
        string? reason,
        int? performedBy,
        string? ipAddress = null)
    {
        var log = new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = reason,
            PerformedBy = performedBy,
            PerformedAt = DateTime.UtcNow,
            IpAddress = ipAddress
        };

        await _context.AuditLogs.AddAsync(log);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetLogsForEntityAsync(string entityName, int entityId)
    {
        return await _context.AuditLogs
            .Include(a => a.PerformedByNavigation)
            .Where(a => a.EntityName == entityName && a.EntityId == entityId)
            .OrderByDescending(a => a.PerformedAt)
            .ToListAsync();
    }
}
