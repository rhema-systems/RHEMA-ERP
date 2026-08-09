using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public enum QuantitySurveyRateItemCategory
{
    StandardItem = 0,
    Material = 1,
    Labour = 2,
    Plant = 3,
    Equipment = 4,
    Subcontract = 5
}

public enum QuantitySurveyRateSourceType
{
    Baseline = 0,
    InventoryStandardCost = 1,
    InventoryAverageCost = 2,
    InventoryLastPurchaseCost = 3,
    PurchaseOrder = 4,
    SupplierQuotation = 5,
    ContractorQuotation = 6,
    FrameworkAgreement = 7,
    HistoricalProject = 8,
    LabourSchedule = 9,
    PlantHire = 10,
    MarketSurvey = 11,
    RateBuildUp = 12
}

public enum QuantitySurveyRateLifecycleStatus
{
    Draft = 0,
    Published = 1,
    Retired = 2
}

public enum QuantitySurveyMarketSurveyPriceBasis
{
    CurrentMarketPrice = 0,
    AverageSurveyPrice = 1,
    LowestSurveyPrice = 2,
    HighestSurveyPrice = 3,
    ForecastedPrice = 4
}

public enum QuantitySurveyHistoricalRateSourceType
{
    CompletedBoqLine = 0,
    CertifiedValuation = 1,
    ProcurementPrice = 2,
    ActualProjectCost = 3
}

[Table("QuantitySurveyRateLibraryItems")]
public sealed class QuantitySurveyRateLibraryItem : TenantEntity
{
    [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    public QuantitySurveyRateItemCategory Category { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public Guid? ProjectCatalogEntryId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public bool IsActive { get; set; } = true;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public UnitOfMeasure UnitOfMeasure { get; set; } = null!;
    public ProjectCatalogEntry? ProjectCatalogEntry { get; set; }
    public InventoryItem? InventoryItem { get; set; }
    public ICollection<QuantitySurveyRateLibraryRate> Rates { get; set; } = new List<QuantitySurveyRateLibraryRate>();
}

[Table("QuantitySurveyRateLibraryRates")]
public sealed class QuantitySurveyRateLibraryRate : TenantEntity
{
    public Guid RateLibraryItemId { get; set; }
    public int Version { get; set; } = 1;
    [Column(TypeName = "decimal(18,6)")] public decimal UnitRate { get; set; }
    public Guid CurrencyId { get; set; }
    [Required, StringLength(3)] public string CurrencyCodeSnapshot { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public Guid? ProjectTypeId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public QuantitySurveyRateSourceType SourceType { get; set; }
    [StringLength(250)] public string? SourceReference { get; set; }
    public DateTime SourceDate { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    public Guid? MarketAnalysisId { get; set; }
    [StringLength(50)] public string? MarketAnalysisCodeSnapshot { get; set; }
    public Guid? PreviousRateId { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal? PreviousUnitRate { get; set; }
    [StringLength(3)] public string? PreviousCurrencyCodeSnapshot { get; set; }
    public int? MarketSurveyQuoteCount { get; set; }
    public DateTime? NextReviewDueAt { get; set; }
    public QuantitySurveyHistoricalRateSourceType? HistoricalSourceType { get; set; }
    public Guid? HistoricalSourceId { get; set; }
    public Guid? HistoricalProjectId { get; set; }
    [StringLength(50)] public string? HistoricalProjectCodeSnapshot { get; set; }
    [StringLength(250)] public string? HistoricalSourceLabelSnapshot { get; set; }
    [StringLength(20)] public string? HistoricalUnitOfMeasureSnapshot { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal? HistoricalQuantity { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? HistoricalTotalAmount { get; set; }
    [StringLength(64)] public string? HistoricalSourceHash { get; set; }
    public Guid? RateBuildUpId { get; set; }
    public QuantitySurveyRateLifecycleStatus LifecycleStatus { get; set; }
    [StringLength(1000)] public string? ChangeReason { get; set; }
    public Guid PreparedById { get; set; }
    public DateTime PreparedAt { get; set; }
    public Guid? PublishedById { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? RetiredById { get; set; }
    public DateTime? RetiredAt { get; set; }
    [Required, StringLength(100)] public string AuditAction { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public QuantitySurveyRateLibraryItem RateLibraryItem { get; set; } = null!;
    public Currency Currency { get; set; } = null!;
    public ProjectType? ProjectType { get; set; }
    public Location? Location { get; set; }
    public BusinessPartner? BusinessPartner { get; set; }
    public CentralDocumentRecord? CentralDocumentRecord { get; set; }
    public CentralDocumentVersion? CentralDocumentVersion { get; set; }
    public MarketAnalysis? MarketAnalysis { get; set; }
    public QuantitySurveyRateLibraryRate? PreviousRate { get; set; }
    public Project? HistoricalProject { get; set; }
    public QuantitySurveyRateBuildUp? RateBuildUp { get; set; }
}

[Table("QuantitySurveyRateLibraryRevisions")]
public sealed class QuantitySurveyRateLibraryRevision : TenantEntity
{
    public Guid RateLibraryItemId { get; set; }
    public Guid? RateId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? AfterJson { get; set; }

    public QuantitySurveyRateLibraryItem RateLibraryItem { get; set; } = null!;
    public QuantitySurveyRateLibraryRate? Rate { get; set; }
}
