using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFinanceAuditService
{
    Task<AuditLog> RecordAsync(FinanceAuditEventDto auditEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(
        Guid tenantId,
        string resource,
        string resourceId,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
