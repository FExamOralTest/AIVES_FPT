using System;

namespace Aives.Domain.Entities;

/// <summary>
/// Audit trail entity to record all critical actions, especially examiner score overrides and result publishing (BR-15).
/// </summary>
public partial class AuditLog
{
    public long LogId { get; set; }

    /// <summary>
    /// Target entity name, e.g., "Result", "Answer".
    /// </summary>
    public string EntityName { get; set; } = null!;

    /// <summary>
    /// Identifier of the target entity (e.g. ResultId, AnswerId).
    /// </summary>
    public int EntityId { get; set; }

    /// <summary>
    /// Action performed, e.g., "REVIEW_SCORE_OVERRIDE", "PUBLISH_RESULT", "RETRY_ANSWER".
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// Snapshot of old value in JSON format.
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// Snapshot of new value in JSON format.
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Reason or explanation provided for this change.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// User who performed this action.
    /// </summary>
    public int? PerformedBy { get; set; }

    /// <summary>
    /// Timestamp in UTC when the action occurred.
    /// </summary>
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Client IP address or system source if available.
    /// </summary>
    public string? IpAddress { get; set; }

    public virtual User? PerformedByNavigation { get; set; }
}
