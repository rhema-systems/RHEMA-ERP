using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryLabelProfileConfiguration : IEntityTypeConfiguration<InventoryLabelProfile>
{
    public void Configure(EntityTypeBuilder<InventoryLabelProfile> builder)
    {
        builder.ToTable("InventoryLabelProfiles");
        builder.Property(item => item.Name).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(500);
        builder.Property(item => item.Symbology).HasMaxLength(20).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.Name })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(item => item.TenantId)
            .IsUnique().HasFilter("[IsDeleted] = 0 AND [IsDefault] = 1");
    }
}

public sealed class InventoryLabelPrintEventConfiguration : IEntityTypeConfiguration<InventoryLabelPrintEvent>
{
    public void Configure(EntityTypeBuilder<InventoryLabelPrintEvent> builder)
    {
        builder.ToTable("InventoryLabelPrintEvents");
        builder.Property(item => item.Identifier).HasMaxLength(200).IsRequired();
        builder.Property(item => item.IdentifierKind).HasMaxLength(40).IsRequired();
        builder.Property(item => item.LotNumber).HasMaxLength(100);
        builder.Property(item => item.SerialNumber).HasMaxLength(100);
        builder.Property(item => item.PrinterName).HasMaxLength(200);
        builder.Property(item => item.CorrelationId).HasMaxLength(100).IsRequired();
        builder.HasIndex(item => new { item.TenantId, item.PrintedAtUtc });
        builder.HasOne(item => item.LabelProfile).WithMany(item => item.PrintEvents)
            .HasForeignKey(item => item.LabelProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.InventoryItem).WithMany()
            .HasForeignKey(item => item.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.UnitOfMeasure).WithMany()
            .HasForeignKey(item => item.UnitOfMeasureId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryScanBatchConfiguration : IEntityTypeConfiguration<InventoryScanBatch>
{
    public void Configure(EntityTypeBuilder<InventoryScanBatch> builder)
    {
        builder.ToTable("InventoryScanBatches");
        builder.Property(item => item.DeviceId).HasMaxLength(100).IsRequired();
        builder.Property(item => item.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(item => item.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(item => item.DocumentReference).HasMaxLength(100).IsRequired();
        builder.Property(item => item.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(item => item.FailureCode).HasMaxLength(1000);
        builder.Property(item => item.FailureMessage).HasMaxLength(2000);
        builder.HasIndex(item => new { item.TenantId, item.DeviceId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.DocumentId, item.Operation });
        builder.HasIndex(item => new { item.TenantId, item.CapturedAtUtc });
        builder.HasOne(item => item.Warehouse).WithMany()
            .HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Lines).WithOne(item => item.ScanBatch)
            .HasForeignKey(item => item.ScanBatchId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class InventoryScanLineConfiguration : IEntityTypeConfiguration<InventoryScanLine>
{
    public void Configure(EntityTypeBuilder<InventoryScanLine> builder)
    {
        builder.ToTable("InventoryScanLines");
        builder.Property(item => item.RawIdentifier).HasMaxLength(200).IsRequired();
        builder.Property(item => item.IdentifierKind).HasMaxLength(40).IsRequired();
        builder.Property(item => item.LocationIdentifier).HasMaxLength(100);
        builder.Property(item => item.LotNumber).HasMaxLength(100);
        builder.Property(item => item.BatchNumber).HasMaxLength(100);
        builder.Property(item => item.SerialNumber).HasMaxLength(100);
        builder.HasIndex(item => new { item.ScanBatchId, item.ClientLineId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.InventoryItemId, item.ScannedAtUtc });
        builder.HasOne(item => item.InventoryItem).WithMany()
            .HasForeignKey(item => item.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.UnitOfMeasure).WithMany()
            .HasForeignKey(item => item.UnitOfMeasureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Location).WithMany()
            .HasForeignKey(item => item.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
