using ErpSystem.Core.DTOs.Pricing;
using ErpSystem.Core.Entities.Pricing;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Pricing;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Pricing;

/// <summary>
/// Price List management service implementation
/// </summary>
public class PriceListService : IPriceListService
{
    private readonly IPriceListRepository _priceListRepository;
    private readonly IPriceListLineRepository _priceListLineRepository;
    private readonly IPriceListChangeHistoryRepository _changeHistoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<PriceListService> _logger;

    public PriceListService(
        IPriceListRepository priceListRepository,
        IPriceListLineRepository priceListLineRepository,
        IPriceListChangeHistoryRepository changeHistoryRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<PriceListService> logger)
    {
        _priceListRepository = priceListRepository;
        _priceListLineRepository = priceListLineRepository;
        _changeHistoryRepository = changeHistoryRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<IEnumerable<PriceListDto>> GetAllAsync()
    {
        var entities = await _priceListRepository.GetAllAsync();
        return entities.Select(MapToDto);
    }

    public async Task<PriceListDto?> GetByIdAsync(Guid id)
    {
        var entity = await _priceListRepository.GetByIdAsync(id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<PriceListDto?> GetByCodeAsync(string code)
    {
        var entity = await _priceListRepository.GetByCodeAsync(code);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<PriceListDto> CreateAsync(CreatePriceListDto dto)
    {
        // Check for duplicate code
        var existing = await _priceListRepository.GetByCodeAsync(dto.PriceListCode);
        if (existing != null)
            throw new InvalidOperationException($"Price list with code '{dto.PriceListCode}' already exists.");

        var entity = new PriceList
        {
            PriceListCode = dto.PriceListCode,
            Name = dto.Name,
            Description = dto.Description,
            Type = dto.Type,
            Currency = dto.Currency ?? "USD",
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            Status = PriceListStatus.Draft,
            ApprovalStatus = PriceListApprovalStatus.Draft,
            IsDefault = dto.IsDefault,
            Priority = dto.Priority,
            ApplicableEntityType = dto.ApplicableEntityType,
            ApplicableEntityId = dto.ApplicableEntityId,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            TenantId = _currentUserProvider.TenantId,
            CreatedById = _currentUserProvider.UserId
        };

        await _priceListRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created price list: {Code} - {Name}", entity.PriceListCode, entity.Name);
        return MapToDto(entity);
    }

    public async Task<PriceListDto> UpdateAsync(Guid id, UpdatePriceListDto dto)
    {
        var entity = await _priceListRepository.GetByIdAsync(id);
        if (entity == null)
            throw new KeyNotFoundException($"Price list with ID {id} not found.");

        entity.Name = dto.Name ?? entity.Name;
        entity.Description = dto.Description;
        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.IsDefault = dto.IsDefault;
        entity.Priority = dto.Priority;
        entity.ApplicableEntityType = dto.ApplicableEntityType;
        entity.ApplicableEntityId = dto.ApplicableEntityId;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;

        await _priceListRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated price list: {Id}", id);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _priceListRepository.GetByIdAsync(id);
        if (entity == null) return false;

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _priceListRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Deleted price list: {Id}", id);
        return true;
    }

    public async Task<IEnumerable<PriceListDto>> GetByTypeAsync(PriceListType type)
    {
        var entities = await _priceListRepository.GetActiveByTypeAsync(type);
        return entities.Select(MapToDto);
    }

    public async Task<IEnumerable<PriceListDto>> GetActiveAsync()
    {
        // Get all price lists that are Active or Draft (for dropdown selection)
        // This allows users to select price lists that are not yet fully approved
        var allEntities = await _priceListRepository.GetAllAsync();
        var activeOrDraft = allEntities.Where(p =>
            p.Status == PriceListStatus.Active ||
            p.Status == PriceListStatus.Draft);
        return activeOrDraft.Select(MapToDto);
    }

    public async Task<IEnumerable<PriceListDto>> GetByStatusAsync(PriceListStatus status)
    {
        var entities = await _priceListRepository.GetByStatusAsync(status);
        return entities.Select(MapToDto);
    }

    public async Task<IEnumerable<PriceListDto>> SearchAsync(string searchTerm)
    {
        var entities = await _priceListRepository.SearchAsync(searchTerm);
        return entities.Select(MapToDto);
    }

    public async Task<PriceListDto> SubmitForApprovalAsync(Guid id)
    {
        var entity = await _priceListRepository.GetByIdAsync(id);
        if (entity == null)
            throw new KeyNotFoundException($"Price list with ID {id} not found.");

        if (entity.ApprovalStatus != PriceListApprovalStatus.Draft)
            throw new InvalidOperationException("Only draft price lists can be submitted for approval.");

        entity.ApprovalStatus = PriceListApprovalStatus.PendingApproval;
        entity.UpdatedAt = DateTime.UtcNow;

        await _priceListRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Price list {Id} submitted for approval", id);
        return MapToDto(entity);
    }

    public async Task<PriceListDto> ApproveAsync(Guid id, Guid approvedById, string? comments = null)
    {
        var entity = await _priceListRepository.GetByIdAsync(id);
        if (entity == null)
            throw new KeyNotFoundException($"Price list with ID {id} not found.");

        if (entity.ApprovalStatus != PriceListApprovalStatus.PendingApproval)
            throw new InvalidOperationException("Only pending approval price lists can be approved.");

        entity.ApprovalStatus = PriceListApprovalStatus.Approved;
        entity.ApprovedById = approvedById;
        entity.ApprovedDate = DateTime.UtcNow;
        entity.ApprovalComments = comments;
        entity.Status = PriceListStatus.Active;
        entity.UpdatedAt = DateTime.UtcNow;

        await _priceListRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Price list {Id} approved by {ApprovedById}", id, approvedById);
        return MapToDto(entity);
    }

    public async Task<PriceListDto> RejectAsync(Guid id, Guid rejectedById, string reason)
    {
        var entity = await _priceListRepository.GetByIdAsync(id);
        if (entity == null)
            throw new KeyNotFoundException($"Price list with ID {id} not found.");

        if (entity.ApprovalStatus != PriceListApprovalStatus.PendingApproval)
            throw new InvalidOperationException("Only pending approval price lists can be rejected.");

        entity.ApprovalStatus = PriceListApprovalStatus.Rejected;
        entity.ApprovedById = rejectedById;
        entity.ApprovedDate = DateTime.UtcNow;
        entity.ApprovalComments = reason;
        entity.UpdatedAt = DateTime.UtcNow;

        await _priceListRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Price list {Id} rejected by {RejectedById}: {Reason}", id, rejectedById, reason);
        return MapToDto(entity);
    }

    public async Task<IEnumerable<PriceListDto>> GetPendingApprovalAsync()
    {
        var entities = await _priceListRepository.GetPendingApprovalAsync();
        return entities.Select(MapToDto);
    }

    public async Task<PriceListDto> ActivateAsync(Guid id)
    {
        var entity = await _priceListRepository.GetByIdAsync(id);
        if (entity == null)
            throw new KeyNotFoundException($"Price list with ID {id} not found.");

        entity.Status = PriceListStatus.Active;
        entity.UpdatedAt = DateTime.UtcNow;

        await _priceListRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<PriceListDto> DeactivateAsync(Guid id)
    {
        var entity = await _priceListRepository.GetByIdAsync(id);
        if (entity == null)
            throw new KeyNotFoundException($"Price list with ID {id} not found.");

        entity.Status = PriceListStatus.Cancelled;
        entity.UpdatedAt = DateTime.UtcNow;

        await _priceListRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<PriceListDto> CopyPriceListAsync(Guid sourcePriceListId, string newCode, string newName)
    {
        var source = await _priceListRepository.GetByIdWithLinesAsync(sourcePriceListId);
        if (source == null)
            throw new KeyNotFoundException($"Source price list with ID {sourcePriceListId} not found.");

        var newPriceList = new PriceList
        {
            PriceListCode = newCode,
            Name = newName,
            Description = source.Description,
            Type = source.Type,
            Currency = source.Currency,
            Status = PriceListStatus.Draft,
            ApprovalStatus = PriceListApprovalStatus.Draft,
            IsDefault = false,
            Priority = source.Priority,
            Notes = $"Copied from {source.PriceListCode}",
            CreatedAt = DateTime.UtcNow,
            TenantId = _currentUserProvider.TenantId,
            CreatedById = _currentUserProvider.UserId
        };

        await _priceListRepository.AddAsync(newPriceList);
        await _unitOfWork.SaveChangesAsync();

        // Copy lines
        foreach (var line in source.Lines.Where(l => !l.IsDeleted))
        {
            var newLine = new PriceListLine
            {
                PriceListId = newPriceList.Id,
                InventoryItemId = line.InventoryItemId,
                UnitOfMeasure = line.UnitOfMeasure,
                BasePrice = line.BasePrice,
                DiscountPercent = line.DiscountPercent,
                NetPrice = line.NetPrice,
                MinQuantity = line.MinQuantity,
                MaxQuantity = line.MaxQuantity,
                IsActive = line.IsActive,
                CreatedAt = DateTime.UtcNow,
                TenantId = _currentUserProvider.TenantId,
                CreatedById = _currentUserProvider.UserId
            };
            await _priceListLineRepository.AddAsync(newLine);
        }

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Copied price list {SourceId} to new price list {NewId}", sourcePriceListId, newPriceList.Id);

        return MapToDto(newPriceList);
    }

    public async Task<PriceListDto> SupersedePriceListAsync(Guid oldPriceListId, Guid newPriceListId)
    {
        var oldPriceList = await _priceListRepository.GetByIdAsync(oldPriceListId);
        var newPriceList = await _priceListRepository.GetByIdAsync(newPriceListId);

        if (oldPriceList == null)
            throw new KeyNotFoundException($"Old price list with ID {oldPriceListId} not found.");
        if (newPriceList == null)
            throw new KeyNotFoundException($"New price list with ID {newPriceListId} not found.");

        oldPriceList.Status = PriceListStatus.Superseded;
        oldPriceList.UpdatedAt = DateTime.UtcNow;

        newPriceList.SupersededPriceListId = oldPriceListId;
        newPriceList.UpdatedAt = DateTime.UtcNow;

        await _priceListRepository.UpdateAsync(oldPriceList);
        await _priceListRepository.UpdateAsync(newPriceList);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(newPriceList);
    }

    public async Task<decimal?> GetPriceForItemAsync(Guid priceListId, Guid inventoryItemId, decimal quantity = 1)
    {
        var line = await GetPriceLineForItemAsync(priceListId, inventoryItemId, quantity);
        return line?.NetPrice;
    }

    public async Task<PriceListLineDto?> GetPriceLineForItemAsync(Guid priceListId, Guid inventoryItemId, decimal quantity = 1)
    {
        var lines = await _priceListLineRepository.GetByInventoryItemAsync(inventoryItemId);
        var line = lines
            .Where(l => l.PriceListId == priceListId && l.IsActive && !l.IsDeleted)
            .Where(l => quantity >= l.MinQuantity && (l.MaxQuantity == null || quantity <= l.MaxQuantity))
            .OrderByDescending(l => l.MinQuantity)
            .FirstOrDefault();

        return line == null ? null : MapLineToDto(line);
    }

    private static PriceListDto MapToDto(PriceList entity)
    {
        return new PriceListDto
        {
            Id = entity.Id,
            PriceListCode = entity.PriceListCode,
            Name = entity.Name,
            Description = entity.Description,
            Type = entity.Type,
            Currency = entity.Currency,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            Status = entity.Status,
            ApprovalStatus = entity.ApprovalStatus,
            IsDefault = entity.IsDefault,
            Priority = entity.Priority,
            ApplicableEntityType = entity.ApplicableEntityType,
            ApplicableEntityId = entity.ApplicableEntityId,
            ApprovedById = entity.ApprovedById,
            ApprovedDate = entity.ApprovedDate,
            Version = entity.Version,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            LineCount = entity.Lines?.Count(l => !l.IsDeleted) ?? 0,
            IsEffective = entity.IsEffective
        };
    }

    private static PriceListLineDto MapLineToDto(PriceListLine entity)
    {
        return new PriceListLineDto
        {
            Id = entity.Id,
            PriceListId = entity.PriceListId,
            InventoryItemId = entity.InventoryItemId,
            UnitOfMeasure = entity.UnitOfMeasure,
            BasePrice = entity.BasePrice,
            DiscountPercent = entity.DiscountPercent,
            NetPrice = entity.NetPrice,
            MinQuantity = entity.MinQuantity,
            MaxQuantity = entity.MaxQuantity,
            LeadTimeDays = entity.LeadTimeDays,
            IsTaxInclusive = entity.IsTaxInclusive,
            RoundingRule = entity.RoundingRule,
            SupplierItemCode = entity.SupplierItemCode,
            MinimumOrderQuantity = entity.MinimumOrderQuantity,
            OrderMultiple = entity.OrderMultiple,
            LastPriceUpdate = entity.LastPriceUpdate,
            PreviousPrice = entity.PreviousPrice,
            PriceChangePercent = entity.PriceChangePercent,
            Notes = entity.Notes,
            IsActive = entity.IsActive
        };
    }
}

