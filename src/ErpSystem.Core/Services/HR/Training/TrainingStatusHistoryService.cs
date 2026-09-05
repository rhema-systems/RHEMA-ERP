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

    /// <inheritdoc />
    public async Task RecordAsync(
        string entityType,
        Guid entityId,
        string? entityReference,
        int? fromStatus,
        string? fromStatusName,
        int toStatus,
        string toStatusName,
        Guid? changedByEmployeeId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        // TenantId is set explicitly: the DbContext is registered without a tenant, so the auto-stamp
        // is inert and an unstamped row would fail the FK.
        var row = new TrainingStatusHistory
        {
            TenantId            = GetTenantId(),
            EntityType          = entityType,
            EntityId            = entityId,
            EntityReference     = entityReference,
            FromStatus          = fromStatus,
            FromStatusName      = fromStatusName,
            ToStatus            = toStatus,
            ToStatusName        = toStatusName,
            ChangedByEmployeeId = changedByEmployeeId,
            ChangedByName       = string.IsNullOrWhiteSpace(_currentUserProvider.FullName)
                                    ? _currentUserProvider.Username
                                    : _currentUserProvider.FullName,
            ChangedAt           = DateTime.UtcNow,
            Reason              = reason,
        };

        // Staged only — see the interface remarks. The caller commits this alongside the change it describes.
        await _repository.AddAsync(row);
    }
}
