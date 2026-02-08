using ErpSystem.Core.DTOs.Pricing;
using ErpSystem.Core.Entities.Pricing;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Pricing;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Pricing;

/// <summary>
/// Price List Change History service implementation
/// </summary>
public class PriceListChangeHistoryService : IPriceListChangeHistoryService
{
    private readonly IPriceListChangeHistoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<PriceListChangeHistoryService> _logger;

    public PriceListChangeHistoryService(
        IPriceListChangeHistoryRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<PriceListChangeHistoryService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<IEnumerable<PriceListChangeHistoryDto>> GetByInventoryItemAsync(Guid inventoryItemId)
    {
        var entities = await _repository.GetByInventoryItemAsync(inventoryItemId);
        return entities.Select(MapToDto);
    }

    public async Task<IEnumerable<PriceListChangeHistoryDto>> GetByPriceListLineAsync(Guid priceListLineId)
    {
        var entities = await _repository.GetByPriceListLineAsync(priceListLineId);
        return entities.Select(MapToDto);
    }

    public async Task<IEnumerable<PriceListChangeHistoryDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var entities = await _repository.GetByDateRangeAsync(startDate, endDate);
        return entities.Select(MapToDto);
    }

    public async Task RecordPriceChangeAsync(Guid priceListLineId, Guid inventoryItemId, decimal oldPrice, decimal newPrice, string changeType, string? reason = null)
    {
        var changePercent = oldPrice > 0 ? ((newPrice - oldPrice) / oldPrice) * 100 : 0;

        var entity = new PriceListChangeHistory
        {
            PriceListLineId = priceListLineId,
            InventoryItemId = inventoryItemId,
            OldPrice = oldPrice,
            NewPrice = newPrice,
            ChangePercent = changePercent,
            ChangeType = changeType,
            ChangeReason = reason,
            EffectiveDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            TenantId = _currentUserProvider.TenantId,
            CreatedById = _currentUserProvider.UserId
        };

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Recorded price change for item {ItemId}: {OldPrice} -> {NewPrice} ({ChangePercent:F2}%)",
            inventoryItemId, oldPrice, newPrice, changePercent);
    }

    private static PriceListChangeHistoryDto MapToDto(PriceListChangeHistory entity)
    {
        return new PriceListChangeHistoryDto
        {
            Id = entity.Id,
            PriceListLineId = entity.PriceListLineId,
            InventoryItemId = entity.InventoryItemId,
            OldPrice = entity.OldPrice,
            NewPrice = entity.NewPrice,
            ChangePercent = entity.ChangePercent,
            ChangeType = entity.ChangeType,
            ChangeReason = entity.ChangeReason,
            EffectiveDate = entity.EffectiveDate,
            CreatedAt = entity.CreatedAt
        };
    }
}

