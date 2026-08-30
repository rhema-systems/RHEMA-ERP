namespace ErpSystem.Core.DTOs.Procurement;

public enum ProcurementDocumentFamily
{
    Tender = 0,
    Evaluation = 1,
    Approval = 2,
    Supplier = 3,
    Contract = 4,
    GoodsReceiptNote = 5,
    MaterialReceiptNote = 6,
    VendorInvoice = 7,
    Disposal = 8,
    Requisition = 9
}
public sealed class ProcurementDocumentFamilyDto
{
    public ProcurementDocumentFamily Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public string AccessProfile { get; set; } = string.Empty;
    public IReadOnlyList<string> Classifications { get; set; } = [];
    public bool CanRead { get; set; }
    public bool CanUpload { get; set; }
}

public sealed class ProcurementDocumentSourceOptionDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class ProcurementManagedDocumentDto
{
    public Guid DocumentRecordId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public ProcurementDocumentFamily Family { get; set; }
    public Guid SourceRecordId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public string DocumentReference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public string VersionNumber { get; set; } = string.Empty;
    public string VersionStatus { get; set; } = string.Empty;
    public string AccessProfile { get; set; } = string.Empty;
    public string RetentionStatus { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public string MalwareStatus { get; set; } = string.Empty;
    public bool MetadataComplete { get; set; }
    public bool AccessCovered { get; set; }
    public bool RetentionCovered { get; set; }
    public bool LegalHold { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
