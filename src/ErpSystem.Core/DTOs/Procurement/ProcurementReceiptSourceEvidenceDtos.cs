using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementReceiptSourceEvidenceOverviewDto
{
    public Guid ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public bool WaybillRequired { get; set; } = true;
    public bool WaybillReady { get; set; }
    public bool InspectionEvidenceLocked { get; set; }
    public bool CanUpload { get; set; }
    public string FinanceOwnershipNotice { get; set; } =
        "A VAT invoice copy is supporting receipt evidence only. Finance/AP owns invoice recognition, matching, posting, payment and reversal.";
    public IReadOnlyList<ProcurementReceiptSourceEvidenceDto> Evidence { get; set; } =
        Array.Empty<ProcurementReceiptSourceEvidenceDto>();
}

public sealed class ProcurementReceiptSourceEvidenceDto
{
    public Guid Id { get; set; }
    public ProcurementReceiptSourceEvidenceKind EvidenceKind { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ChecksumSha256 { get; set; } = string.Empty;
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
}

public sealed class UploadProcurementReceiptSourceEvidenceRequest
{
    [Required]
    public ProcurementReceiptSourceEvidenceKind EvidenceKind { get; set; }

    [Required, StringLength(100, MinimumLength = 2)]
    public string ReferenceNumber { get; set; } = string.Empty;

    [Required]
    public DateTime DocumentDate { get; set; }

    [Required]
    public Guid ClientRequestId { get; set; }
}

public sealed class ProcurementReceiptSourceEvidenceUploadCommand
{
    public required ProcurementReceiptSourceEvidenceKind EvidenceKind { get; init; }
    public required string ReferenceNumber { get; init; }
    public required DateTime DocumentDate { get; init; }
    public required Guid ClientRequestId { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Content { get; init; }
}
