using System.Text;
using ErpSystem.Core.DTOs.Pricing;
using ErpSystem.Core.Entities.Pricing;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Pricing;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Pricing;

/// <summary>
/// Price List Line management service implementation
/// </summary>
public class PriceListLineService : IPriceListLineService
{
    private readonly IPriceListLineRepository _lineRepository;
    private readonly IPriceListChangeHistoryRepository _changeHistoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<PriceListLineService> _logger;

    public PriceListLineService(
        IPriceListLineRepository lineRepository,
        IPriceListChangeHistoryRepository changeHistoryRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<PriceListLineService> logger)
    {
        _lineRepository = lineRepository;
        _changeHistoryRepository = changeHistoryRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<IEnumerable<PriceListLineDto>> GetByPriceListAsync(Guid priceListId)
    {
        var entities = await _lineRepository.GetByPriceListIdAsync(priceListId);
        return entities.Select(MapToDto);
    }

    public async Task<PriceListLineDto?> GetByIdAsync(Guid id)
    {
        var entity = await _lineRepository.GetByIdAsync(id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<IEnumerable<ItemPriceListLineDto>> GetByInventoryItemAsync(Guid inventoryItemId)
    {
        var entities = await _lineRepository.GetByInventoryItemAsync(inventoryItemId);
        return entities.Select(line => new ItemPriceListLineDto
        {
            Id = line.Id,
            PriceListId = line.PriceListId,
            PriceListCode = line.PriceList?.PriceListCode ?? string.Empty,
            PriceListName = line.PriceList?.Name ?? string.Empty,
            PriceListType = line.PriceList?.Type ?? PriceListType.Sales,
            PriceListStatus = line.PriceList?.Status ?? PriceListStatus.Draft,
            Currency = line.PriceList?.Currency ?? "USD",
            EffectiveFrom = line.PriceList?.EffectiveFrom,
            EffectiveTo = line.PriceList?.EffectiveTo,
            InventoryItemId = line.InventoryItemId,
            ItemCode = line.InventoryItem?.ItemCode ?? string.Empty,
            ItemName = line.InventoryItem?.Name ?? string.Empty,
            ItemDescription = line.InventoryItem?.Description,
            UnitOfMeasure = line.UnitOfMeasure,
            BasePrice = line.BasePrice,
            DiscountPercent = line.DiscountPercent,
            NetPrice = line.NetPrice,
            MinQuantity = line.MinQuantity,
            MaxQuantity = line.MaxQuantity,
            SupplierItemCode = line.SupplierItemCode,
            LastPriceUpdate = line.LastPriceUpdate,
            IsActive = line.IsActive
        });
    }

    public async Task<PriceListLineDto> CreateAsync(CreatePriceListLineDto dto)
    {
        var entity = new PriceListLine
        {
            PriceListId = dto.PriceListId,
            InventoryItemId = dto.InventoryItemId,
            UnitOfMeasure = dto.UnitOfMeasure ?? "EA",
            BasePrice = dto.BasePrice,
            DiscountPercent = dto.DiscountPercent,
            NetPrice = CalculateNetPrice(dto.BasePrice, dto.DiscountPercent),
            MinQuantity = dto.MinQuantity,
            MaxQuantity = dto.MaxQuantity,
            SupplierItemCode = dto.SupplierItemCode,
            MinimumOrderQuantity = dto.MinimumOrderQuantity,
            OrderMultiple = dto.OrderMultiple,
            LeadTimeDays = dto.LeadTimeDays,
            Notes = dto.Notes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            TenantId = _currentUserProvider.TenantId,
            CreatedById = _currentUserProvider.UserId
        };

        await _lineRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created price list line for item {ItemId} in price list {PriceListId}",
            dto.InventoryItemId, dto.PriceListId);
        return MapToDto(entity);
    }

    public async Task<PriceListLineDto> UpdateAsync(Guid id, UpdatePriceListLineDto dto)
    {
        var entity = await _lineRepository.GetByIdAsync(id);
        if (entity == null)
            throw new KeyNotFoundException($"Price list line with ID {id} not found.");

        // Record price change history if price changed
        if (entity.NetPrice != CalculateNetPrice(dto.BasePrice, dto.DiscountPercent))
        {
            await RecordPriceChange(entity, dto.BasePrice, dto.DiscountPercent);
        }

        entity.BasePrice = dto.BasePrice;
        entity.DiscountPercent = dto.DiscountPercent;
        entity.NetPrice = CalculateNetPrice(dto.BasePrice, dto.DiscountPercent);
        entity.MinQuantity = dto.MinQuantity;
        entity.MaxQuantity = dto.MaxQuantity;
        entity.SupplierItemCode = dto.SupplierItemCode;
        entity.MinimumOrderQuantity = dto.MinimumOrderQuantity;
        entity.OrderMultiple = dto.OrderMultiple;
        entity.LeadTimeDays = dto.LeadTimeDays;
        entity.Notes = dto.Notes;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _lineRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _lineRepository.GetByIdAsync(id);
        if (entity == null) return false;

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _lineRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<PriceListLineDto>> BulkCreateAsync(IEnumerable<CreatePriceListLineDto> dtos)
    {
        var results = new List<PriceListLineDto>();
        foreach (var dto in dtos)
        {
            var result = await CreateAsync(dto);
            results.Add(result);
        }
        return results;
    }

    public async Task<int> BulkUpdatePricesAsync(Guid priceListId, decimal percentageChange)
    {
        var lines = await _lineRepository.GetByPriceListIdAsync(priceListId);
        var count = 0;

        foreach (var line in lines.Where(l => l.IsActive && !l.IsDeleted))
        {
            var oldPrice = line.NetPrice;
            line.BasePrice = line.BasePrice * (1 + percentageChange / 100);
            line.NetPrice = CalculateNetPrice(line.BasePrice, line.DiscountPercent);
            line.PreviousPrice = oldPrice;
            line.PriceChangePercent = percentageChange;
            line.UpdatedAt = DateTime.UtcNow;

            await _lineRepository.UpdateAsync(line);
            count++;
        }

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Bulk updated {Count} prices in price list {PriceListId} by {Percent}%",
            count, priceListId, percentageChange);
        return count;
    }

    public async Task<int> BulkDeleteByPriceListAsync(Guid priceListId)
    {
        var lines = await _lineRepository.GetByPriceListIdAsync(priceListId);
        var count = 0;

        foreach (var line in lines)
        {
            line.IsDeleted = true;
            line.DeletedAt = DateTime.UtcNow;
            await _lineRepository.UpdateAsync(line);
            count++;
        }

        await _unitOfWork.SaveChangesAsync();
        return count;
    }

    public async Task<IEnumerable<PriceListLineDto>> ImportFromCsvAsync(Guid priceListId, Stream csvStream)
    {
        var results = new List<PriceListLineDto>();
        using var reader = new StreamReader(csvStream);

        // Skip header
        await reader.ReadLineAsync();

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var values = line.Split(',');
            if (values.Length < 3) continue;

            var dto = new CreatePriceListLineDto
            {
                PriceListId = priceListId,
                InventoryItemId = Guid.Parse(values[0].Trim()),
                BasePrice = decimal.Parse(values[1].Trim()),
                DiscountPercent = values.Length > 2 ? decimal.Parse(values[2].Trim()) : 0,
                MinQuantity = values.Length > 3 ? decimal.Parse(values[3].Trim()) : 1
            };

            var result = await CreateAsync(dto);
            results.Add(result);
        }

        return results;
    }

    public async Task<byte[]> ExportToCsvAsync(Guid priceListId)
    {
        var lines = await _lineRepository.GetByPriceListIdAsync(priceListId);
        var sb = new StringBuilder();

        sb.AppendLine("InventoryItemId,BasePrice,DiscountPercent,NetPrice,MinQuantity,MaxQuantity,UOM,IsActive");

        foreach (var line in lines.Where(l => !l.IsDeleted))
        {
            sb.AppendLine($"{line.InventoryItemId},{line.BasePrice},{line.DiscountPercent},{line.NetPrice},{line.MinQuantity},{line.MaxQuantity ?? 0},{line.UnitOfMeasure},{line.IsActive}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private async Task RecordPriceChange(PriceListLine line, decimal newBasePrice, decimal newDiscountPercent)
    {
        var newNetPrice = CalculateNetPrice(newBasePrice, newDiscountPercent);
        var changePercent = line.NetPrice > 0 ? ((newNetPrice - line.NetPrice) / line.NetPrice) * 100 : 0;

        var history = new PriceListChangeHistory
        {
            PriceListLineId = line.Id,
            InventoryItemId = line.InventoryItemId,
            OldPrice = line.NetPrice,
            NewPrice = newNetPrice,
            ChangePercent = changePercent,
            ChangeType = changePercent > 0 ? "Increase" : "Decrease",
            EffectiveDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            TenantId = _currentUserProvider.TenantId,
            CreatedById = _currentUserProvider.UserId
        };

        await _changeHistoryRepository.AddAsync(history);
    }

    private static decimal CalculateNetPrice(decimal basePrice, decimal discountPercent)
    {
        return basePrice * (1 - discountPercent / 100);
    }

    private static PriceListLineDto MapToDto(PriceListLine entity)
    {
        return new PriceListLineDto
        {
            Id = entity.Id,
            PriceListId = entity.PriceListId,
            InventoryItemId = entity.InventoryItemId,
            ItemCode = entity.InventoryItem?.ItemCode ?? string.Empty,
            ItemName = entity.InventoryItem?.Name ?? string.Empty,
            ItemDescription = entity.InventoryItem?.Description,
            UnitOfMeasure = entity.UnitOfMeasure,
            BasePrice = entity.BasePrice,
            DiscountPercent = entity.DiscountPercent,
            NetPrice = entity.NetPrice,
            MinQuantity = entity.MinQuantity,
            MaxQuantity = entity.MaxQuantity,
            SupplierItemCode = entity.SupplierItemCode,
            MinimumOrderQuantity = entity.MinimumOrderQuantity,
            OrderMultiple = entity.OrderMultiple,
            LeadTimeDays = entity.LeadTimeDays,
            PreviousPrice = entity.PreviousPrice,
            PriceChangePercent = entity.PriceChangePercent,
            Notes = entity.Notes,
            IsActive = entity.IsActive
        };
    }
}

