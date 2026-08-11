using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed record QuantitySurveyDayworkVariationLookupDto(Guid Id, string Reference, string Title, string Type,
    Guid ContractId, string ContractNumber, Guid ContractorId, string Contractor, string Currency, decimal ValuedAmount, string Status);
public sealed record QuantitySurveyDayworkRateLookupDto(Guid Id, Guid ItemId, string Code, string Name,
    QuantitySurveyDayworkLineType LineType, Guid UnitOfMeasureId, string Unit, decimal UnitRate, string Currency, DateTime EffectiveFrom, DateTime? EffectiveTo);

public sealed class QuantitySurveyDayworkWorkspaceDto
{
    public IReadOnlyList<QuantitySurveyDayworkVariationLookupDto> Variations { get; init; } = [];
    public IReadOnlyList<QuantitySurveyDayworkRateLookupDto> Rates { get; init; } = [];
    public IReadOnlyList<QuantitySurveyDayworkSheetDto> Sheets { get; init; } = [];
}

public sealed class SaveQuantitySurveyDayworkRequest
{
    public Guid? Id { get; init; }
    public Guid ClientRequestId { get; init; }
    public Guid VariationOrderId { get; init; }
    public DateTime WorkDate { get; init; }
    [Required, StringLength(200, MinimumLength = 2)] public string WorkLocation { get; init; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 10)] public string Description { get; init; } = string.Empty;
    public string? RowVersion { get; init; }
    [MinLength(1)] public IReadOnlyList<SaveQuantitySurveyDayworkLineRequest> Lines { get; init; } = [];
}

public sealed class SaveQuantitySurveyDayworkLineRequest
{
    public Guid RateLibraryRateId { get; init; }
    [Range(typeof(decimal), "0.0001", "999999999999")] public decimal Quantity { get; init; }
    [StringLength(1000)] public string? Note { get; init; }
}

public class QuantitySurveyDayworkActionRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; init; } = string.Empty;
}

public sealed class QuantitySurveyDayworkLineDto
{
    public Guid Id { get; init; }
    public QuantitySurveyDayworkLineType LineType { get; init; }
    public Guid RateLibraryRateId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitRate { get; init; }
    public decimal Amount { get; init; }
    public string? Note { get; init; }
}

public sealed class QuantitySurveyDayworkEvidenceDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string ChecksumSha256 { get; init; } = string.Empty;
}

public sealed class QuantitySurveyDayworkSheetDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid VariationOrderId { get; init; }
    public string VariationReference { get; init; } = string.Empty;
    public string VariationType { get; init; } = string.Empty;
    public string SheetNumber { get; init; } = string.Empty;
    public DateTime WorkDate { get; init; }
    public string WorkLocation { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public QuantitySurveyDayworkSheetStatus Status { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public string Contractor { get; init; } = string.Empty;
    public DateTime? ContractorSignedAt { get; init; }
    public DateTime? VerifiedAt { get; init; }
    public string? VerificationNote { get; init; }
    public string? RejectionReason { get; init; }
    public bool CertificateEligible { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveyDayworkLineDto> Lines { get; init; } = [];
    public IReadOnlyList<QuantitySurveyDayworkEvidenceDto> Evidence { get; init; } = [];
}
