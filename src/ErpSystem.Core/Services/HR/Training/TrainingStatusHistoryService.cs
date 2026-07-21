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

    public TrainingStatusHistoryService(IGenericRepository<TrainingStatusHistory> repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<TrainingStatusHistoryDto>> GetForEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default)
    {
        var rows = await _repository.GetQueryable()
            .Where(h => h.EntityType == entityType && h.EntityId == entityId)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync(cancellationToken);

        return rows.ToDtoList();
    }
}
