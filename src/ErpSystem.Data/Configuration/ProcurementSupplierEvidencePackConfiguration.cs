using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementSupplierEvidencePackVersionConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierEvidencePackVersion>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierEvidencePackVersion> builder)
    {
        builder.ToTable("ProcurementSupplierEvidencePackVersions", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierEvidencePackVersions_Lifecycle");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierEvidencePackVersions_State",
                "[Category] BETWEEN 0 AND 2 AND [Version] >= 1 AND [Status] BETWEEN 0 AND 3 " +
                "AND [SourceConfigurationProfileVersion] >= 1 " +
                "AND ([EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]) " +
                "AND LEN([CreationCorrelationId]) > 0 AND LEN([LastOperationCorrelationId]) > 0 " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([LifecycleSnapshotJson]) = 1");
        });

        builder.HasIndex(item => new { item.TenantId, item.PackKey, item.Version }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PackCode, item.Version }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.CreationCorrelationId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PackKey })
            .IsUnique().HasFilter("[Status] IN (0, 1) AND [IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.Category, item.Status, item.EffectiveFromUtc });
        builder.HasIndex(item => new { item.TenantId, item.SourceConfigurationProfileId });
        builder.HasIndex(item => new { item.TenantId, item.WorkflowDefinitionId });

        builder.HasOne(item => item.SourceConfigurationProfile).WithMany()
            .HasForeignKey(item => item.SourceConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SupersedesVersion).WithMany()
            .HasForeignKey(item => item.SupersedesVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSupplierEvidenceRequirementConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierEvidenceRequirement>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierEvidenceRequirement> builder)
    {
        builder.ToTable("ProcurementSupplierEvidenceRequirements", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierEvidenceRequirements_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierEvidenceRequirements_State",
                "[Kind] BETWEEN 0 AND 2 AND [ValidityMode] BETWEEN 0 AND 2 " +
                "AND [ApprovalStepOrder] >= 1 AND [MaxFileSizeBytes] > 0 " +
                "AND ([AllowedClassificationsJson] IS NULL OR ISJSON([AllowedClassificationsJson]) = 1) " +
                "AND ISJSON([AllowedMimeTypesJson]) = 1 AND LEN([IntegrityHash]) = 64 " +
                "AND (([ValidityMode] = 2 AND [MinimumRemainingDays] > 0) OR " +
                "([ValidityMode] <> 2 AND [MinimumRemainingDays] IS NULL))");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierEvidenceRequirements_Kind",
                "([Kind] = 0 AND [DocumentType] IS NOT NULL) OR " +
                "([Kind] = 1 AND [ClassificationScheme] IS NOT NULL AND [AllowedClassificationsJson] IS NOT NULL) OR " +
                "([Kind] = 2 AND [DocumentType] IS NOT NULL AND [ClassificationScheme] IS NOT NULL " +
                "AND [AllowedClassificationsJson] IS NOT NULL)");
        });

        builder.HasIndex(item => new { item.TenantId, item.PackVersionId, item.RequirementCode }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PackVersionId, item.ApprovalStepOrder });
        builder.HasOne(item => item.PackVersion).WithMany(item => item.Requirements)
            .HasForeignKey(item => item.PackVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSupplierRegistrationEvidencePackBindingConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierRegistrationEvidencePackBinding>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierRegistrationEvidencePackBinding> builder)
    {
        builder.ToTable("ProcurementSupplierRegistrationEvidencePackBindings", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierRegistrationEvidencePackBindings_Immutable");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierRegistrationEvidencePackBindings_State",
                "[RegistrationCategory] BETWEEN 0 AND 2 AND [PackVersion] >= 1 " +
                "AND LEN([PackSnapshotHash]) = 64 AND LEN([IntegrityHash]) = 64 " +
                "AND ISJSON([PackSnapshotJson]) = 1");
        });

        builder.HasIndex(item => new { item.TenantId, item.RegistrationId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PackVersionId });
        builder.HasOne(item => item.Registration).WithOne(item => item.EvidencePackBinding)
            .HasForeignKey<ProcurementSupplierRegistrationEvidencePackBinding>(item => item.RegistrationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PackVersionRecord).WithMany()
            .HasForeignKey(item => item.PackVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
