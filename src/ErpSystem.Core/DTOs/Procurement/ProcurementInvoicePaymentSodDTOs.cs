namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementInvoicePaymentSodReadinessDto
{
    public string SourceType { get; init; } = string.Empty;
    public Guid SourceId { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public Guid CurrentActorUserId { get; init; }
    public bool CanApprove { get; init; }
    public bool HasInvoiceProcessorLineage { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; init; }
    public Guid? ControlEventId { get; init; }
    public Guid? PolicySetId { get; init; }
    public string? PolicyCode { get; init; }
    public int? PolicyVersion { get; init; }
    public Guid? RuleId { get; init; }
    public string? RuleCode { get; init; }
    public IReadOnlyList<string> DecisionKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ProcurementInvoicePaymentSodInvoiceDto> Invoices { get; init; } =
        Array.Empty<ProcurementInvoicePaymentSodInvoiceDto>();
}

public sealed class ProcurementInvoicePaymentSodInvoiceDto
{
    public Guid VendorInvoiceId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public Guid? InvoiceProcessorUserId { get; init; }
    public DateTime? SubmittedAtUtc { get; init; }
    public bool ProcessorLineagePresent { get; init; }
    public bool ConflictsWithCurrentActor { get; init; }
}
