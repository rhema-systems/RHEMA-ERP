using ErpSystem.Core.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Projects;

public sealed class ProjectTypeConfiguration : IEntityTypeConfiguration<ProjectType>
{
    public void Configure(EntityTypeBuilder<ProjectType> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Name });
    }
}

public sealed class ProjectPriorityConfiguration : IEntityTypeConfiguration<ProjectPriority>
{
    public void Configure(EntityTypeBuilder<ProjectPriority> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.SortOrder });
    }
}

public sealed class ProjectTemplateConfiguration : IEntityTypeConfiguration<ProjectTemplate>
{
    public void Configure(EntityTypeBuilder<ProjectTemplate> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ProjectTypeId, x.Name });
    }
}

public sealed class ProjectPortfolioConfiguration : IEntityTypeConfiguration<ProjectPortfolio>
{
    public void Configure(EntityTypeBuilder<ProjectPortfolio> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Name });
    }
}

public sealed class ProjectProgramConfiguration : IEntityTypeConfiguration<ProjectProgram>
{
    public void Configure(EntityTypeBuilder<ProjectProgram> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.PortfolioId, x.Name });

        builder.HasOne(x => x.Portfolio)
            .WithMany(x => x.Programs)
            .HasForeignKey(x => x.PortfolioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectManagementSettingsConfiguration : IEntityTypeConfiguration<ProjectManagementSettings>
{
    public void Configure(EntityTypeBuilder<ProjectManagementSettings> builder)
    {
        builder.HasIndex(x => x.TenantId).IsUnique();
    }
}

public sealed class ProjectCatalogEntryConfiguration : IEntityTypeConfiguration<ProjectCatalogEntry>
{
    public void Configure(EntityTypeBuilder<ProjectCatalogEntry> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.CatalogType, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CatalogType, x.SortOrder });
        builder.HasIndex(x => new { x.TenantId, x.CatalogType, x.IsActive });
    }
}

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.ProjectCode }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.ProjectTypeId });
        builder.HasIndex(x => new { x.TenantId, x.ExternalPortalAccessEnabled, x.BusinessPartnerId });

        builder.HasOne(x => x.ProjectType)
            .WithMany(x => x.Projects)
            .HasForeignKey(x => x.ProjectTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectPriority)
            .WithMany(x => x.Projects)
            .HasForeignKey(x => x.ProjectPriorityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Template)
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Portfolio)
            .WithMany(x => x.Projects)
            .HasForeignKey(x => x.PortfolioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Program)
            .WithMany(x => x.Projects)
            .HasForeignKey(x => x.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectInitiationVersionConfiguration : IEntityTypeConfiguration<ProjectInitiationVersion>
{
    public void Configure(EntityTypeBuilder<ProjectInitiationVersion> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.VersionNumber }).IsUnique();
    }
}

public sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.UserId, x.Role }).IsUnique();
    }
}

public sealed class ProjectWorkItemConfiguration : IEntityTypeConfiguration<ProjectWorkItem>
{
    public void Configure(EntityTypeBuilder<ProjectWorkItem> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.ParentId, x.SortOrder });

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectMilestoneConfiguration : IEntityTypeConfiguration<ProjectMilestone>
{
    public void Configure(EntityTypeBuilder<ProjectMilestone> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.TargetDate });
    }
}

public sealed class ProjectResourceAllocationConfiguration : IEntityTypeConfiguration<ProjectResourceAllocation>
{
    public void Configure(EntityTypeBuilder<ProjectResourceAllocation> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.UserId, x.StartDate, x.EndDate });
        builder.HasIndex(x => new { x.UserId, x.Status });

        builder.HasOne(x => x.WorkItem)
            .WithMany()
            .HasForeignKey(x => x.WorkItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectRiskConfiguration : IEntityTypeConfiguration<ProjectRisk>
{
    public void Configure(EntityTypeBuilder<ProjectRisk> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}

public sealed class ProjectIssueConfiguration : IEntityTypeConfiguration<ProjectIssue>
{
    public void Configure(EntityTypeBuilder<ProjectIssue> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}

public sealed class ProjectQualityCheckpointConfiguration : IEntityTypeConfiguration<ProjectQualityCheckpoint>
{
    public void Configure(EntityTypeBuilder<ProjectQualityCheckpoint> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.DueDate });

        builder.HasOne(x => x.WorkItem)
            .WithMany()
            .HasForeignKey(x => x.WorkItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Deliverable)
            .WithMany()
            .HasForeignKey(x => x.DeliverableId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectNonConformanceConfiguration : IEntityTypeConfiguration<ProjectNonConformance>
{
    public void Configure(EntityTypeBuilder<ProjectNonConformance> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.Severity });

        builder.HasOne(x => x.QualityCheckpoint)
            .WithMany()
            .HasForeignKey(x => x.QualityCheckpointId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Deliverable)
            .WithMany()
            .HasForeignKey(x => x.DeliverableId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectChangeRequestConfiguration : IEntityTypeConfiguration<ProjectChangeRequest>
{
    public void Configure(EntityTypeBuilder<ProjectChangeRequest> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}

public sealed class ProjectBillingScheduleConfiguration : IEntityTypeConfiguration<ProjectBillingSchedule>
{
    public void Configure(EntityTypeBuilder<ProjectBillingSchedule> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.BillingDate });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ContractId, x.ContractMilestoneId });

        builder.HasOne(x => x.Milestone)
            .WithMany()
            .HasForeignKey(x => x.MilestoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectInvoiceRequestConfiguration : IEntityTypeConfiguration<ProjectInvoiceRequest>
{
    public void Configure(EntityTypeBuilder<ProjectInvoiceRequest> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.RequestNumber }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Status });

        builder.HasOne(x => x.BillingSchedule)
            .WithMany()
            .HasForeignKey(x => x.BillingScheduleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectDocumentConfiguration : IEntityTypeConfiguration<ProjectDocument>
{
    public void Configure(EntityTypeBuilder<ProjectDocument> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Category });
        builder.HasIndex(x => new { x.ProjectId, x.IsExternalVisible });
    }
}

public sealed class ProjectCommentConfiguration : IEntityTypeConfiguration<ProjectComment>
{
    public void Configure(EntityTypeBuilder<ProjectComment> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.WorkItemId });
    }
}

public sealed class ProjectDeliverableConfiguration : IEntityTypeConfiguration<ProjectDeliverable>
{
    public void Configure(EntityTypeBuilder<ProjectDeliverable> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.IsExternalVisible, x.ExternalSubmissionAllowed });
    }
}

public sealed class ProjectTaskDependencyConfiguration : IEntityTypeConfiguration<ProjectTaskDependency>
{
    public void Configure(EntityTypeBuilder<ProjectTaskDependency> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.PredecessorWorkItemId, x.SuccessorWorkItemId }).IsUnique();
    }
}

public sealed class ProjectInterdependencyConfiguration : IEntityTypeConfiguration<ProjectInterdependency>
{
    public void Configure(EntityTypeBuilder<ProjectInterdependency> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.SourceProjectId, x.TargetProjectId, x.DependencyType }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.ImpactLevel });

        builder.HasOne(x => x.SourceProject)
            .WithMany()
            .HasForeignKey(x => x.SourceProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TargetProject)
            .WithMany()
            .HasForeignKey(x => x.TargetProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectBaselineConfiguration : IEntityTypeConfiguration<ProjectBaseline>
{
    public void Configure(EntityTypeBuilder<ProjectBaseline> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Name });
    }
}

public sealed class ProjectTimesheetEntryConfiguration : IEntityTypeConfiguration<ProjectTimesheetEntry>
{
    public void Configure(EntityTypeBuilder<ProjectTimesheetEntry> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.UserId, x.EntryDate });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}

public sealed class ProjectExpenseConfiguration : IEntityTypeConfiguration<ProjectExpense>
{
    public void Configure(EntityTypeBuilder<ProjectExpense> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.UserId, x.ExpenseDate });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}

public sealed class ProjectRevenueRecognitionConfiguration : IEntityTypeConfiguration<ProjectRevenueRecognition>
{
    public void Configure(EntityTypeBuilder<ProjectRevenueRecognition> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.RecognitionPeriod });
    }
}

public sealed class ProjectBudgetRevisionConfiguration : IEntityTypeConfiguration<ProjectBudgetRevision>
{
    public void Configure(EntityTypeBuilder<ProjectBudgetRevision> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.EffectiveDate });
    }
}

public sealed class ProjectForecastVersionConfiguration : IEntityTypeConfiguration<ProjectForecastVersion>
{
    public void Configure(EntityTypeBuilder<ProjectForecastVersion> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.IsActive });
        builder.HasIndex(x => new { x.ProjectId, x.AsOfDate });
    }
}

public sealed class ProjectAssetLinkConfiguration : IEntityTypeConfiguration<ProjectAssetLink>
{
    public void Configure(EntityTypeBuilder<ProjectAssetLink> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.LinkType, x.Status });
    }
}

public sealed class ProjectExternalAccessPolicyConfiguration : IEntityTypeConfiguration<ProjectExternalAccessPolicy>
{
    public void Configure(EntityTypeBuilder<ProjectExternalAccessPolicy> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.BusinessPartnerId, x.ArtifactType, x.ArtifactId }).IsUnique();
    }
}

public sealed class ProjectDecisionConfiguration : IEntityTypeConfiguration<ProjectDecision>
{
    public void Configure(EntityTypeBuilder<ProjectDecision> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.DecisionDate });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}

public sealed class ProjectMeetingMinuteConfiguration : IEntityTypeConfiguration<ProjectMeetingMinute>
{
    public void Configure(EntityTypeBuilder<ProjectMeetingMinute> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.MeetingDate });
    }
}

public sealed class ProjectActionItemConfiguration : IEntityTypeConfiguration<ProjectActionItem>
{
    public void Configure(EntityTypeBuilder<ProjectActionItem> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status, x.DueDate });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.ActionItems)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MeetingMinute)
            .WithMany(x => x.ActionItems)
            .HasForeignKey(x => x.MeetingMinuteId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.WorkItem)
            .WithMany()
            .HasForeignKey(x => x.WorkItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectLessonLearnedConfiguration : IEntityTypeConfiguration<ProjectLessonLearned>
{
    public void Configure(EntityTypeBuilder<ProjectLessonLearned> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Category });
    }
}

public sealed class ProjectClosureConfiguration : IEntityTypeConfiguration<ProjectClosure>
{
    public void Configure(EntityTypeBuilder<ProjectClosure> builder)
    {
        builder.HasIndex(x => x.ProjectId).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}
