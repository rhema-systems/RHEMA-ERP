using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Finance;

public sealed class VendorInvoiceOptionalApprovalConfiguration : IEntityTypeConfiguration<VendorInvoice>
{
    public void Configure(EntityTypeBuilder<VendorInvoice> builder)
    {
        builder.ToTable("VendorInvoice", table => table.HasTrigger("TR_VendorInvoices_OptionalApproval"));
        builder.Property(x => x.ApprovalRequired).HasDefaultValue(true);
    }
}
