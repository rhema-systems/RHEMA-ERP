namespace ErpSystem.Core.DTOs.Finance;

public sealed class VendorInvoiceDistributionDto
{
    public Guid InvoiceId { get; set; }
    public string Status { get; set; } = "Proposed";
    public string Currency { get; set; } = string.Empty;
    public string Basis { get; set; } = string.Empty;
    public Guid? JournalEntryId { get; set; }
    public string? JournalEntryNumber { get; set; }
    public List<VendorInvoiceDistributionLineDto> Lines { get; set; } = new();
    public decimal TotalDebit => Lines.Sum(line => line.Debit);
    public decimal TotalCredit => Lines.Sum(line => line.Credit);
}

public sealed class VendorInvoiceDistributionLineDto
{
    public string LineId { get; set; } = string.Empty;
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
