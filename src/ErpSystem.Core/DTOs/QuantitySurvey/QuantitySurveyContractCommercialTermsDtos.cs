using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed record QuantitySurveyContractPaymentTermLookupDto(
    Guid Id, string Code, string Name, int DueDays, string ApplicableTo);

public sealed record QuantitySurveyContractDocumentLookupDto(
    Guid Id, string DocumentType, string FileName, Guid CentralDocumentRecordId,
    Guid CentralDocumentVersionId);

public sealed class QuantitySurveyContractCommercialTermsDto
{
    public Guid ContractId { get; set; }
    public Guid ProjectId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string ContractTitle { get; set; } = string.Empty;
    public string ContractStatus { get; set; } = string.Empty;
    public decimal ContractValue { get; set; }
    public string Currency { get; set; } = string.Empty;
    public Guid? PaymentTermId { get; set; }
    public decimal ProvisionalSumAmount { get; set; }
    public decimal ContingencyAmount { get; set; }
    public decimal RetentionPercentage { get; set; }
    public int? DefectsLiabilityDays { get; set; }
    public string? RetentionClause { get; set; }
    public bool AllowSectionalTakeover { get; set; }
    public string? SectionalTakeoverClause { get; set; }
    public bool AllowSubcontracting { get; set; }
    public Guid? SubcontractPaymentTermId { get; set; }
    public string? SubcontractTerms { get; set; }
    public int? ClaimNoticePeriodDays { get; set; }
    public string? ClaimClause { get; set; }
    public Guid? CommercialTermsContractDocumentId { get; set; }
    public Guid? ConfigurationProfileId { get; set; }
    public Guid? ContractControlsDecisionId { get; set; }
    public Guid? RetentionDecisionId { get; set; }
    public string? PolicyHash { get; set; }
    public DateTime? ConfiguredAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class QuantitySurveyContractCommercialTermsWorkspaceDto
{
    public QuantitySurveyContractCommercialTermsDto Terms { get; set; } = new();
    public IReadOnlyList<QuantitySurveyContractPaymentTermLookupDto> PaymentTerms { get; set; } = [];
    public IReadOnlyList<QuantitySurveyContractDocumentLookupDto> ContractDocuments { get; set; } = [];
    public bool IsEditable { get; set; }
    public decimal MaximumRetentionPercentage { get; set; }
    public int MaximumDefectsLiabilityDays { get; set; }
    public decimal SectionalTakeoverReleasePercentage { get; set; }
    public bool AllowRetentionBond { get; set; }
    public bool ControlProvisionalSums { get; set; }
    public bool ControlContingencies { get; set; }
    public bool ControlDefectsLiability { get; set; }
    public bool ControlSectionalTakeover { get; set; }
    public bool ControlSubcontracts { get; set; }
    public bool ControlClaimClauses { get; set; }
    public bool RequireCommercialTermsDocument { get; set; }
    public IReadOnlyList<string> ReadinessBlockers { get; set; } = [];
}

public sealed class ConfigureQuantitySurveyContractCommercialTermsRequest
{
    [Required] public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required] public Guid PaymentTermId { get; set; }
    [Range(0, double.MaxValue)] public decimal ProvisionalSumAmount { get; set; }
    [Range(0, double.MaxValue)] public decimal ContingencyAmount { get; set; }
    [Range(0, 100)] public decimal RetentionPercentage { get; set; }
    [Range(0, 3650)] public int? DefectsLiabilityDays { get; set; }
    [MaxLength(2000)] public string? RetentionClause { get; set; }
    public bool AllowSectionalTakeover { get; set; }
    [MaxLength(2000)] public string? SectionalTakeoverClause { get; set; }
    public bool AllowSubcontracting { get; set; }
    public Guid? SubcontractPaymentTermId { get; set; }
    [MaxLength(2000)] public string? SubcontractTerms { get; set; }
    [Range(0, 3650)] public int? ClaimNoticePeriodDays { get; set; }
    [MaxLength(2000)] public string? ClaimClause { get; set; }
    public Guid? CommercialTermsContractDocumentId { get; set; }
}
