using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class SupplierConsolidationService : ISupplierConsolidationService
{
    private readonly ISupplierConsolidationRepository _consolidationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<SupplierConsolidationService> _logger;

    public SupplierConsolidationService(
        ISupplierConsolidationRepository consolidationRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<SupplierConsolidationService> logger)
    {
        _consolidationRepository = consolidationRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<SupplierConsolidationDetailDto?> GetByIdAsync(Guid id)
    {
        var consolidation = await _consolidationRepository.GetByIdAsync(id);
        return consolidation == null ? null : MapToDetailDto(consolidation);
    }

    public async Task<SupplierConsolidationDto?> GetByConsolidationCodeAsync(string consolidationCode)
    {
        var consolidation = await _consolidationRepository.GetByConsolidationCodeAsync(consolidationCode);
        return consolidation == null ? null : MapToDto(consolidation);
    }

    public async Task<PagedResult<SupplierConsolidationDto>> GetConsolidationsAsync(
        int page, int pageSize, string? search = null, string? status = null)
    {
        var result = await _consolidationRepository.GetConsolidationsAsync(page, pageSize, search, status, null);
        return new PagedResult<SupplierConsolidationDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<IEnumerable<SupplierConsolidationDto>> GetByItemCategoryAsync(string itemCategory)
    {
        var consolidations = await _consolidationRepository.GetByItemCategoryAsync(itemCategory);
        return consolidations.Select(MapToDto);
    }

    public async Task<IEnumerable<SupplierConsolidationDto>> GetByOpportunityLevelAsync(string opportunityLevel)
    {
        var consolidations = await _consolidationRepository.GetByOpportunityLevelAsync(opportunityLevel);
        return consolidations.Select(MapToDto);
    }

    public async Task<IEnumerable<SupplierConsolidationDto>> GetByCategoryAsync(string category)
    {
        var consolidations = await _consolidationRepository.GetByItemCategoryAsync(category);
        return consolidations.Select(MapToDto);
    }

    public async Task<IEnumerable<SupplierConsolidationDto>> GetBySupplierAsync(Guid supplierId)
    {
        // Get all consolidations and filter by preferred supplier
        var result = await _consolidationRepository.GetConsolidationsAsync(1, 1000);
        return result.Items.Where(c => c.PreferredSupplierIds?.Contains(supplierId.ToString()) == true).Select(MapToDto);
    }

    public async Task<IEnumerable<SupplierConsolidationDto>> GetActiveConsolidationsAsync()
    {
        var consolidations = await _consolidationRepository.GetByStatusAsync("Approved");
        return consolidations.Select(MapToDto);
    }

    public async Task<IEnumerable<SupplierConsolidationDto>> GetOpportunitiesAsync()
    {
        var consolidations = await _consolidationRepository.GetByOpportunityLevelAsync("High");
        return consolidations.Select(MapToDto);
    }

    public async Task<SupplierConsolidationDetailDto> CreateAsync(CreateSupplierConsolidationDto dto)
    {
        var consolidationCode = await _consolidationRepository.GenerateConsolidationCodeAsync();
        var consolidation = new SupplierConsolidation
        {
            ConsolidationCode = consolidationCode,
            Title = dto.Title,
            Description = dto.Description,
            ItemCategory = dto.ItemCategory,
            AnalysisPeriodStart = dto.AnalysisPeriodStart,
            AnalysisPeriodEnd = dto.AnalysisPeriodEnd,
            CurrentSupplierCount = dto.CurrentSupplierCount,
            RecommendedSupplierCount = dto.RecommendedSupplierCount,
            TotalSpend = dto.TotalSpend,
            PotentialSavings = dto.PotentialSavings,
            Currency = dto.Currency,
            OpportunityLevel = dto.OpportunityLevel,
            RecommendedStrategy = dto.RecommendedStrategy,
            StrategyRationale = dto.StrategyRationale,
            PreferredSupplierIds = dto.PreferredSupplierIds,
            SuppliersToPhaseOut = dto.SuppliersToPhaseOut,
            ImplementationPlan = dto.ImplementationPlan,
            Notes = dto.Notes,
            Status = "Draft",
            TenantId = _currentUserProvider.TenantId
        };

        await _consolidationRepository.AddAsync(consolidation);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Created supplier consolidation {ConsolidationCode}", consolidationCode);

        return await GetByIdAsync(consolidation.Id) ?? throw new InvalidOperationException("Failed to retrieve created consolidation");
    }

    public async Task<SupplierConsolidationDetailDto> UpdateAsync(Guid id, CreateSupplierConsolidationDto dto)
    {
        var consolidation = await _consolidationRepository.GetByIdAsync(id);
        if (consolidation == null) throw new KeyNotFoundException($"Supplier consolidation with ID {id} not found");

        consolidation.Title = dto.Title;
        consolidation.Description = dto.Description;
        consolidation.ItemCategory = dto.ItemCategory;
        consolidation.AnalysisPeriodStart = dto.AnalysisPeriodStart;
        consolidation.AnalysisPeriodEnd = dto.AnalysisPeriodEnd;
        consolidation.CurrentSupplierCount = dto.CurrentSupplierCount;
        consolidation.RecommendedSupplierCount = dto.RecommendedSupplierCount;
        consolidation.TotalSpend = dto.TotalSpend;
        consolidation.PotentialSavings = dto.PotentialSavings;
        consolidation.Currency = dto.Currency;
        consolidation.OpportunityLevel = dto.OpportunityLevel;
        consolidation.RecommendedStrategy = dto.RecommendedStrategy;
        consolidation.StrategyRationale = dto.StrategyRationale;
        consolidation.PreferredSupplierIds = dto.PreferredSupplierIds;
        consolidation.SuppliersToPhaseOut = dto.SuppliersToPhaseOut;
        consolidation.ImplementationPlan = dto.ImplementationPlan;
        consolidation.Notes = dto.Notes;
        consolidation.UpdatedAt = DateTime.UtcNow;

        await _consolidationRepository.UpdateAsync(consolidation);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated consolidation");
    }

    public async Task DeleteAsync(Guid id)
    {
        var consolidation = await _consolidationRepository.GetByIdAsync(id);
        if (consolidation == null) throw new KeyNotFoundException($"Supplier consolidation with ID {id} not found");

        consolidation.IsDeleted = true;
        consolidation.UpdatedAt = DateTime.UtcNow;
        await _consolidationRepository.UpdateAsync(consolidation);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<SupplierConsolidationDetailDto> SubmitForReviewAsync(Guid id)
    {
        var consolidation = await _consolidationRepository.GetByIdAsync(id);
        if (consolidation == null) throw new KeyNotFoundException($"Supplier consolidation with ID {id} not found");

        consolidation.Status = "UnderReview";
        consolidation.UpdatedAt = DateTime.UtcNow;

        await _consolidationRepository.UpdateAsync(consolidation);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve consolidation");
    }

    public async Task<SupplierConsolidationDetailDto> ApproveAsync(Guid id)
    {
        var consolidation = await _consolidationRepository.GetByIdAsync(id);
        if (consolidation == null) throw new KeyNotFoundException($"Supplier consolidation with ID {id} not found");

        consolidation.Status = "Approved";
        consolidation.UpdatedAt = DateTime.UtcNow;

        await _consolidationRepository.UpdateAsync(consolidation);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve consolidation");
    }

    public async Task<IEnumerable<SupplierConsolidationDto>> GetPendingConsolidationsAsync()
    {
        var consolidations = await _consolidationRepository.GetByStatusAsync("Draft");
        var underReview = await _consolidationRepository.GetByStatusAsync("UnderReview");
        return consolidations.Concat(underReview).Select(MapToDto);
    }

    public async Task<IEnumerable<SupplierConsolidationDto>> GetImplementedConsolidationsAsync()
    {
        var consolidations = await _consolidationRepository.GetByStatusAsync("Implemented");
        return consolidations.Select(MapToDto);
    }

    public async Task<SupplierConsolidationDetailDto> ImplementAsync(Guid id)
    {
        var consolidation = await _consolidationRepository.GetByIdAsync(id);
        if (consolidation == null) throw new KeyNotFoundException($"Supplier consolidation with ID {id} not found");

        consolidation.Status = "Implemented";
        consolidation.ImplementationDate = DateTime.UtcNow;
        consolidation.UpdatedAt = DateTime.UtcNow;

        await _consolidationRepository.UpdateAsync(consolidation);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve consolidation");
    }

    public async Task RecordActualSavingsAsync(Guid id, decimal actualSavings)
    {
        var consolidation = await _consolidationRepository.GetByIdAsync(id);
        if (consolidation == null) throw new KeyNotFoundException($"Supplier consolidation with ID {id} not found");

        consolidation.ActualSavings = actualSavings;
        consolidation.UpdatedAt = DateTime.UtcNow;

        await _consolidationRepository.UpdateAsync(consolidation);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<decimal> GetTotalEstimatedSavingsAsync()
    {
        var result = await _consolidationRepository.GetConsolidationsAsync(1, 10000);
        return result.Items.Where(c => !c.IsDeleted).Sum(c => c.PotentialSavings);
    }

    public async Task<decimal> GetTotalActualSavingsAsync()
    {
        var result = await _consolidationRepository.GetConsolidationsAsync(1, 10000);
        return result.Items.Where(c => !c.IsDeleted).Sum(c => c.ActualSavings);
    }

    #region Mapping Methods

    private static SupplierConsolidationDto MapToDto(SupplierConsolidation consolidation)
    {
        return new SupplierConsolidationDto
        {
            Id = consolidation.Id,
            ConsolidationCode = consolidation.ConsolidationCode,
            Title = consolidation.Title,
            Description = consolidation.Description,
            ItemCategory = consolidation.ItemCategory,
            AnalysisPeriodStart = consolidation.AnalysisPeriodStart,
            AnalysisPeriodEnd = consolidation.AnalysisPeriodEnd,
            CurrentSupplierCount = consolidation.CurrentSupplierCount,
            RecommendedSupplierCount = consolidation.RecommendedSupplierCount,
            TotalSpend = consolidation.TotalSpend,
            PotentialSavings = consolidation.PotentialSavings,
            Currency = consolidation.Currency,
            OpportunityLevel = consolidation.OpportunityLevel,
            RecommendedStrategy = consolidation.RecommendedStrategy,
            StrategyRationale = consolidation.StrategyRationale,
            PreparedByName = consolidation.PreparedBy?.FullName,
            PreparedDate = consolidation.PreparedDate,
            Status = consolidation.Status,
            ImplementationDate = consolidation.ImplementationDate,
            ActualSavings = consolidation.ActualSavings,
            CreatedAt = consolidation.CreatedAt
        };
    }

    private static SupplierConsolidationDetailDto MapToDetailDto(SupplierConsolidation consolidation)
    {
        return new SupplierConsolidationDetailDto
        {
            Id = consolidation.Id,
            ConsolidationCode = consolidation.ConsolidationCode,
            Title = consolidation.Title,
            Description = consolidation.Description,
            ItemCategory = consolidation.ItemCategory,
            AnalysisPeriodStart = consolidation.AnalysisPeriodStart,
            AnalysisPeriodEnd = consolidation.AnalysisPeriodEnd,
            CurrentSupplierCount = consolidation.CurrentSupplierCount,
            RecommendedSupplierCount = consolidation.RecommendedSupplierCount,
            TotalSpend = consolidation.TotalSpend,
            PotentialSavings = consolidation.PotentialSavings,
            Currency = consolidation.Currency,
            OpportunityLevel = consolidation.OpportunityLevel,
            RecommendedStrategy = consolidation.RecommendedStrategy,
            StrategyRationale = consolidation.StrategyRationale,
            PreparedByName = consolidation.PreparedBy?.FullName,
            PreparedDate = consolidation.PreparedDate,
            Status = consolidation.Status,
            ImplementationDate = consolidation.ImplementationDate,
            ActualSavings = consolidation.ActualSavings,
            CreatedAt = consolidation.CreatedAt,
            PreparedById = consolidation.PreparedById,
            PreferredSupplierIds = consolidation.PreferredSupplierIds,
            SuppliersToPhaseOut = consolidation.SuppliersToPhaseOut,
            ImplementationPlan = consolidation.ImplementationPlan,
            Notes = consolidation.Notes
        };
    }

    #endregion
}
