namespace ErpSystem.Core.DTOs.Finance;

public sealed class InvoiceDistributionDto
{
    public Guid InvoiceId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public bool IsEstimated { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool IsBalanced => TotalDebit == TotalCredit && TotalDebit > 0;
    public IReadOnlyList<InvoiceDistributionLineDto> Lines { get; set; } = [];
}

public sealed class InvoiceDistributionLineDto
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public Guid? SourceLineId { get; set; }
}
