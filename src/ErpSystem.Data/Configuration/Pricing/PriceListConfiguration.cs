using ErpSystem.Core.Entities.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Pricing;

public class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.ToTable("PriceLists");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PriceListCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("USD");

        builder.Property(p => p.Notes)
            .HasMaxLength(2000);

        builder.Property(p => p.ApprovalComments)
            .HasMaxLength(1000);

        // Indexes
        builder.HasIndex(p => p.PriceListCode);
        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => p.Type);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.ApprovalStatus);
        builder.HasIndex(p => new { p.TenantId, p.PriceListCode }).IsUnique();

        // Relationships
        builder.HasOne(p => p.SupersededPriceList)
            .WithMany()
            .HasForeignKey(p => p.SupersededPriceListId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Lines)
            .WithOne(l => l.PriceList)
            .HasForeignKey(l => l.PriceListId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PriceListLineConfiguration : IEntityTypeConfiguration<PriceListLine>
{
    public void Configure(EntityTypeBuilder<PriceListLine> builder)
    {
        builder.ToTable("PriceListLines");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.UnitOfMeasure)
            .HasMaxLength(20)
            .HasDefaultValue("EA");

        builder.Property(l => l.BasePrice)
            .HasPrecision(18, 4);

        builder.Property(l => l.DiscountPercent)
            .HasPrecision(5, 2);

        builder.Property(l => l.NetPrice)
            .HasPrecision(18, 4);

        builder.Property(l => l.MinQuantity)
            .HasPrecision(18, 4);

        builder.Property(l => l.MaxQuantity)
            .HasPrecision(18, 4);

        builder.Property(l => l.SupplierItemCode)
            .HasMaxLength(100);

        builder.Property(l => l.MinimumOrderQuantity)
            .HasPrecision(18, 4);

        builder.Property(l => l.OrderMultiple)
            .HasPrecision(18, 4);

        builder.Property(l => l.PreviousPrice)
            .HasPrecision(18, 4);

        builder.Property(l => l.PriceChangePercent)
            .HasPrecision(5, 2);

        builder.Property(l => l.Notes)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(l => l.PriceListId);
        builder.HasIndex(l => l.InventoryItemId);
        builder.HasIndex(l => l.TenantId);
        builder.HasIndex(l => new { l.PriceListId, l.InventoryItemId, l.MinQuantity }).IsUnique();
    }
}

public class CustomerGroupConfiguration : IEntityTypeConfiguration<CustomerGroup>
{
    public void Configure(EntityTypeBuilder<CustomerGroup> builder)
    {
        builder.ToTable("CustomerGroups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.GroupCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(g => g.Description)
            .HasMaxLength(1000);

        builder.Property(g => g.DefaultDiscountPercent)
            .HasPrecision(5, 2);

        builder.Property(g => g.DefaultPaymentTerms)
            .HasMaxLength(50);

        builder.Property(g => g.DefaultCreditLimit)
            .HasPrecision(18, 2);

        // Indexes
        builder.HasIndex(g => g.TenantId);
        builder.HasIndex(g => new { g.TenantId, g.GroupCode }).IsUnique();

        // Relationships
        builder.HasOne(g => g.DefaultPriceList)
            .WithMany()
            .HasForeignKey(g => g.DefaultPriceListId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupplierGroupConfiguration : IEntityTypeConfiguration<SupplierGroup>
{
    public void Configure(EntityTypeBuilder<SupplierGroup> builder)
    {
        builder.ToTable("SupplierGroups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.GroupCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(g => g.Description)
            .HasMaxLength(1000);

        builder.Property(g => g.DefaultPaymentTerms)
            .HasMaxLength(50);

        // Indexes
        builder.HasIndex(g => g.TenantId);
        builder.HasIndex(g => new { g.TenantId, g.GroupCode }).IsUnique();

        // Relationships
        builder.HasOne(g => g.DefaultPriceList)
            .WithMany()
            .HasForeignKey(g => g.DefaultPriceListId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PriceListChangeHistoryConfiguration : IEntityTypeConfiguration<PriceListChangeHistory>
{
    public void Configure(EntityTypeBuilder<PriceListChangeHistory> builder)
    {
        builder.ToTable("PriceListChangeHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.OldPrice)
            .HasPrecision(18, 4);

        builder.Property(h => h.NewPrice)
            .HasPrecision(18, 4);

        builder.Property(h => h.ChangePercent)
            .HasPrecision(5, 2);

        builder.Property(h => h.ChangeType)
            .HasMaxLength(50);

        builder.Property(h => h.ChangeReason)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(h => h.TenantId);
        builder.HasIndex(h => h.PriceListLineId);
        builder.HasIndex(h => h.InventoryItemId);
        builder.HasIndex(h => h.EffectiveDate);
    }
}

