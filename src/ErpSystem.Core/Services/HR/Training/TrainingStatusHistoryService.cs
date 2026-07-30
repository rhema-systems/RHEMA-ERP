using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

public class TrainingStatusHistoryService : ITrainingStatusHistoryService
{
    private readonly IGenericRepository<TrainingStatusHistory> _repository;
    private readonly ICurrentUserProvider _currentUserProvider;

    public TrainingStatusHistoryService(
        IGenericRepository<TrainingStatusHistory> repository,
        ICurrentUserProvider currentUserProvider)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<IEnumerable<TrainingStatusHistoryDto>> GetForEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var rows = await _repository.GetQueryable()
            .Where(h => h.TenantId == tenantId && h.EntityType == entityType && h.EntityId == entityId)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync(cancellationToken);

        return rows.ToDtoList();
    }
}
