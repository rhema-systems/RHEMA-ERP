namespace ErpSystem.Core.DTOs.Procurement;

public sealed class PurchaseReceiptDistributionDto
{
    public string Status { get; set; } = "Proposed";
    public string Currency { get; set; } = string.Empty;
    public string Basis { get; set; } = string.Empty;
    public Guid? JournalEntryId { get; set; }
    public string? JournalEntryNumber { get; set; }
    public bool CanEdit { get; set; }
    public bool HasOverrides { get; set; }
    public bool NeedsReview { get; set; }
    public string? EditBlockReason { get; set; }
    public string Version { get; set; } = string.Empty;
    public string BasisVersion { get; set; } = string.Empty;
    public List<PurchaseReceiptDistributionGroupDto> Groups { get; set; } = new();
    public List<PurchaseReceiptDistributionLineDto> Lines { get; set; } = new();
    public decimal TotalDebit => Lines.Sum(line => line.Debit);
    public decimal TotalCredit => Lines.Sum(line => line.Credit);
}

public sealed class PurchaseReceiptDistributionLineDto
{
    public Guid LineId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public sealed class PurchaseReceiptDistributionGroupDto
{
    public Guid InventoryItemId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public Guid DefaultAccountId { get; set; }
}

public sealed class PurchaseReceiptDistributionAccountDto
{
    public Guid Id { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
}

public class PurchaseReceiptDistributionVersionRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public string Version { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    public string BasisVersion { get; set; } = string.Empty;
}

public sealed class SavePurchaseReceiptDistributionRequest : PurchaseReceiptDistributionVersionRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public List<SavePurchaseReceiptDistributionLine> Lines { get; set; } = new();
}

public sealed class SavePurchaseReceiptDistributionLine
{
    public Guid LineId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}
