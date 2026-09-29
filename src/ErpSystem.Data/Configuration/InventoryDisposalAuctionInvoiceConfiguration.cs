using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryDisposalAuctionInvoiceConfiguration : IEntityTypeConfiguration<InventoryDisposalAuctionInvoice>
{
    public void Configure(EntityTypeBuilder<InventoryDisposalAuctionInvoice> builder)
    {
        builder.ToTable("InventoryDisposalAuctionInvoices", table => table.HasTrigger("TR_InventoryDisposalAuctionInvoices_Guard"));
        builder.HasIndex(value => value.InventoryDisposalCaseId).IsUnique();
        builder.HasIndex(value => value.InvoiceId).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.IdempotencyKey }).IsUnique();
        builder.HasOne(value => value.Disposal).WithMany().HasForeignKey(value => value.InventoryDisposalCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.Invoice).WithMany().HasForeignKey(value => value.InvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}
