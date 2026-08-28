using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public enum QuantitySurveyRateBuildUpCalculationMethod
{
    QuantityTimesPublishedRate = 0,
    Percentage = 1,
    FixedAmount = 2
}

public enum QuantitySurveyRateBuildUpPercentageBasis
{
    MaterialSubtotal = 0,
    DirectCost = 1,
    RunningTotal = 2
}

[Table("QuantitySurveyRateBuildUps")]
public sealed class QuantitySurveyRateBuildUp : TenantEntity
{
    public Guid RateLibraryItemId { get; set; }
    public int Version { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(80)] public string BuildUpNumber { get; set; } = string.Empty;
    public DateTime SourceDate { get; set; }
    public Guid CurrencyId { get; set; }
    [Required, StringLength(3)] public string CurrencyCodeSnapshot { get; set; } = string.Empty;
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public int ConfigurationProfileVersion { get; set; }
    public int DecimalPlaces { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal MaterialSubtotal { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal DirectCost { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal AddOnCost { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal UnitRate { get; set; }
    [Required, StringLength(64)] public string CalculationHash { get; set; } = string.Empty;
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    [Required, StringLength(1000)] public string ChangeReason { get; set; } = string.Empty;
    public Guid PreparedById { get; set; }
    public DateTime PreparedAt { get; set; }
    [Required, StringLength(100)] public string AuditAction { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public QuantitySurveyRateLibraryItem RateLibraryItem { get; set; } = null!;
    public QuantitySurveyRateLibraryRate? GeneratedRate { get; set; }
    public Currency Currency { get; set; } = null!;
    public QuantitySurveyConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public QuantitySurveyConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public CentralDocumentRecord? CentralDocumentRecord { get; set; }
    public CentralDocumentVersion? CentralDocumentVersion { get; set; }
    public ICollection<QuantitySurveyRateBuildUpLine> Lines { get; set; } = new List<QuantitySurveyRateBuildUpLine>();
}

[Table("QuantitySurveyRateBuildUpLines")]
public sealed class QuantitySurveyRateBuildUpLine : TenantEntity
{
    public Guid RateBuildUpId { get; set; }
    public int Sequence { get; set; }
    public QuantitySurveyRateComponent Component { get; set; }
    public QuantitySurveyRateBuildUpCalculationMethod CalculationMethod { get; set; }
    public QuantitySurveyRateBuildUpPercentageBasis? PercentageBasis { get; set; }
    [Required, StringLength(250)] public string Description { get; set; } = string.Empty;
    public Guid? SourceRateLibraryItemId { get; set; }
    public Guid? SourceRateId { get; set; }
    [StringLength(50)] public string? SourceItemCodeSnapshot { get; set; }
    [StringLength(200)] public string? SourceItemNameSnapshot { get; set; }
    [StringLength(20)] public string? SourceUnitOfMeasureSnapshot { get; set; }
    public int? SourceRateVersion { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal? SourceUnitRate { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal? InputQuantity { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal? InputPercentage { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal? InputFixedAmount { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal BasisAmount { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal CalculatedAmount { get; set; }

    public QuantitySurveyRateBuildUp RateBuildUp { get; set; } = null!;
    public QuantitySurveyRateLibraryItem? SourceRateLibraryItem { get; set; }
    public QuantitySurveyRateLibraryRate? SourceRate { get; set; }
}
