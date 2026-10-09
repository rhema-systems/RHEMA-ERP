using ErpSystem.Core.Entities.MobilePos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class MobilePosStoreConfiguration : IEntityTypeConfiguration<MobilePosStore>
{
    public void Configure(EntityTypeBuilder<MobilePosStore> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.Code }).IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.Status });
        entity.HasIndex(item => new { item.TenantId, item.DefaultWalkInBusinessPartnerId });
        entity.HasOne(item => item.CompanyProfile).WithMany().HasForeignKey(item => item.CompanyProfileId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.Location).WithMany().HasForeignKey(item => item.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.Warehouse).WithMany().HasForeignKey(item => item.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.DefaultWalkInBusinessPartner).WithMany()
            .HasForeignKey(item => item.DefaultWalkInBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.DefaultWalkInBusinessPartnerRole).WithMany()
            .HasForeignKey(item => item.DefaultWalkInBusinessPartnerRoleId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.OfflinePolicy).WithMany().HasForeignKey(item => item.OfflinePolicyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MobilePosStoreDimensionDefaultConfiguration : IEntityTypeConfiguration<MobilePosStoreDimensionDefault>
{
    public void Configure(EntityTypeBuilder<MobilePosStoreDimensionDefault> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosStoreId, item.FinanceDimensionDefinitionId })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasOne(item => item.MobilePosStore).WithMany(item => item.DimensionDefaults)
            .HasForeignKey(item => item.MobilePosStoreId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.FinanceDimensionDefinition).WithMany()
            .HasForeignKey(item => item.FinanceDimensionDefinitionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.FinanceDimensionValue).WithMany()
            .HasForeignKey(item => item.FinanceDimensionValueId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MobilePosTillConfiguration : IEntityTypeConfiguration<MobilePosTill>
{
    public void Configure(EntityTypeBuilder<MobilePosTill> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.TillNumber }).IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.LiquidityAccountId }).IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [Status] <> 4");
        entity.HasOne(item => item.MobilePosStore).WithMany(item => item.Tills)
            .HasForeignKey(item => item.MobilePosStoreId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.LiquidityAccount).WithMany()
            .HasForeignKey(item => item.LiquidityAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MobilePosTillPaymentMethodConfiguration : IEntityTypeConfiguration<MobilePosTillPaymentMethod>
{
    public void Configure(EntityTypeBuilder<MobilePosTillPaymentMethod> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosTillId, item.PaymentMethodId })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        entity.HasOne(item => item.MobilePosTill).WithMany(item => item.PaymentMethods)
            .HasForeignKey(item => item.MobilePosTillId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.PaymentMethod).WithMany()
            .HasForeignKey(item => item.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MobilePosUserStoreAssignmentConfiguration : IEntityTypeConfiguration<MobilePosUserStoreAssignment>
{
    public void Configure(EntityTypeBuilder<MobilePosUserStoreAssignment> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.UserId }).IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
        entity.HasIndex(item => new { item.TenantId, item.MobilePosStoreId, item.IsActive });
        entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosStore).WithMany().HasForeignKey(item => item.MobilePosStoreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MobilePosOfflinePolicyConfiguration : IEntityTypeConfiguration<MobilePosOfflinePolicy>
{
    public void Configure(EntityTypeBuilder<MobilePosOfflinePolicy> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.Name }).IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.IsActive });
    }
}

public sealed class MobilePosDeviceConfiguration : IEntityTypeConfiguration<MobilePosDevice>
{
    public void Configure(EntityTypeBuilder<MobilePosDevice> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.InstallationIdHash }).IsUnique()
            .HasFilter("[IsDeleted] = 0");
        entity.HasIndex(item => new { item.TenantId, item.Status, item.LastSeenAtUtc });
        entity.HasIndex(item => new { item.TenantId, item.MobilePosTillId }).IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [MobilePosTillId] IS NOT NULL AND [Status] = 2");
        entity.HasOne(item => item.RequestedByUser).WithMany().HasForeignKey(item => item.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.ApprovedByUser).WithMany().HasForeignKey(item => item.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.RevokedByUser).WithMany().HasForeignKey(item => item.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosStore).WithMany().HasForeignKey(item => item.MobilePosStoreId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosTill).WithMany().HasForeignKey(item => item.MobilePosTillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MobilePosDeviceAssignmentHistoryConfiguration : IEntityTypeConfiguration<MobilePosDeviceAssignmentHistory>
{
    public void Configure(EntityTypeBuilder<MobilePosDeviceAssignmentHistory> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosDeviceId, item.ChangedAtUtc });
        entity.HasOne(item => item.MobilePosDevice).WithMany().HasForeignKey(item => item.MobilePosDeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MobilePosOfflineGrantConfiguration : IEntityTypeConfiguration<MobilePosOfflineGrant>
{
    public void Configure(EntityTypeBuilder<MobilePosOfflineGrant> entity)
    {
        entity.HasIndex(item => new { item.TenantId, item.MobilePosDeviceId, item.Status, item.ExpiresAtUtc });
        entity.HasIndex(item => new { item.TenantId, item.CashierTillSessionId, item.Status });
        entity.HasOne(item => item.MobilePosDevice).WithMany().HasForeignKey(item => item.MobilePosDeviceId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.MobilePosOfflinePolicy).WithMany()
            .HasForeignKey(item => item.MobilePosOfflinePolicyId).OnDelete(DeleteBehavior.Restrict);
    }
}
