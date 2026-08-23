using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class MarketAnalysisService : IMarketAnalysisService
{
    private readonly IMarketAnalysisRepository _analysisRepository;
    private readonly IPriceHistoryRepository _priceHistoryRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly ICurrencyService _currencyService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<MarketAnalysisService> _logger;

    public MarketAnalysisService(
        IMarketAnalysisRepository analysisRepository,
        IPriceHistoryRepository priceHistoryRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        ICurrencyService currencyService,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<MarketAnalysisService> logger)
    {
        _analysisRepository = analysisRepository;
        _priceHistoryRepository = priceHistoryRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _currencyService = currencyService;
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
        var analysis = await RequireAnalysisAsync(id);
        if (string.Equals(analysis.Status, "Published", StringComparison.OrdinalIgnoreCase))
        {
            return await GetByIdAsync(id) ?? throw UnexpectedRetrievalFailure();
        }

        EnsureDraft(analysis, "publish");
        await ValidateForPublicationAsync(analysis);

        analysis.Status = "Published";
        analysis.UpdatedAt = DateTime.UtcNow;
        await _analysisRepository.UpdateAsync(analysis);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw UnexpectedRetrievalFailure();
    }

    public async Task<MarketAnalysisDetailDto> CreateAsync(CreateMarketAnalysisDto dto)
    {
        var currencyCode = await ValidateDraftAsync(dto);
        var analysisCode = await _analysisRepository.GenerateAnalysisCodeAsync();
        var now = DateTime.UtcNow;
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
            Currency = currencyCode,
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
            PreparedById = _currentUserProvider.UserId,
            PreparedDate = now,
            TenantId = _currentUserProvider.TenantId
        };

        await _analysisRepository.AddAsync(analysis);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Created market analysis {AnalysisCode}", analysisCode);

        return await GetByIdAsync(analysis.Id) ?? throw UnexpectedRetrievalFailure();
    }

    public async Task<MarketAnalysisDetailDto> UpdateAsync(Guid id, CreateMarketAnalysisDto dto)
    {
        var analysis = await RequireAnalysisAsync(id);
        EnsureDraft(analysis, "edit");
        var currencyCode = await ValidateDraftAsync(dto);

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
        analysis.Currency = currencyCode;
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

        return await GetByIdAsync(id) ?? throw UnexpectedRetrievalFailure();
    }

    public async Task DeleteAsync(Guid id)
    {
        var analysis = await RequireAnalysisAsync(id);
        EnsureDraft(analysis, "delete");

        analysis.IsDeleted = true;
        analysis.UpdatedAt = DateTime.UtcNow;
        await _analysisRepository.UpdateAsync(analysis);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PriceHistoryDto> AddPriceHistoryAsync(Guid analysisId, CreatePriceHistoryDto dto)
    {
        var analysis = await RequireAnalysisAsync(analysisId);
        EnsureDraft(analysis, "change survey quotes for");
        var supplier = await ResolveSupplierAsync(dto.SupplierId, dto.SupplierName);
        ValidateQuote(dto, analysis.Currency);

        var priceHistory = new PriceHistory
        {
            MarketAnalysisId = analysisId,
            ItemCategory = analysis.ItemCategory,
            ItemDescription = analysis.ItemDescription,
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            PriceDate = dto.PriceDate,
            UnitPrice = dto.UnitPrice,
            Currency = analysis.Currency,
            UnitOfMeasure = dto.UnitOfMeasure.Trim().ToUpperInvariant(),
            PriceSource = string.IsNullOrWhiteSpace(dto.PriceSource) ? "MarketSurvey" : dto.PriceSource.Trim(),
            PurchaseOrderId = dto.PurchaseOrderId,
            TenderId = dto.TenderId,
            Notes = dto.Notes,
            TenantId = _currentUserProvider.TenantId
        };

        await _priceHistoryRepository.AddAsync(priceHistory);
        await _unitOfWork.SaveChangesAsync();
        return MapToPriceHistoryDto(priceHistory);
    }

    public async Task<PriceHistoryDto> UpdatePriceHistoryAsync(
        Guid analysisId,
        Guid priceHistoryId,
        CreatePriceHistoryDto dto)
    {
        var analysis = await RequireAnalysisAsync(analysisId);
        EnsureDraft(analysis, "change survey quotes for");
        var priceHistory = await _priceHistoryRepository.GetByIdAsync(priceHistoryId);
        if (priceHistory == null || priceHistory.MarketAnalysisId != analysisId || priceHistory.IsDeleted)
        {
            throw Rule("MARKET_SURVEY_QUOTE_NOT_FOUND", "The selected market survey quote was not found.", 404);
        }

        var supplier = await ResolveSupplierAsync(dto.SupplierId, dto.SupplierName);
        ValidateQuote(dto, analysis.Currency);

        priceHistory.ItemCategory = analysis.ItemCategory;
        priceHistory.ItemDescription = analysis.ItemDescription;
        priceHistory.SupplierId = supplier.Id;
        priceHistory.SupplierName = supplier.Name;
        priceHistory.PriceDate = dto.PriceDate;
        priceHistory.UnitPrice = dto.UnitPrice;
        priceHistory.Currency = analysis.Currency;
        priceHistory.UnitOfMeasure = dto.UnitOfMeasure.Trim().ToUpperInvariant();
        priceHistory.PriceSource = string.IsNullOrWhiteSpace(dto.PriceSource) ? "MarketSurvey" : dto.PriceSource.Trim();
        priceHistory.PurchaseOrderId = dto.PurchaseOrderId;
        priceHistory.TenderId = dto.TenderId;
        priceHistory.Notes = dto.Notes?.Trim();
        priceHistory.UpdatedAt = DateTime.UtcNow;

        await _priceHistoryRepository.UpdateAsync(priceHistory);
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
        if (priceHistory == null)
        {
            throw Rule("MARKET_SURVEY_QUOTE_NOT_FOUND", "The selected market survey quote was not found.", 404);
        }

        if (priceHistory.MarketAnalysisId.HasValue)
        {
            var analysis = await RequireAnalysisAsync(priceHistory.MarketAnalysisId.Value);
            EnsureDraft(analysis, "change survey quotes for");
        }

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
        if (analysis == null)
        {
            throw Rule("MARKET_ANALYSIS_NOT_FOUND", "The selected market analysis was not found.", 404);
        }

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
        if (analysis == null)
        {
            throw Rule("MARKET_ANALYSIS_NOT_FOUND", "The selected market analysis was not found.", 404);
        }

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

    private async Task<MarketAnalysis> RequireAnalysisAsync(Guid id)
    {
        var analysis = await _analysisRepository.GetByIdAsync(id);
        if (analysis == null || analysis.IsDeleted)
        {
            throw Rule("MARKET_ANALYSIS_NOT_FOUND", "The selected market analysis was not found.", 404);
        }

        return analysis;
    }

    private async Task<string> ValidateDraftAsync(CreateMarketAnalysisDto dto)
    {
        if (_currentUserProvider.TenantId == Guid.Empty)
        {
            throw Rule("MARKET_ANALYSIS_TENANT_REQUIRED", "A valid tenant is required to maintain market analysis records.", 403);
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw Rule("MARKET_ANALYSIS_TITLE_REQUIRED", "Enter a title for the market analysis.");
        }

        if (dto.AnalysisPeriodStart == default || dto.AnalysisPeriodEnd == default ||
            dto.AnalysisPeriodEnd.Date < dto.AnalysisPeriodStart.Date)
        {
            throw Rule("MARKET_ANALYSIS_PERIOD_INVALID", "The analysis end date must be on or after the start date.");
        }

        if (dto.HistoricalAveragePrice is < 0 || dto.PreviousPrice is < 0 || dto.CurrentMarketPrice is < 0 ||
            dto.ForecastedPrice is < 0 || dto.LeadTimeDays is < 0)
        {
            throw Rule("MARKET_ANALYSIS_VALUE_INVALID", "Prices and lead time cannot be negative.");
        }

        return await RequireActiveCurrencyAsync(dto.Currency);
    }

    private async Task ValidateForPublicationAsync(MarketAnalysis analysis)
    {
        if (string.IsNullOrWhiteSpace(analysis.ItemCategory) || string.IsNullOrWhiteSpace(analysis.ItemDescription))
        {
            throw Rule("MARKET_ANALYSIS_ITEM_REQUIRED", "Select the inventory item and category before publishing the market analysis.");
        }

        if (analysis.CurrentMarketPrice <= 0)
        {
            throw Rule("MARKET_ANALYSIS_PRICE_REQUIRED", "Enter a current market price greater than zero before publishing the market analysis.");
        }

        await RequireActiveCurrencyAsync(analysis.Currency);
    }

    private async Task<string> RequireActiveCurrencyAsync(string? currencyCode)
    {
        var normalized = currencyCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw Rule("MARKET_ANALYSIS_CURRENCY_REQUIRED", "Select an active Finance currency.");
        }

        var currency = await _currencyService.GetByCodeAsync(normalized);
        if (currency == null || !currency.IsActive || currency.TenantId != _currentUserProvider.TenantId)
        {
            throw Rule(
                "MARKET_ANALYSIS_CURRENCY_INVALID",
                $"Currency {normalized} is not active for this tenant. Select an active currency configured in Finance.");
        }

        return currency.CurrencyCode.Trim().ToUpperInvariant();
    }

    private async Task<(Guid? Id, string Name)> ResolveSupplierAsync(Guid? supplierId, string? manualSupplierName)
    {
        if (!supplierId.HasValue)
        {
            var name = manualSupplierName?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw Rule("MARKET_SURVEY_SUPPLIER_REQUIRED", "Select an approved supplier or enter a supplier name.");
            }

            return (null, name);
        }

        var supplier = await _businessPartnerRepository.GetByIdAsync(supplierId.Value);
        if (supplier == null || supplier.TenantId != _currentUserProvider.TenantId)
        {
            throw Rule("MARKET_SURVEY_SUPPLIER_NOT_FOUND", "The selected supplier was not found for this tenant.", 404);
        }

        var supportsSupply = string.Equals(supplier.PartnerType, "Supplier", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supplier.PartnerType, "Both", StringComparison.OrdinalIgnoreCase);
        if (!supportsSupply || !BusinessPartnerLifecyclePolicy.IsOperationallyApproved(supplier))
        {
            throw Rule("MARKET_SURVEY_SUPPLIER_INELIGIBLE", "Select an active, approved supplier that is not blacklisted.", 409);
        }

        return (supplier.Id, supplier.PartnerName.Trim());
    }

    private static void ValidateQuote(CreatePriceHistoryDto dto, string analysisCurrency)
    {
        if (dto.UnitPrice <= 0)
        {
            throw Rule("MARKET_SURVEY_PRICE_INVALID", "Quote price must be greater than zero.");
        }

        if (dto.PriceDate == default)
        {
            throw Rule("MARKET_SURVEY_DATE_REQUIRED", "Enter the supplier quote date.");
        }

        if (string.IsNullOrWhiteSpace(dto.UnitOfMeasure))
        {
            throw Rule("MARKET_SURVEY_UOM_REQUIRED", "Enter the unit of measure for the supplier quote.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Currency) &&
            !string.Equals(dto.Currency.Trim(), analysisCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw Rule("MARKET_SURVEY_CURRENCY_MISMATCH", "The supplier quote currency must match the market analysis currency.", 409);
        }
    }

    private static void EnsureDraft(MarketAnalysis analysis, string action)
    {
        if (!string.Equals(analysis.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            throw Rule(
                "MARKET_ANALYSIS_NOT_DRAFT",
                $"Only Draft market analyses can be used to {action} this record.",
                409);
        }
    }

    private static BusinessRuleException Rule(string code, string message, int statusCode = 422)
        => new(code, message, statusCode);

    private static InvalidOperationException UnexpectedRetrievalFailure()
        => new("The market analysis was saved but could not be reloaded.");

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
