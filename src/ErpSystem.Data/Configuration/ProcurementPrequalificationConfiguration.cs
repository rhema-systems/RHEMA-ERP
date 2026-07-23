using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementPrequalificationExerciseConfiguration : IEntityTypeConfiguration<ProcurementPrequalificationExercise>
{
    public void Configure(EntityTypeBuilder<ProcurementPrequalificationExercise> builder)
    {
        builder.ToTable("ProcurementPrequalificationExercises",
            table => table.HasTrigger("TR_ProcurementPrequalificationExercises_Lifecycle"));
        builder.HasIndex(item => new { item.TenantId, item.Reference }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.Status, item.ClosesAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.PolicySetId });
        builder.HasIndex(item => new { item.TenantId, item.WorkflowDefinitionId });
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });
        builder.HasOne(item => item.PolicySet).WithMany()
            .HasForeignKey(item => item.PolicySetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementPrequalificationCriterionConfiguration : IEntityTypeConfiguration<ProcurementPrequalificationCriterion>
{
    public void Configure(EntityTypeBuilder<ProcurementPrequalificationCriterion> builder)
    {
        builder.ToTable("ProcurementPrequalificationCriteria",
            table => table.HasTrigger("TR_ProcurementPrequalificationCriteria_ImmutableAfterAdvertisement"));
        builder.HasIndex(item => new { item.TenantId, item.ExerciseId, item.Code }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ExerciseId, item.SortOrder });
        builder.HasOne(item => item.Exercise).WithMany(item => item.Criteria)
            .HasForeignKey(item => item.ExerciseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementPrequalificationApplicationConfiguration : IEntityTypeConfiguration<ProcurementPrequalificationApplication>
{
    public void Configure(EntityTypeBuilder<ProcurementPrequalificationApplication> builder)
    {
        builder.ToTable("ProcurementPrequalificationApplications",
            table => table.HasTrigger("TR_ProcurementPrequalificationApplications_Lifecycle"));
        builder.HasIndex(item => new { item.TenantId, item.ApplicationNumber }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ExerciseId, item.BusinessPartnerId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ExerciseId, item.Status });
        builder.HasOne(item => item.Exercise).WithMany(item => item.Applications)
            .HasForeignKey(item => item.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementPrequalificationScoreConfiguration : IEntityTypeConfiguration<ProcurementPrequalificationScore>
{
    public void Configure(EntityTypeBuilder<ProcurementPrequalificationScore> builder)
    {
        builder.ToTable("ProcurementPrequalificationScores",
            table => table.HasTrigger("TR_ProcurementPrequalificationScores_Immutable"));
        builder.HasIndex(item => new { item.TenantId, item.ApplicationId, item.CriterionId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.EvaluatedById, item.EvaluatedAtUtc });
        builder.HasOne(item => item.Application).WithMany(item => item.Scores)
            .HasForeignKey(item => item.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Criterion).WithMany(item => item.Scores)
            .HasForeignKey(item => item.CriterionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementQualifiedListEntryConfiguration : IEntityTypeConfiguration<ProcurementQualifiedListEntry>
{
    public void Configure(EntityTypeBuilder<ProcurementQualifiedListEntry> builder)
    {
        builder.ToTable("ProcurementQualifiedListEntries",
            table => table.HasTrigger("TR_ProcurementQualifiedListEntries_Lifecycle"));
        builder.HasIndex(item => new { item.TenantId, item.ExerciseId, item.ApplicationId, item.CategoryId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId, item.CategoryId, item.Status, item.ExpiresAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.ExerciseId, item.Status });
        builder.HasOne(item => item.Exercise).WithMany(item => item.QualifiedEntries)
            .HasForeignKey(item => item.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Application).WithMany(item => item.QualifiedEntries)
            .HasForeignKey(item => item.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Category).WithMany()
            .HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
