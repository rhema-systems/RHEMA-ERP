using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Finance;

public sealed class FinancePurchasingOptionalApprovalConfiguration :
    IEntityTypeConfiguration<FinancePurchaseOrder>, IEntityTypeConfiguration<FinancePurchaseOrderReceipt>
{
    public void Configure(EntityTypeBuilder<FinancePurchaseOrder> builder)
    {
        builder.Property(entity => entity.ApprovalRequired).HasDefaultValue(true);
        builder.ToTable("FinancePurchaseOrders", table => table.HasTrigger("TR_FinancePurchaseOrders_ApprovalPolicy"));
    }

    public void Configure(EntityTypeBuilder<FinancePurchaseOrderReceipt> builder)
    {
        builder.Property(entity => entity.ApprovalRequired).HasDefaultValue(true);
        builder.ToTable("FinancePurchaseOrderReceipts", table => table.HasTrigger("TR_FinancePurchaseOrderReceipts_ApprovalPolicy"));
    }
}
