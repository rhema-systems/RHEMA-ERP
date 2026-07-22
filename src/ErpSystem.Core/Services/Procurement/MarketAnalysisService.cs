using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class MarketAnalysisService : IMarketAnalysisService
{
    private readonly IMarketAnalysisRepository _analysisRepository;
    private readonly IPriceHistoryRepository _priceHistoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<MarketAnalysisService> _logger;

    public MarketAnalysisService(
        IMarketAnalysisRepository analysisRepository,
        IPriceHistoryRepository priceHistoryRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<MarketAnalysisService> logger)
    {
        _analysisRepository = analysisRepository;
        _priceHistoryRepository = priceHistoryRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<MarketAnalysisDetailDto?> GetByIdAsync(Guid id)
    {
        var analysis = await _analysisRepository.GetWithPriceHistoriesAsync(id);
        return analysis == null ? null : MapToDetailDto(analysis);
    }

    public async Task<MarketAnalysisDto?> GetByAnalysisCodeAsync(string analysisCode)
    {
        var analysis = await _analysisRepository.GetByAnalysisCodeAsync(analysisCode);
        return analysis == null ? null : MapToDto(analysis);
    }

    public async Task<PagedResult<MarketAnalysisDto>> GetAnalysesAsync(
        int page, int pageSize, string? search = null, string? itemCategory = null, string? status = null)
    {
        var result = await _analysisRepository.GetAnalysesAsync(page, pageSize, search, status, itemCategory);
        return new PagedResult<MarketAnalysisDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<IEnumerable<MarketAnalysisDto>> GetByItemCategoryAsync(string itemCategory)
    {
        var analyses = await _analysisRepository.GetByItemCategoryAsync(itemCategory);
        return analyses.Select(MapToDto);
    }

    public async Task<IEnumerable<MarketAnalysisDto>> GetByCategoryAsync(string category)
    {
        var analyses = await _analysisRepository.GetByItemCategoryAsync(category);
        return analyses.Select(MapToDto);
    }

    public async Task<IEnumerable<MarketAnalysisDto>> GetByItemAsync(string itemName)
    {
        var analyses = await _analysisRepository.GetByItemCategoryAsync(itemName);
        return analyses.Select(MapToDto);
    }

    public async Task<IEnumerable<MarketAnalysisDto>> GetRecentAnalysesAsync(int count = 10)
    {
        var result = await _analysisRepository.GetAnalysesAsync(1, count);
        return result.Items.Select(MapToDto);
    }

    public async Task<MarketAnalysisDetailDto> PublishAsync(Guid id)
    {
        var analysis = await _analysisRepository.GetByIdAsync(id);
        if (analysis == null) throw new KeyNotFoundException($"Market analysis with ID {id} not found");

        analysis.Status = "Published";
        analysis.UpdatedAt = DateTime.UtcNow;
        await _analysisRepository.UpdateAsync(analysis);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve analysis");
    }

    public async Task<MarketAnalysisDetailDto> CreateAsync(CreateMarketAnalysisDto dto)
    {
        var analysisCode = await _analysisRepository.GenerateAnalysisCodeAsync();
        var analysis = new MarketAnalysis
        {
            AnalysisCode = analysisCode,
            Title = dto.Title,
            Description = dto.Description,
            ItemCategory = dto.ItemCategory,
            ItemDescription = dto.ItemDescription,
            AnalysisPeriodStart = dto.AnalysisPeriodStart,
            AnalysisPeriodEnd = dto.AnalysisPeriodEnd,
            HistoricalAveragePrice = dto.HistoricalAveragePrice,
            PreviousPrice = dto.PreviousPrice,
            CurrentMarketPrice = dto.CurrentMarketPrice,
            ForecastedPrice = dto.ForecastedPrice,
            PriceTrend = dto.PriceTrend,
            PriceChangePercent = dto.PriceChangePercent,
            PriceVariancePercent = dto.PriceVariancePercent ?? CalculateVariance(dto.CurrentMarketPrice, dto.PreviousPrice ?? dto.HistoricalAveragePrice),
            Currency = dto.Currency,
            LeadTimeDays = dto.LeadTimeDays,
            MarketAvailability = dto.MarketAvailability,
            SupplyRiskLevel = dto.SupplyRiskLevel,
            InflationImpactPercent = dto.InflationImpactPercent,
            RecommendedBudgetAdjustmentPercent = dto.RecommendedBudgetAdjustmentPercent,
            MarketRiskLevel = dto.MarketRiskLevel,
            RiskFactors = dto.RiskFactors,
            Opportunities = dto.Opportunities,
            RecommendedStrategy = dto.RecommendedStrategy,
            StrategyRationale = dto.StrategyRationale,
            OptimalPurchaseMonth = dto.OptimalPurchaseMonth,
            SeasonalPattern = dto.SeasonalPattern,
            Notes = dto.Notes,
            Status = "Draft",
            TenantId = _currentUserProvider.TenantId
        };

        await _analysisRepository.AddAsync(analysis);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Created market analysis {AnalysisCode}", analysisCode);

        return await GetByIdAsync(analysis.Id) ?? throw new InvalidOperationException("Failed to retrieve created analysis");
    }

    public async Task<MarketAnalysisDetailDto> UpdateAsync(Guid id, CreateMarketAnalysisDto dto)
    {
        var analysis = await _analysisRepository.GetByIdAsync(id);
        if (analysis == null) throw new KeyNotFoundException($"Market analysis with ID {id} not found");

        analysis.Title = dto.Title;
        analysis.Description = dto.Description;
        analysis.ItemCategory = dto.ItemCategory;
        analysis.ItemDescription = dto.ItemDescription;
        analysis.AnalysisPeriodStart = dto.AnalysisPeriodStart;
        analysis.AnalysisPeriodEnd = dto.AnalysisPeriodEnd;
        analysis.HistoricalAveragePrice = dto.HistoricalAveragePrice;
        analysis.PreviousPrice = dto.PreviousPrice;
        analysis.CurrentMarketPrice = dto.CurrentMarketPrice;
        analysis.ForecastedPrice = dto.ForecastedPrice;
        analysis.PriceTrend = dto.PriceTrend;
        analysis.PriceChangePercent = dto.PriceChangePercent;
        analysis.PriceVariancePercent = dto.PriceVariancePercent ?? CalculateVariance(dto.CurrentMarketPrice, dto.PreviousPrice ?? dto.HistoricalAveragePrice);
        analysis.Currency = dto.Currency;
        analysis.LeadTimeDays = dto.LeadTimeDays;
        analysis.MarketAvailability = dto.MarketAvailability;
        analysis.SupplyRiskLevel = dto.SupplyRiskLevel;
        analysis.InflationImpactPercent = dto.InflationImpactPercent;
        analysis.RecommendedBudgetAdjustmentPercent = dto.RecommendedBudgetAdjustmentPercent;
        analysis.MarketRiskLevel = dto.MarketRiskLevel;
        analysis.RiskFactors = dto.RiskFactors;
        analysis.Opportunities = dto.Opportunities;
        analysis.RecommendedStrategy = dto.RecommendedStrategy;
        analysis.StrategyRationale = dto.StrategyRationale;
        analysis.OptimalPurchaseMonth = dto.OptimalPurchaseMonth;
        analysis.SeasonalPattern = dto.SeasonalPattern;
        analysis.Notes = dto.Notes;
        analysis.UpdatedAt = DateTime.UtcNow;

        await _analysisRepository.UpdateAsync(analysis);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated analysis");
    }

    public async Task DeleteAsync(Guid id)
    {
        var analysis = await _analysisRepository.GetByIdAsync(id);
        if (analysis == null) throw new KeyNotFoundException($"Market analysis with ID {id} not found");

        analysis.IsDeleted = true;
        analysis.UpdatedAt = DateTime.UtcNow;
        await _analysisRepository.UpdateAsync(analysis);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PriceHistoryDto> AddPriceHistoryAsync(Guid analysisId, CreatePriceHistoryDto dto)
    {
        var priceHistory = new PriceHistory
        {
            MarketAnalysisId = analysisId,
            ItemCategory = dto.ItemCategory,
            ItemDescription = dto.ItemDescription,
            SupplierId = dto.SupplierId,
            SupplierName = dto.SupplierName,
            PriceDate = dto.PriceDate,
            UnitPrice = dto.UnitPrice,
            Currency = dto.Currency,
            UnitOfMeasure = dto.UnitOfMeasure,
            PriceSource = dto.PriceSource,
            PurchaseOrderId = dto.PurchaseOrderId,
            TenderId = dto.TenderId,
            Notes = dto.Notes,
            TenantId = _currentUserProvider.TenantId
        };

        await _priceHistoryRepository.AddAsync(priceHistory);
        await _unitOfWork.SaveChangesAsync();
        return MapToPriceHistoryDto(priceHistory);
    }

    public async Task<IEnumerable<PriceHistoryDto>> GetPriceHistoriesAsync(Guid analysisId)
    {
        var history = await _priceHistoryRepository.GetByMarketAnalysisIdAsync(analysisId);
        return history.Select(MapToPriceHistoryDto);
    }

    public async Task<IEnumerable<PriceHistoryDto>> GetPriceHistoryAsync(Guid analysisId)
    {
        var history = await _priceHistoryRepository.GetByMarketAnalysisIdAsync(analysisId);
        return history.Select(MapToPriceHistoryDto);
    }

    public async Task<IEnumerable<PriceHistoryDto>> GetPriceHistoryByDateRangeAsync(Guid analysisId, DateTime startDate, DateTime endDate)
    {
        var history = await _priceHistoryRepository.GetByDateRangeAsync(startDate, endDate);
        return history.Where(h => h.MarketAnalysisId == analysisId).Select(MapToPriceHistoryDto);
    }

    public async Task<IEnumerable<PriceHistoryDto>> GetPriceHistoryBySupplierAsync(Guid supplierId)
    {
        var history = await _priceHistoryRepository.GetBySupplierAsync(supplierId);
        return history.Select(MapToPriceHistoryDto);
    }

    public async Task DeletePriceHistoryAsync(Guid priceHistoryId)
    {
        var priceHistory = await _priceHistoryRepository.GetByIdAsync(priceHistoryId);
        if (priceHistory == null) throw new KeyNotFoundException($"Price history with ID {priceHistoryId} not found");

        priceHistory.IsDeleted = true;
        priceHistory.UpdatedAt = DateTime.UtcNow;
        await _priceHistoryRepository.UpdateAsync(priceHistory);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<decimal> GetAveragePriceAsync(string category)
    {
        var startDate = DateTime.UtcNow.AddMonths(-12);
        var endDate = DateTime.UtcNow;
        return await _priceHistoryRepository.GetAveragePriceAsync(category, startDate, endDate);
    }

    public async Task<decimal> GetAveragePriceByAnalysisAsync(Guid analysisId, int months = 12)
    {
        var startDate = DateTime.UtcNow.AddMonths(-months);
        var endDate = DateTime.UtcNow;
        var history = await _priceHistoryRepository.GetByMarketAnalysisIdAsync(analysisId);
        var relevantHistory = history.Where(h => h.PriceDate >= startDate && h.PriceDate <= endDate);
        return relevantHistory.Any() ? relevantHistory.Average(h => h.UnitPrice) : 0;
    }

    public async Task<decimal> GetPriceTrendPercentageAsync(Guid analysisId, int months = 6)
    {
        var startDate = DateTime.UtcNow.AddMonths(-months);
        var endDate = DateTime.UtcNow;
        var history = await _priceHistoryRepository.GetByMarketAnalysisIdAsync(analysisId);
        var relevantHistory = history.Where(h => h.PriceDate >= startDate && h.PriceDate <= endDate).OrderBy(h => h.PriceDate).ToList();
        if (relevantHistory.Count < 2) return 0;
        var firstPrice = relevantHistory.First().UnitPrice;
        var lastPrice = relevantHistory.Last().UnitPrice;
        return firstPrice > 0 ? ((lastPrice - firstPrice) / firstPrice) * 100 : 0;
    }

    public async Task<PriceTrendDto> GetPriceTrendAsync(Guid analysisId, int months = 12)
    {
        var analysis = await _analysisRepository.GetWithPriceHistoriesAsync(analysisId);
        if (analysis == null) throw new KeyNotFoundException($"Market analysis with ID {analysisId} not found");

        var startDate = DateTime.UtcNow.AddMonths(-months);
        var endDate = DateTime.UtcNow;
        var history = analysis.PriceHistories?
            .Where(h => !h.IsDeleted && h.PriceDate >= startDate && h.PriceDate <= endDate)
            .OrderBy(h => h.PriceDate)
            .ToList() ?? new List<PriceHistory>();

        var avgPrice = history.Any() ? history.Average(h => h.UnitPrice) : 0;
        var minPrice = history.Any() ? history.Min(h => h.UnitPrice) : 0;
        var maxPrice = history.Any() ? history.Max(h => h.UnitPrice) : 0;
        var changePercent = 0m;
        var trend = "Stable";

        if (history.Count >= 2)
        {
            var firstPrice = history.First().UnitPrice;
            var lastPrice = history.Last().UnitPrice;
            changePercent = firstPrice > 0 ? ((lastPrice - firstPrice) / firstPrice) * 100 : 0;
            trend = changePercent > 5 ? "Increasing" : changePercent < -5 ? "Decreasing" : "Stable";
        }

        return new PriceTrendDto
        {
            MarketAnalysisId = analysisId,
            ItemCategory = analysis.ItemCategory,
            ItemDescription = analysis.ItemDescription,
            Trend = trend,
            ChangePercent = changePercent,
            AveragePrice = avgPrice,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            CurrentPrice = analysis.CurrentMarketPrice,
            ForecastedPrice = analysis.ForecastedPrice,
            Currency = analysis.Currency,
            AnalysisPeriodMonths = months,
            AnalysisDate = DateTime.UtcNow,
            PriceHistory = history.Select(MapToPriceHistoryDto).ToList()
        };
    }

    public async Task<MarketSurveySummaryDto> GetMarketSurveySummaryAsync(Guid analysisId)
    {
        var analysis = await _analysisRepository.GetWithPriceHistoriesAsync(analysisId);
        if (analysis == null) throw new KeyNotFoundException($"Market analysis with ID {analysisId} not found");

        var quotes = analysis.PriceHistories?
            .Where(p => !p.IsDeleted)
            .Where(p => p.PriceSource.Equals("Quote", StringComparison.OrdinalIgnoreCase)
                || p.PriceSource.Equals("MarketSurvey", StringComparison.OrdinalIgnoreCase)
                || p.PriceSource.Equals("SupplierQuote", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.PriceDate)
            .ToList() ?? new List<PriceHistory>();

        var averagePrice = quotes.Any() ? Math.Round(quotes.Average(q => q.UnitPrice), 4) : analysis.CurrentMarketPrice;
        var lowestQuote = quotes.OrderBy(q => q.UnitPrice).FirstOrDefault();
        var highestPrice = quotes.Any() ? quotes.Max(q => q.UnitPrice) : analysis.CurrentMarketPrice;

        return new MarketSurveySummaryDto
        {
            MarketAnalysisId = analysisId,
            QuoteCount = quotes.Count,
            AverageMarketPrice = averagePrice,
            LowestPrice = lowestQuote?.UnitPrice ?? analysis.CurrentMarketPrice,
            HighestPrice = highestPrice,
            RecommendedPlanningEstimate = averagePrice,
            Currency = quotes.Select(q => q.Currency).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? analysis.Currency,
            LowestPriceSupplierId = lowestQuote?.SupplierId,
            LowestPriceSupplierName = lowestQuote?.SupplierName,
            LatestQuoteDate = quotes.FirstOrDefault()?.PriceDate,
            Quotes = quotes.Select(MapToPriceHistoryDto).ToList()
        };
    }

    #region Mapping Methods

    private static decimal? CalculateVariance(decimal currentPrice, decimal? previousPrice)
    {
        if (!previousPrice.HasValue || previousPrice.Value <= 0) return null;
        return Math.Round(((currentPrice - previousPrice.Value) / previousPrice.Value) * 100, 2);
    }

    private static MarketAnalysisDto MapToDto(MarketAnalysis analysis)
    {
        return new MarketAnalysisDto
        {
            Id = analysis.Id,
            AnalysisCode = analysis.AnalysisCode,
            Title = analysis.Title,
            Description = analysis.Description,
            ItemCategory = analysis.ItemCategory,
            ItemDescription = analysis.ItemDescription,
            AnalysisPeriodStart = analysis.AnalysisPeriodStart,
            AnalysisPeriodEnd = analysis.AnalysisPeriodEnd,
            HistoricalAveragePrice = analysis.HistoricalAveragePrice,
            PreviousPrice = analysis.PreviousPrice,
            CurrentMarketPrice = analysis.CurrentMarketPrice,
            ForecastedPrice = analysis.ForecastedPrice,
            PriceTrend = analysis.PriceTrend,
            PriceChangePercent = analysis.PriceChangePercent,
            PriceVariancePercent = analysis.PriceVariancePercent,
            Currency = analysis.Currency,
            LeadTimeDays = analysis.LeadTimeDays,
            MarketAvailability = analysis.MarketAvailability,
            SupplyRiskLevel = analysis.SupplyRiskLevel,
            InflationImpactPercent = analysis.InflationImpactPercent,
            RecommendedBudgetAdjustmentPercent = analysis.RecommendedBudgetAdjustmentPercent,
            MarketRiskLevel = analysis.MarketRiskLevel,
            RiskFactors = analysis.RiskFactors,
            Opportunities = analysis.Opportunities,
            RecommendedStrategy = analysis.RecommendedStrategy,
            StrategyRationale = analysis.StrategyRationale,
            OptimalPurchaseMonth = analysis.OptimalPurchaseMonth,
            SeasonalPattern = analysis.SeasonalPattern,
            PreparedByName = analysis.PreparedBy?.FullName,
            PreparedDate = analysis.PreparedDate,
            Status = analysis.Status,
            CreatedAt = analysis.CreatedAt
        };
    }

    private static MarketAnalysisDetailDto MapToDetailDto(MarketAnalysis analysis)
    {
        return new MarketAnalysisDetailDto
        {
            Id = analysis.Id,
            AnalysisCode = analysis.AnalysisCode,
            Title = analysis.Title,
            Description = analysis.Description,
            ItemCategory = analysis.ItemCategory,
            ItemDescription = analysis.ItemDescription,
            AnalysisPeriodStart = analysis.AnalysisPeriodStart,
            AnalysisPeriodEnd = analysis.AnalysisPeriodEnd,
            HistoricalAveragePrice = analysis.HistoricalAveragePrice,
            PreviousPrice = analysis.PreviousPrice,
            CurrentMarketPrice = analysis.CurrentMarketPrice,
            ForecastedPrice = analysis.ForecastedPrice,
            PriceTrend = analysis.PriceTrend,
            PriceChangePercent = analysis.PriceChangePercent,
            PriceVariancePercent = analysis.PriceVariancePercent,
            Currency = analysis.Currency,
            LeadTimeDays = analysis.LeadTimeDays,
            MarketAvailability = analysis.MarketAvailability,
            SupplyRiskLevel = analysis.SupplyRiskLevel,
            InflationImpactPercent = analysis.InflationImpactPercent,
            RecommendedBudgetAdjustmentPercent = analysis.RecommendedBudgetAdjustmentPercent,
            MarketRiskLevel = analysis.MarketRiskLevel,
            RiskFactors = analysis.RiskFactors,
            Opportunities = analysis.Opportunities,
            RecommendedStrategy = analysis.RecommendedStrategy,
            StrategyRationale = analysis.StrategyRationale,
            OptimalPurchaseMonth = analysis.OptimalPurchaseMonth,
            SeasonalPattern = analysis.SeasonalPattern,
            PreparedByName = analysis.PreparedBy?.FullName,
            PreparedDate = analysis.PreparedDate,
            Status = analysis.Status,
            CreatedAt = analysis.CreatedAt,
            PreparedById = analysis.PreparedById,
            Notes = analysis.Notes,
            PriceHistories = analysis.PriceHistories?.Where(p => !p.IsDeleted).Select(MapToPriceHistoryDto).ToList() ?? new()
        };
    }

    private static PriceHistoryDto MapToPriceHistoryDto(PriceHistory priceHistory)
    {
        return new PriceHistoryDto
        {
            Id = priceHistory.Id,
            MarketAnalysisId = priceHistory.MarketAnalysisId,
            ItemCategory = priceHistory.ItemCategory,
            ItemDescription = priceHistory.ItemDescription,
            SupplierId = priceHistory.SupplierId,
            SupplierName = priceHistory.SupplierName,
            PriceDate = priceHistory.PriceDate,
            UnitPrice = priceHistory.UnitPrice,
            Currency = priceHistory.Currency,
            UnitOfMeasure = priceHistory.UnitOfMeasure,
            PriceSource = priceHistory.PriceSource,
            PurchaseOrderId = priceHistory.PurchaseOrderId,
            TenderId = priceHistory.TenderId,
            Notes = priceHistory.Notes
        };
    }

    #endregion
}
