using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyRateBuildUpSourceDto
{
    public Guid RateLibraryItemId { get; init; }
    public Guid RateId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public QuantitySurveyRateItemCategory Category { get; init; }
    public string UnitOfMeasure { get; init; } = string.Empty;
    public int RateVersion { get; init; }
    public decimal UnitRate { get; init; }
    public Guid CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public Guid? ProjectTypeId { get; init; }
    public Guid? LocationId { get; init; }
    public Guid? BusinessPartnerId { get; init; }
}

public sealed class QuantitySurveyRateBuildUpContextDto
{
    public IReadOnlyList<QuantitySurveyRateComponent> AllowedComponents { get; init; } = [];
    public bool RequireProjectType { get; init; }
    public bool RequireLocation { get; init; }
    public bool AllowBusinessPartner { get; init; }
    public bool RequireBusinessPartner { get; init; }
    public IReadOnlyList<Guid> AllowedProjectTypeIds { get; init; } = [];
    public IReadOnlyList<Guid> AllowedLocationIds { get; init; } = [];
    public decimal MaximumOverheadPercent { get; init; }
    public decimal MaximumProfitPercent { get; init; }
    public decimal MaximumContingencyPercent { get; init; }
    public decimal MaximumWastagePercent { get; init; }
    public int DecimalPlaces { get; init; }
    public IReadOnlyList<QuantitySurveyRateBuildUpSourceDto> Sources { get; init; } = [];
}

public sealed class QuantitySurveyRateBuildUpLineRequest
{
    [Range(1, 1000)] public int Sequence { get; set; }
    public QuantitySurveyRateComponent Component { get; set; }
    public QuantitySurveyRateBuildUpCalculationMethod CalculationMethod { get; set; }
    public QuantitySurveyRateBuildUpPercentageBasis? PercentageBasis { get; set; }
    [StringLength(250)] public string? Description { get; set; }
    public Guid? SourceRateId { get; set; }
    [Range(typeof(decimal), "0", "9999999999999")] public decimal? Quantity { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal? Percentage { get; set; }
    [Range(typeof(decimal), "0", "9999999999999")] public decimal? FixedAmount { get; set; }
}

public class PreviewQuantitySurveyRateBuildUpRequest
{
    public DateTime SourceDate { get; set; }
    public Guid CurrencyId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public Guid? ProjectTypeId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    [MinLength(1), MaxLength(250)] public List<QuantitySurveyRateBuildUpLineRequest> Lines { get; set; } = [];
}

public sealed class PrepareQuantitySurveyRateBuildUpRequest : PreviewQuantitySurveyRateBuildUpRequest
{
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64, MinimumLength = 64)] public string PreviewIntegrityHash { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string ChangeReason { get; set; } = string.Empty;
}

public sealed class QuantitySurveyRateBuildUpPreviewLineDto
{
    public int Sequence { get; init; }
    public QuantitySurveyRateComponent Component { get; init; }
    public QuantitySurveyRateBuildUpCalculationMethod CalculationMethod { get; init; }
    public QuantitySurveyRateBuildUpPercentageBasis? PercentageBasis { get; init; }
    public string Description { get; init; } = string.Empty;
    public Guid? SourceRateLibraryItemId { get; init; }
    public Guid? SourceRateId { get; init; }
    public string? SourceItemCode { get; init; }
    public string? SourceItemName { get; init; }
    public string? SourceUnitOfMeasure { get; init; }
    public int? SourceRateVersion { get; init; }
    public decimal? SourceUnitRate { get; init; }
    public decimal? Quantity { get; init; }
    public decimal? Percentage { get; init; }
    public decimal? FixedAmount { get; init; }
    public decimal BasisAmount { get; init; }
    public decimal CalculatedAmount { get; init; }
}

public sealed class QuantitySurveyRateBuildUpPreviewDto
{
    public Guid RateLibraryItemId { get; init; }
    public DateTime SourceDate { get; init; }
    public Guid CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public Guid ConfigurationProfileId { get; init; }
    public Guid ConfigurationDecisionId { get; init; }
    public int ConfigurationProfileVersion { get; init; }
    public int DecimalPlaces { get; init; }
    public decimal MaterialSubtotal { get; init; }
    public decimal DirectCost { get; init; }
    public decimal AddOnCost { get; init; }
    public decimal UnitRate { get; init; }
    public string IntegrityHash { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveyRateBuildUpPreviewLineDto> Lines { get; init; } = [];
}

public sealed class QuantitySurveyRateBuildUpDto
{
    public Guid Id { get; init; }
    public Guid RateLibraryItemId { get; init; }
    public int Version { get; init; }
    public Guid ClientRequestId { get; init; }
    public string BuildUpNumber { get; init; } = string.Empty;
    public DateTime SourceDate { get; init; }
    public Guid CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public int ConfigurationProfileVersion { get; init; }
    public int DecimalPlaces { get; init; }
    public decimal MaterialSubtotal { get; init; }
    public decimal DirectCost { get; init; }
    public decimal AddOnCost { get; init; }
    public decimal UnitRate { get; init; }
    public string CalculationHash { get; init; } = string.Empty;
    public Guid? CentralDocumentRecordId { get; init; }
    public Guid? CentralDocumentVersionId { get; init; }
    public string ChangeReason { get; init; } = string.Empty;
    public Guid PreparedById { get; init; }
    public DateTime PreparedAt { get; init; }
    public QuantitySurveyRateDto GeneratedRate { get; init; } = null!;
    public IReadOnlyList<QuantitySurveyRateBuildUpPreviewLineDto> Lines { get; init; } = [];
}
