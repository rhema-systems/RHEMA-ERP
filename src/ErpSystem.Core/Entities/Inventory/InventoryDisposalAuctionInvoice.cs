using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Entities.Inventory;

public sealed class InventoryDisposalAuctionInvoice : TenantEntity
{
    public Guid InventoryDisposalCaseId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid CreatedByUserId { get; set; }
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string PayloadHash { get; set; } = string.Empty;
    [Required] public string InvoiceEconomicsJson { get; set; } = string.Empty;
    public InventoryDisposalCase Disposal { get; set; } = null!;
    public Invoice Invoice { get; set; } = null!;
}
