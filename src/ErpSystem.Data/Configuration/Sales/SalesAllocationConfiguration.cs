using ErpSystem.Core.Entities.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Sales;

public sealed class SalesAllocationConfiguration : IEntityTypeConfiguration<SalesAllocation>
{
    public void Configure(EntityTypeBuilder<SalesAllocation> builder)
    {
        builder.ToTable("SalesAllocations");

        builder.HasIndex(x => new { x.TenantId, x.SaleableSourceId, x.SourceItemId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [Status] IN ('Reserved','Allocated','Sold','Leased','PendingApproval','Approved')");
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.BusinessPartnerId });
        builder.HasIndex(x => new { x.TenantId, x.SalesOrderId });
        builder.HasIndex(x => new { x.TenantId, x.SalesAgreementId });

        builder.Property(x => x.SourceCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SourceType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.AdapterKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.SourceItemId).HasMaxLength(150).IsRequired();
        builder.Property(x => x.SourceItemCode).HasMaxLength(100);
        builder.Property(x => x.SourceItemName).HasMaxLength(250).IsRequired();
        builder.Property(x => x.SourceItemType).HasMaxLength(80);
        builder.Property(x => x.CustomerName).HasMaxLength(200);
        builder.Property(x => x.AllocationType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(10);
        builder.Property(x => x.EstimatedValue).HasColumnType("decimal(18,2)");
        builder.Property(x => x.AgreedValue).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.ReleaseReason).HasMaxLength(500);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SaleableSource)
            .WithMany()
            .HasForeignKey(x => x.SaleableSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BusinessPartner)
            .WithMany()
            .HasForeignKey(x => x.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SalesOrder)
            .WithMany()
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SalesAgreement)
            .WithMany()
            .HasForeignKey(x => x.SalesAgreementId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.History)
            .WithOne(x => x.SalesAllocation)
            .HasForeignKey(x => x.SalesAllocationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SalesAllocationHistoryConfiguration : IEntityTypeConfiguration<SalesAllocationHistory>
{
    public void Configure(EntityTypeBuilder<SalesAllocationHistory> builder)
    {
        builder.ToTable("SalesAllocationHistories");
        builder.HasIndex(x => new { x.TenantId, x.SalesAllocationId, x.PerformedAt });
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FromStatus).HasMaxLength(50);
        builder.Property(x => x.ToStatus).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PerformedByName).HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
