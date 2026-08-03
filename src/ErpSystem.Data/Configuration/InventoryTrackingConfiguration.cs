using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryTrackingExceptionConfiguration : IEntityTypeConfiguration<InventoryTrackingException>
{
    public void Configure(EntityTypeBuilder<InventoryTrackingException> builder)
    {
        builder.ToTable("InventoryTrackingExceptions", table =>
        {
            table.HasCheckConstraint("CK_InventoryTrackingExceptions_IntegrityHash", "LEN([IntegrityHash]) = 64");
            table.HasCheckConstraint("CK_InventoryTrackingExceptions_Expiry", "[ExpiresAtUtc] > [ApprovedAtUtc]");
        });
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.Property(item => item.IntegrityHash).HasMaxLength(64).IsRequired();
        builder.Property(item => item.ReferenceType).HasMaxLength(100).IsRequired();
        builder.Property(item => item.ReferenceNumber).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(item => item.LotNumber).HasMaxLength(100);
        builder.Property(item => item.BatchNumber).HasMaxLength(100);
        builder.Property(item => item.SerialNumber).HasMaxLength(100);

        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ReferenceId, item.ReferenceLineId });
        builder.HasIndex(item => new { item.TenantId, item.InventoryItemId, item.WarehouseId, item.ExpiresAtUtc });

        builder.HasOne(item => item.InventoryItem).WithMany()
            .HasForeignKey(item => item.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Warehouse).WithMany()
            .HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Location).WithMany()
            .HasForeignKey(item => item.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowEvidenceDocument).WithMany()
            .HasForeignKey(item => item.WorkflowEvidenceDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryTraceabilityEventConfiguration : IEntityTypeConfiguration<InventoryTraceabilityEvent>
{
    public void Configure(EntityTypeBuilder<InventoryTraceabilityEvent> builder)
    {
        builder.ToTable("InventoryTraceabilityEvents", table =>
        {
            table.HasCheckConstraint("CK_InventoryTraceabilityEvents_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_InventoryTraceabilityEvents_PayloadHash", "LEN([PayloadHash]) = 64");
        });
        builder.Property(item => item.Quantity).HasPrecision(18, 4);
        builder.Property(item => item.ReferenceType).HasMaxLength(100).IsRequired();
        builder.Property(item => item.ReferenceNumber).HasMaxLength(100).IsRequired();
        builder.Property(item => item.EventKey).HasMaxLength(200).IsRequired();
        builder.Property(item => item.LotNumber).HasMaxLength(100);
        builder.Property(item => item.BatchNumber).HasMaxLength(100);
        builder.Property(item => item.SerialNumber).HasMaxLength(100);
        builder.Property(item => item.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(item => item.PayloadHash).HasMaxLength(64).IsRequired();

        builder.HasIndex(item => new { item.TenantId, item.EventKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.InventoryItemId, item.WarehouseId, item.OccurredAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.SerialNumber })
            .HasFilter("[SerialNumber] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.LotNumber, item.BatchNumber });

        builder.HasOne(item => item.InventoryItem).WithMany()
            .HasForeignKey(item => item.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Warehouse).WithMany()
            .HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Location).WithMany()
            .HasForeignKey(item => item.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.TrackingException).WithMany()
            .HasForeignKey(item => item.TrackingExceptionId).OnDelete(DeleteBehavior.Restrict);
    }
}
