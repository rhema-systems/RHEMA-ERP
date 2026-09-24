using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Configurations;

/// <summary>
/// Persistence contract for canonical Business Partner roles and governed Finance profiles.
/// These tables replace forward Finance Supplier/Customer identity; they are not a compatibility
/// bridge to the legacy masters.
/// </summary>
public static class BusinessPartnerFinanceProfileConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<BusinessPartnerRole>(entity =>
        {
            entity.ToTable("BusinessPartnerRoles", table => table.HasCheckConstraint(
                "CK_BusinessPartnerRoles_InactiveEvidence",
                "[Status] <> 2 OR ([InactiveFromUtc] IS NOT NULL AND NULLIF(LTRIM(RTRIM([StatusReason])), '') IS NOT NULL)"));
            entity.HasIndex(x => new { x.TenantId, x.BusinessPartnerId, x.RoleType }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.RoleType, x.Status });
            entity.HasOne(x => x.BusinessPartner)
                .WithMany(x => x.Roles)
                .HasForeignKey(x => x.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BusinessPartnerApProfileVersion>(entity =>
        {
            entity.ToTable("BusinessPartnerApProfileVersions", table => table.HasCheckConstraint(
                "CK_BusinessPartnerApProfiles_EffectivePeriod",
                "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]"));
            entity.HasIndex(x => new { x.TenantId, x.BusinessPartnerRoleId, x.VersionNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.BusinessPartnerRoleId, x.Status, x.EffectiveFrom });
            entity.HasOne(x => x.BusinessPartnerRole).WithMany()
                .HasForeignKey(x => x.BusinessPartnerRoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PaymentTerm).WithMany()
                .HasForeignKey(x => x.PaymentTermId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DefaultTaxGroup).WithMany()
                .HasForeignKey(x => x.DefaultTaxGroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DefaultExpenseAccount).WithMany()
                .HasForeignKey(x => x.DefaultExpenseAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<BusinessPartnerApWhtDefault>().WithMany()
                .HasForeignKey(x => x.DefaultWithholdingLineId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<BusinessPartnerApWhtDefault>(entity =>
        {
            entity.ToTable("BusinessPartnerApWhtDefaults");
            entity.HasIndex(x => new { x.TenantId, x.ApProfileVersionId, x.CategoryCode }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ApProfileVersionId, x.IsDefaultForAp }, "IX_BusinessPartnerApWhtDefaults_OneDefault")
                .IsUnique()
                .HasFilter("[IsDefaultForAp] = 1 AND [IsDeleted] = 0");
            entity.HasOne(x => x.ApProfileVersion).WithMany(x => x.WithholdingDefaults)
                .HasForeignKey(x => x.ApProfileVersionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WithholdingTax).WithMany()
                .HasForeignKey(x => x.WithholdingTaxId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BusinessPartnerArProfileVersion>(entity =>
        {
            entity.ToTable("BusinessPartnerArProfileVersions", table => table.HasCheckConstraint(
                "CK_BusinessPartnerArProfiles_EffectivePeriod",
                "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]"));
            entity.HasIndex(x => new { x.TenantId, x.BusinessPartnerRoleId, x.VersionNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.BusinessPartnerRoleId, x.Status, x.EffectiveFrom });
            entity.Property(x => x.CreditLimit).HasPrecision(18, 2);
            entity.HasOne(x => x.BusinessPartnerRole).WithMany()
                .HasForeignKey(x => x.BusinessPartnerRoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PaymentTerm).WithMany()
                .HasForeignKey(x => x.PaymentTermId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
