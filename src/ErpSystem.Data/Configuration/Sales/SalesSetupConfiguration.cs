using ErpSystem.Core.Entities.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Sales;

public sealed class SalesSaleableSourceConfiguration : IEntityTypeConfiguration<SalesSaleableSource>
{
    public void Configure(EntityTypeBuilder<SalesSaleableSource> builder)
    {
        builder.ToTable("SalesSaleableSources");
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IsActive, x.SortOrder });
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.SourceType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.AdapterKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Icon).HasMaxLength(80);
        builder.Property(x => x.ColorCode).HasMaxLength(20);
        builder.Property(x => x.SupportedTransactionTypes).HasMaxLength(300);
        builder.Property(x => x.DefaultCurrency).HasMaxLength(10);
        builder.Property(x => x.DefaultWorkflowEntityType).HasMaxLength(80);
        builder.Property(x => x.RequiresExternalModule).HasDefaultValue(false);
        builder.Property(x => x.IsSystemSource).HasDefaultValue(false);
        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
