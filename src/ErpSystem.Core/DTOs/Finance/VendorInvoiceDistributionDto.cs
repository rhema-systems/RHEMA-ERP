namespace ErpSystem.Core.DTOs.Finance;

public sealed class VendorInvoiceDistributionDto
{
    public Guid InvoiceId { get; set; }
    public string Status { get; set; } = "Proposed";
    public string Currency { get; set; } = string.Empty;
    public string Basis { get; set; } = string.Empty;
    public Guid? JournalEntryId { get; set; }
    public string? JournalEntryNumber { get; set; }
    public bool CanEdit { get; set; }
    public string? EditBlockReason { get; set; }
    public string Version { get; set; } = string.Empty;
    public string BasisVersion { get; set; } = string.Empty;
    public bool HasOverrides { get; set; }
    public bool NeedsReview { get; set; }
    public List<VendorInvoiceDistributionGroupDto> Groups { get; set; } = new();
    public List<VendorInvoiceDistributionLineDto> Lines { get; set; } = new();
    public decimal TotalDebit => Lines.Sum(line => line.Debit);
    public decimal TotalCredit => Lines.Sum(line => line.Credit);
}

public sealed class VendorInvoiceDistributionLineDto
{
    public string LineId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public Guid? SourceDocumentLineId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public sealed class VendorInvoiceDistributionGroupDto
{
    public string GroupId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AccountId { get; set; }
    public bool CanChangeAccount { get; set; }
    public string? AccountRestriction { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public sealed class SaveVendorInvoiceDistributionDto
{
    public string Version { get; set; } = string.Empty;
    public string BasisVersion { get; set; } = string.Empty;
    public List<SaveVendorInvoiceDistributionLineDto> Lines { get; set; } = new();
}

public sealed class SaveVendorInvoiceDistributionLineDto
{
    public Guid LineId { get; set; }
    public string GroupId { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}
