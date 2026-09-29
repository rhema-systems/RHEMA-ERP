using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFinanceAuditService
{
    Task<AuditLog> RecordAsync(FinanceAuditEventDto auditEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a trusted host-worker event without impersonating a request user.
    /// technicalInitiatorUserId anchors the non-null audit FK to the source
    /// instruction's maker; Username and payload identify the actual system actor.
    /// </summary>
    Task<AuditLog> RecordSystemAsync(
        FinanceAuditEventDto auditEvent,
        Guid technicalInitiatorUserId,
        string systemActor,
        CancellationToken cancellationToken = default) =>
        RecordAsync(auditEvent, cancellationToken);

    Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(
        Guid tenantId,
        string resource,
        string resourceId,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
