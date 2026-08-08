using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryDisposalCaseConfiguration : IEntityTypeConfiguration<InventoryDisposalCase>
{
    public void Configure(EntityTypeBuilder<InventoryDisposalCase> builder)
    {
        builder.ToTable("InventoryDisposalCases", table =>
        {
            table.HasCheckConstraint("CK_InventoryDisposalCases_Amounts", "[TotalQuantity] > 0 AND [TotalValue] > 0 AND [ProceedsAmount] >= 0");
            table.HasCheckConstraint("CK_InventoryDisposalCases_Status", "[Status] BETWEEN 1 AND 10");
            table.HasCheckConstraint("CK_InventoryDisposalCases_Method", "[Method] BETWEEN 1 AND 5");
            table.HasTrigger("TR_InventoryDisposalCases_Guard");
        });
        builder.HasIndex(value => new { value.TenantId, value.DisposalNumber }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.IdempotencyKey }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.Status, value.RequestedAtUtc });
        builder.HasIndex(value => value.StockAdjustmentId).IsUnique().HasFilter("[StockAdjustmentId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasOne(value => value.Warehouse).WithMany().HasForeignKey(value => value.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.RequestedBy).WithMany().HasForeignKey(value => value.RequestedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.StockAdjustment).WithMany().HasForeignKey(value => value.StockAdjustmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryDisposalLineConfiguration : IEntityTypeConfiguration<InventoryDisposalLine>
{
    public void Configure(EntityTypeBuilder<InventoryDisposalLine> builder)
    {
        builder.ToTable("InventoryDisposalLines", table =>
        {
            table.HasCheckConstraint("CK_InventoryDisposalLines_Amounts", "[Quantity] > 0 AND [UnitCost] > 0 AND [TotalValue] > 0");
            table.HasTrigger("TR_InventoryDisposalLines_Immutable");
        });
        builder.HasIndex(value => new { value.InventoryDisposalCaseId, value.InventoryItemId, value.LocationId });
        builder.HasOne(value => value.InventoryDisposalCase).WithMany(value => value.Lines).HasForeignKey(value => value.InventoryDisposalCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.InventoryItem).WithMany().HasForeignKey(value => value.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.Location).WithMany().HasForeignKey(value => value.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryDisposalEvidenceConfiguration : IEntityTypeConfiguration<InventoryDisposalEvidence>
{
    public void Configure(EntityTypeBuilder<InventoryDisposalEvidence> builder)
    {
        builder.ToTable("InventoryDisposalEvidence", table => table.HasTrigger("TR_InventoryDisposalEvidence_AppendOnly"));
        builder.HasIndex(value => new { value.InventoryDisposalCaseId, value.CentralDocumentVersionId }).IsUnique();
        builder.HasOne(value => value.InventoryDisposalCase).WithMany(value => value.Evidence).HasForeignKey(value => value.InventoryDisposalCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.CentralDocumentVersion).WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.FileUploadRecord).WithMany().HasForeignKey(value => value.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryDisposalCommitteeMemberConfiguration : IEntityTypeConfiguration<InventoryDisposalCommitteeMember>
{
    public void Configure(EntityTypeBuilder<InventoryDisposalCommitteeMember> builder)
    {
        builder.ToTable("InventoryDisposalCommitteeMembers", table => table.HasTrigger("TR_InventoryDisposalCommitteeMembers_Guard"));
        builder.HasIndex(value => new { value.InventoryDisposalCaseId, value.MemberUserId }).IsUnique();
        builder.HasOne(value => value.InventoryDisposalCase).WithMany(value => value.CommitteeMembers).HasForeignKey(value => value.InventoryDisposalCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.MemberUser).WithMany().HasForeignKey(value => value.MemberUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryDisposalActionConfiguration : IEntityTypeConfiguration<InventoryDisposalAction>
{
    public void Configure(EntityTypeBuilder<InventoryDisposalAction> builder)
    {
        builder.ToTable("InventoryDisposalActions", table =>
        {
            table.HasCheckConstraint("CK_InventoryDisposalActions_Sequence", "[Sequence] > 0");
            table.HasTrigger("TR_InventoryDisposalActions_AppendOnly");
        });
        builder.HasIndex(value => new { value.InventoryDisposalCaseId, value.Sequence }).IsUnique();
        builder.HasIndex(value => new { value.InventoryDisposalCaseId, value.IdempotencyKey }).IsUnique();
        builder.HasOne(value => value.InventoryDisposalCase).WithMany(value => value.Actions).HasForeignKey(value => value.InventoryDisposalCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.ActorUser).WithMany().HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
