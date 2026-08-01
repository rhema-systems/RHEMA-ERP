using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementReceiptDocumentOverviewDto
{
    public Guid PurchaseOrderReceiptId { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public string PurchaseOrderNumber { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public string ReceiptStatus { get; init; } = string.Empty;
    public string InspectionStatus { get; init; } = string.Empty;
    public Guid ConfigurationProfileId { get; init; }
    public int ConfigurationProfileVersion { get; init; }
    public ProcurementReceiptDocumentType ConfiguredDocumentType { get; init; }
    public ProcurementReceiptCoexistenceRule CoexistenceRule { get; init; }
    public IReadOnlyList<string> DecisionKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RequiredEvidence { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AvailableEvidence { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ProcurementReceiptDocumentCheckDto> Checks { get; init; } = Array.Empty<ProcurementReceiptDocumentCheckDto>();
    public IReadOnlyList<ProcurementReceiptDocumentDto> Documents { get; init; } = Array.Empty<ProcurementReceiptDocumentDto>();
    public IReadOnlyList<string> AllowedActions { get; init; } = Array.Empty<string>();
    public bool IsReconciled => Checks.All(item => item.Passed) && Documents.All(item =>
        item.ReconciliationStatus is ProcurementReceiptDocumentReconciliationStatus.Reconciled or
            ProcurementReceiptDocumentReconciliationStatus.Cancelled);
}

public sealed class ProcurementReceiptDocumentDto
{
    public Guid Id { get; init; }
    public ProcurementReceiptDocumentKind DocumentKind { get; init; }
    public string DocumentNumber { get; init; } = string.Empty;
    public string TemplateCode { get; init; } = string.Empty;
    public ProcurementReceiptDocumentStatus Status { get; init; }
    public ProcurementReceiptDocumentReconciliationStatus ReconciliationStatus { get; init; }
    public string? ReconciliationMessage { get; init; }
    public DateTime? ReconciledAtUtc { get; init; }
    public string PreparedByName { get; init; } = string.Empty;
    public DateTime PreparedAtUtc { get; init; }
    public string? IssuedByName { get; init; }
    public DateTime? IssuedAtUtc { get; init; }
    public string? CancelledByName { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
    public string? CancellationReason { get; init; }
    public Guid? CentralDocumentRecordId { get; init; }
    public Guid? CentralDocumentVersionId { get; init; }
    public string? PdfUrl { get; init; }
    public string SourceIntegrityHash { get; init; } = string.Empty;
    public IReadOnlyList<string> RequiredSignatures { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedSignatureRoles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ProcurementReceiptDocumentSignatureDto> Signatures { get; init; } = Array.Empty<ProcurementReceiptDocumentSignatureDto>();
    public IReadOnlyList<ProcurementReceiptDocumentActionDto> Actions { get; init; } = Array.Empty<ProcurementReceiptDocumentActionDto>();
    public IReadOnlyList<string> AllowedActions { get; init; } = Array.Empty<string>();
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ProcurementReceiptDocumentSignatureDto
{
    public Guid Id { get; init; }
    public string RequiredRole { get; init; } = string.Empty;
    public Guid SignedByUserId { get; init; }
    public string SignedByName { get; init; } = string.Empty;
    public DateTime SignedAtUtc { get; init; }
    public string? Comment { get; init; }
    public string IntegrityHash { get; init; } = string.Empty;
}

public sealed class ProcurementReceiptDocumentActionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string FromStatus { get; init; } = string.Empty;
    public string ToStatus { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public DateTime OccurredAtUtc { get; init; }
    public string? Reason { get; init; }
    public string IntegrityHash { get; init; } = string.Empty;
}

public sealed class ProcurementReceiptDocumentCheckDto
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class SignProcurementReceiptDocumentRequest
{
    [Required, StringLength(200)] public string RequiredRole { get; set; } = string.Empty;
    [StringLength(1000)] public string? Comment { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class IssueProcurementReceiptDocumentRequest
{
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class CancelProcurementReceiptDocumentRequest
{
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementReceiptDocumentFileDto
{
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string ContentType { get; init; } = "application/pdf";
    public string FileName { get; init; } = string.Empty;
}
