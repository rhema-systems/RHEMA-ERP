using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed record QuantitySurveyMaterialValuationLookupDto(
    Guid WorksheetId, Guid InterimValuationId, Guid ContractId, string Label,
    string ContractNumber, string ContractorName, string Currency, string Status);

public sealed record QuantitySurveyMaterialSourceLookupDto(
    Guid InventoryItemId, string ItemCode, string ItemName, string UnitOfMeasure,
    Guid? ApprovedRateId, decimal? ApprovedUnitRate, string Currency);

public sealed record QuantitySurveyMaterialIssueLookupDto(
    Guid IssueVoucherLineId, Guid InventoryItemId, string VoucherNumber, string ItemCode,
    string ItemName, string UnitOfMeasure, decimal Quantity, decimal UnitCost,
    decimal TotalValue, string IntegrityHash);

public sealed record QuantitySurveyMaterialEvidenceLookupDto(
    Guid EvidenceId, Guid WorksheetId, QuantitySurveyValuationEvidenceType EvidenceType,
    string Label, string FileName, string ChecksumSha256);

public sealed class QuantitySurveyMaterialReconciliationWorkspaceDto
{
    public IReadOnlyList<QuantitySurveyMaterialValuationLookupDto> Valuations { get; init; } = [];
    public IReadOnlyList<QuantitySurveyMaterialSourceLookupDto> MaterialSources { get; init; } = [];
    public IReadOnlyList<QuantitySurveyMaterialIssueLookupDto> InventoryIssues { get; init; } = [];
    public IReadOnlyList<QuantitySurveyMaterialEvidenceLookupDto> Evidence { get; init; } = [];
    public IReadOnlyList<QuantitySurveyMaterialReconciliationDto> Reconciliations { get; init; } = [];
}

public sealed class SaveQuantitySurveyMaterialReconciliationRequest
{
    public Guid ClientRequestId { get; init; }
    public Guid ValuationWorksheetId { get; init; }
    public string? RowVersion { get; init; }
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; init; } = string.Empty;
    [StringLength(2000)] public string? Notes { get; init; }
    [MinLength(1)] public IReadOnlyList<SaveQuantitySurveyMaterialReconciliationLineRequest> Lines { get; init; } = [];
}

public sealed class SaveQuantitySurveyMaterialReconciliationLineRequest
{
    public QuantitySurveyMaterialLineType LineType { get; init; }
    public Guid InventoryItemId { get; init; }
    [Range(typeof(decimal), "0.0001", "999999999999")] public decimal Quantity { get; init; }
    [Range(typeof(decimal), "0", "999999999999")] public decimal? DeliveredUnitCost { get; init; }
    public Guid? ApprovedRateId { get; init; }
    public Guid? InventoryIssueVoucherLineId { get; init; }
    public Guid? ValuationEvidenceId { get; init; }
}

public class QuantitySurveyMaterialReconciliationActionRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; init; } = string.Empty;
}

public sealed class QuantitySurveyMaterialContractorConfirmationRequest : QuantitySurveyMaterialReconciliationActionRequest
{
    [Required, StringLength(1000, MinimumLength = 10)] public string Attestation { get; init; } = string.Empty;
}

public sealed class QuantitySurveyMaterialReconciliationLineDto
{
    public Guid Id { get; init; }
    public int Sequence { get; init; }
    public QuantitySurveyMaterialLineType LineType { get; init; }
    public Guid InventoryItemId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string UnitOfMeasure { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal? DeliveredUnitCost { get; init; }
    public Guid? ApprovedRateId { get; init; }
    public decimal? ApprovedUnitRate { get; init; }
    public decimal AppliedUnitRate { get; init; }
    public decimal TotalValue { get; init; }
    public Guid? InventoryIssueVoucherLineId { get; init; }
    public string? IssueVoucherNumber { get; init; }
    public Guid? ValuationEvidenceId { get; init; }
    public string SourceHash { get; init; } = string.Empty;
}

public sealed class QuantitySurveyMaterialReconciliationDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ContractId { get; init; }
    public Guid ValuationWorksheetId { get; init; }
    public string ReconciliationNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractorName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public QuantitySurveyMaterialValuationBasis ValuationBasis { get; init; }
    public decimal MaterialOnSiteAmount { get; init; }
    public decimal MaterialOffSiteAmount { get; init; }
    public decimal TdcSuppliedDeductionAmount { get; init; }
    public DateTime? ContractorConfirmedAt { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public string? Notes { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveyMaterialReconciliationLineDto> Lines { get; init; } = [];
}

public sealed class QuantitySurveyMaterialReconciliationRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? BeforeJson { get; init; }
    public string AfterJson { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
