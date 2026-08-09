using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyRateLibraryListRequest
{
    public string? Search { get; set; }
    public QuantitySurveyRateItemCategory? Category { get; set; }
    public Guid? ProjectTypeId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public DateTime? EffectiveAt { get; set; }
    public bool IncludeInactive { get; set; }
    [Range(1, 500)] public int PageSize { get; set; } = 100;
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
}

public sealed class QuantitySurveyRateLibraryPageDto
{
    public IReadOnlyList<QuantitySurveyRateLibraryItemDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed class QuantitySurveyRateLibraryItemDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public QuantitySurveyRateItemCategory Category { get; init; }
    public Guid UnitOfMeasureId { get; init; }
    public string UnitOfMeasureCode { get; init; } = string.Empty;
    public string UnitOfMeasureName { get; init; } = string.Empty;
    public Guid? ProjectCatalogEntryId { get; init; }
    public string? ProjectCatalogEntryLabel { get; init; }
    public Guid? InventoryItemId { get; init; }
    public string? InventoryItemLabel { get; init; }
    public bool IsActive { get; init; }
    public int RateCount { get; init; }
    public QuantitySurveyRateDto? CurrentRate { get; init; }
    public IReadOnlyList<QuantitySurveyRateDto> Rates { get; init; } = [];
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class QuantitySurveyRateDto
{
    public Guid Id { get; init; }
    public Guid RateLibraryItemId { get; init; }
    public int Version { get; init; }
    public decimal UnitRate { get; init; }
    public Guid CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public Guid? ProjectTypeId { get; init; }
    public string? ProjectTypeLabel { get; init; }
    public Guid? LocationId { get; init; }
    public string? LocationLabel { get; init; }
    public Guid? BusinessPartnerId { get; init; }
    public string? BusinessPartnerLabel { get; init; }
    public QuantitySurveyRateSourceType SourceType { get; init; }
    public string? SourceReference { get; init; }
    public DateTime SourceDate { get; init; }
    public Guid? CentralDocumentRecordId { get; init; }
    public Guid? CentralDocumentVersionId { get; init; }
    public string? EvidenceLabel { get; init; }
    public Guid? MarketAnalysisId { get; init; }
    public string? MarketAnalysisCode { get; init; }
    public Guid? PreviousRateId { get; init; }
    public decimal? PreviousUnitRate { get; init; }
    public string? PreviousCurrencyCode { get; init; }
    public decimal? VarianceAmount { get; init; }
    public decimal? VariancePercent { get; init; }
    public int? MarketSurveyQuoteCount { get; init; }
    public DateTime? NextReviewDueAt { get; init; }
    public QuantitySurveyHistoricalRateSourceType? HistoricalSourceType { get; init; }
    public Guid? HistoricalSourceId { get; init; }
    public Guid? HistoricalProjectId { get; init; }
    public string? HistoricalProjectCode { get; init; }
    public string? HistoricalSourceLabel { get; init; }
    public string? HistoricalUnitOfMeasure { get; init; }
    public decimal? HistoricalQuantity { get; init; }
    public decimal? HistoricalTotalAmount { get; init; }
    public string? HistoricalSourceHash { get; init; }
    public Guid? RateBuildUpId { get; init; }
    public QuantitySurveyRateLifecycleStatus LifecycleStatus { get; init; }
    public string? ChangeReason { get; init; }
    public Guid PreparedById { get; init; }
    public DateTime PreparedAt { get; init; }
    public Guid? PublishedById { get; init; }
    public DateTime? PublishedAt { get; init; }
    public Guid? RetiredById { get; init; }
    public DateTime? RetiredAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public class CreateQuantitySurveyRateLibraryItemRequest
{
    [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    public QuantitySurveyRateItemCategory Category { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public Guid? ProjectCatalogEntryId { get; set; }
    public Guid? InventoryItemId { get; set; }
}

public sealed class UpdateQuantitySurveyRateLibraryItemRequest : CreateQuantitySurveyRateLibraryItemRequest
{
    public bool IsActive { get; set; } = true;
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
}

public class SaveQuantitySurveyRateRequest
{
    [Range(typeof(decimal), "0", "9999999999999")] public decimal UnitRate { get; set; }
    public Guid CurrencyId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public Guid? ProjectTypeId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public QuantitySurveyRateSourceType SourceType { get; set; }
    [StringLength(250)] public string? SourceReference { get; set; }
    public DateTime SourceDate { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    [Required, StringLength(1000)] public string ChangeReason { get; set; } = string.Empty;
}

public sealed class UpdateQuantitySurveyRateRequest : SaveQuantitySurveyRateRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class QuantitySurveyRateLifecycleRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
}

public sealed class QuantitySurveyMarketSurveySourceDto
{
    public Guid Id { get; init; }
    public string AnalysisCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? ItemCategory { get; init; }
    public string? ItemDescription { get; init; }
    public DateTime AnalysisPeriodStart { get; init; }
    public DateTime AnalysisPeriodEnd { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal CurrentMarketPrice { get; init; }
    public decimal AverageSurveyPrice { get; init; }
    public decimal LowestSurveyPrice { get; init; }
    public decimal HighestSurveyPrice { get; init; }
    public decimal ForecastedPrice { get; init; }
    public int QuoteCount { get; init; }
    public DateTime? LatestQuoteDate { get; init; }
    public DateTime NextReviewDueAt { get; init; }
    public bool IsOverdue { get; init; }
}

public sealed class PrepareQuantitySurveyMarketSurveyUpdateRequest
{
    public Guid MarketAnalysisId { get; set; }
    public QuantitySurveyMarketSurveyPriceBasis PriceBasis { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public Guid? ProjectTypeId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [Required, StringLength(1000)] public string ChangeReason { get; set; } = string.Empty;
}

public sealed record QuantitySurveyHistoricalRateSourceDto
{
    public QuantitySurveyHistoricalRateSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectTitle { get; init; } = string.Empty;
    public Guid? ProjectTypeId { get; init; }
    public Guid? LocationId { get; init; }
    public Guid? BusinessPartnerId { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public string SourceLabel { get; init; } = string.Empty;
    public DateTime SourceDate { get; init; }
    public string UnitOfMeasure { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitRate { get; init; }
    public decimal TotalAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string IntegrityHash { get; init; } = string.Empty;
    public Guid? ExistingRateId { get; init; }
    public bool CanPromote => !ExistingRateId.HasValue;
}

public sealed class PrepareQuantitySurveyHistoricalRateRequest
{
    public QuantitySurveyHistoricalRateSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    [Required, StringLength(64)] public string SourceIntegrityHash { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public Guid? ProjectTypeId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    [Required, StringLength(1000)] public string ChangeReason { get; set; } = string.Empty;
}

public sealed class QuantitySurveyRateLibraryRevisionDto
{
    public Guid Id { get; init; }
    public Guid RateLibraryItemId { get; init; }
    public Guid? RateId { get; init; }
    public string Action { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? BeforeJson { get; init; }
    public string? AfterJson { get; init; }
    public DateTime CreatedAt { get; init; }
}
