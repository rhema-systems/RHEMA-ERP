using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data;

public partial class ApplicationDbContext
{
    public DbSet<CivilEngineeringConfigurationProfile> CivilEngineeringConfigurationProfiles { get; set; }
    public DbSet<CivilEngineeringConfigurationDecision> CivilEngineeringConfigurationDecisions { get; set; }
    public DbSet<CivilEngineeringConfigurationEvidenceLink> CivilEngineeringConfigurationEvidenceLinks { get; set; }
    public DbSet<CivilEngineeringConfigurationRevision> CivilEngineeringConfigurationRevisions { get; set; }
    public DbSet<ProjectCivilDesignCase> ProjectCivilDesignCases { get; set; }
    public DbSet<ProjectCivilDesignEvidence> ProjectCivilDesignEvidence { get; set; }
    public DbSet<ProjectCivilDesignRevision> ProjectCivilDesignRevisions { get; set; }
    public DbSet<ProjectCivilPlanningGisValidation> ProjectCivilPlanningGisValidations { get; set; }
    public DbSet<ProjectCivilPlanningGisValidationRevision> ProjectCivilPlanningGisValidationRevisions { get; set; }
    public DbSet<ProjectCivilReconnaissanceReport> ProjectCivilReconnaissanceReports { get; set; }
    public DbSet<ProjectCivilReconnaissanceItem> ProjectCivilReconnaissanceItems { get; set; }
    public DbSet<ProjectCivilReconnaissanceRevision> ProjectCivilReconnaissanceRevisions { get; set; }
    public DbSet<ProjectCivilDesignInputResponse> ProjectCivilDesignInputResponses { get; set; }
    public DbSet<ProjectCivilEngineeringDocument> ProjectCivilEngineeringDocuments { get; set; }
    public DbSet<ProjectCivilEngineeringDocumentRevision> ProjectCivilEngineeringDocumentRevisions { get; set; }
    public DbSet<ProjectCivilProjectEngineerAssignment> ProjectCivilProjectEngineerAssignments { get; set; }
    public DbSet<ProjectCivilProjectEngineerAssignmentRevision> ProjectCivilProjectEngineerAssignmentRevisions { get; set; }
    public DbSet<ProjectCivilSiteInstructionRouting> ProjectCivilSiteInstructionRoutings { get; set; }
    public DbSet<ProjectCivilSiteInstructionEvidence> ProjectCivilSiteInstructionEvidence { get; set; }
    public DbSet<ProjectCivilSiteInstructionResponse> ProjectCivilSiteInstructionResponses { get; set; }
    public DbSet<ProjectCivilSiteInstructionRevision> ProjectCivilSiteInstructionRevisions { get; set; }
    public DbSet<ProjectCivilRfiRouting> ProjectCivilRfiRoutings { get; set; }
    public DbSet<ProjectCivilRfiEvidence> ProjectCivilRfiEvidence { get; set; }
    public DbSet<ProjectCivilRfiResponse> ProjectCivilRfiResponses { get; set; }
    public DbSet<ProjectCivilRfiRevision> ProjectCivilRfiRevisions { get; set; }
    public DbSet<ProjectCivilQualityTestReport> ProjectCivilQualityTestReports { get; set; }
    public DbSet<ProjectCivilQualityTestRevision> ProjectCivilQualityTestRevisions { get; set; }
    public DbSet<ProjectCivilInspectionControl> ProjectCivilInspectionControls { get; set; }
    public DbSet<ProjectCivilInspectionRevision> ProjectCivilInspectionRevisions { get; set; }
    public DbSet<ProjectCivilIpcEndorsement> ProjectCivilIpcEndorsements { get; set; }
    public DbSet<ProjectCivilIpcEndorsementRevision> ProjectCivilIpcEndorsementRevisions { get; set; }
    public DbSet<ProjectCivilWeeklySupervisionReport> ProjectCivilWeeklySupervisionReports { get; set; }
    public DbSet<ProjectCivilWeeklySupervisionActivity> ProjectCivilWeeklySupervisionActivities { get; set; }
    public DbSet<ProjectCivilWeeklySupervisionEvidence> ProjectCivilWeeklySupervisionEvidence { get; set; }
    public DbSet<ProjectCivilWeeklySupervisionReview> ProjectCivilWeeklySupervisionReviews { get; set; }
    public DbSet<ProjectCivilWeeklySupervisionRevision> ProjectCivilWeeklySupervisionRevisions { get; set; }
    public DbSet<ProjectCivilExtensionOfTimeControl> ProjectCivilExtensionOfTimeControls { get; set; }
    public DbSet<ProjectCivilExtensionOfTimeRevision> ProjectCivilExtensionOfTimeRevisions { get; set; }
    public DbSet<CivilEngineeringMaintenanceIntake> CivilEngineeringMaintenanceIntakes { get; set; }
    public DbSet<CivilEngineeringMaintenanceIntakeRevision> CivilEngineeringMaintenanceIntakeRevisions { get; set; }
    public DbSet<CivilEngineeringMaintenanceAssessment> CivilEngineeringMaintenanceAssessments { get; set; }
    public DbSet<CivilEngineeringMaintenanceAssessmentRevision> CivilEngineeringMaintenanceAssessmentRevisions { get; set; }
    public DbSet<CivilEngineeringMaintenanceCostingHandoff> CivilEngineeringMaintenanceCostingHandoffs { get; set; }
    public DbSet<CivilEngineeringMaintenanceCostingHandoffRevision> CivilEngineeringMaintenanceCostingHandoffRevisions { get; set; }
    public DbSet<CivilEngineeringMaintenanceExecutionLink> CivilEngineeringMaintenanceExecutionLinks { get; set; }
    public DbSet<CivilEngineeringMaintenanceExecutionLinkRevision> CivilEngineeringMaintenanceExecutionLinkRevisions { get; set; }
    public DbSet<CivilEngineeringMaintenanceCompletionControl> CivilEngineeringMaintenanceCompletionControls { get; set; }
    public DbSet<CivilEngineeringMaintenanceCompletionRevision> CivilEngineeringMaintenanceCompletionRevisions { get; set; }
    public DbSet<ProjectCivilDevelopmentApprovalFile> ProjectCivilDevelopmentApprovalFiles { get; set; }
    public DbSet<ProjectCivilDevelopmentApprovalEvidence> ProjectCivilDevelopmentApprovalEvidence { get; set; }
    public DbSet<ProjectCivilDevelopmentApprovalFileRevision> ProjectCivilDevelopmentApprovalFileRevisions { get; set; }
    public DbSet<ProjectCivilDevelopmentApprovalFileHandoff> ProjectCivilDevelopmentApprovalFileHandoffs { get; set; }
    public DbSet<ProjectCivilDevelopmentApprovalHandoffEvidence> ProjectCivilDevelopmentApprovalHandoffEvidence { get; set; }
    public DbSet<ProjectCivilDevelopmentApprovalEngineeringReview> ProjectCivilDevelopmentApprovalEngineeringReviews { get; set; }
    public DbSet<ProjectCivilDevelopmentApprovalEngineeringReviewDecision> ProjectCivilDevelopmentApprovalEngineeringReviewDecisions { get; set; }
    public DbSet<ProjectCivilDevelopmentApprovalEngineeringReviewRevision> ProjectCivilDevelopmentApprovalEngineeringReviewRevisions { get; set; }
    public DbSet<ProjectCivilDirectTaskControl> ProjectCivilDirectTaskControls { get; set; }
    public DbSet<ProjectCivilDirectTaskRevision> ProjectCivilDirectTaskRevisions { get; set; }
    public DbSet<ProjectCivilDirectTaskFeedbackEntry> ProjectCivilDirectTaskFeedbackEntries { get; set; }
    public DbSet<ProjectCivilMigrationBatch> ProjectCivilMigrationBatches { get; set; }
    public DbSet<ProjectCivilMigrationRecord> ProjectCivilMigrationRecords { get; set; }
    public DbSet<ProjectCivilMigrationValidationIssue> ProjectCivilMigrationValidationIssues { get; set; }
    public DbSet<ProjectCivilMigrationRevision> ProjectCivilMigrationRevisions { get; set; }

    private static void ConfigureCivilEngineeringConfiguration(ModelBuilder builder)
    {
        builder.Entity<CivilEngineeringConfigurationProfile>(entity =>
        {
            entity.Property(item => item.ProfileCode).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ProfileKey, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProfileKey, item.LifecycleStatus });
            entity.HasIndex(item => new { item.TenantId, item.ProfileCode, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.LifecycleStatus, item.EffectiveFrom });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_CivilConfigurationProfiles_Version", "[Version] > 0");
                table.HasCheckConstraint("CK_CivilConfigurationProfiles_Period", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.HasCheckConstraint("CK_CivilConfigurationProfiles_Lifecycle", "[LifecycleStatus] IN (0, 1, 2)");
            });
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CivilEngineeringConfigurationProfile>().WithMany().HasForeignKey(item => item.SupersedesProfileId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CivilEngineeringConfigurationDecision>(entity =>
        {
            entity.Property(item => item.ConfigurationKey).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ProfileId, item.ConfigurationKey }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ConfigurationKey, item.Status });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_CivilConfigurationDecisions_Key", "[ConfigurationKey] LIKE 'CIV-CFG-[0-9][0-9][0-9]'");
                table.HasCheckConstraint("CK_CivilConfigurationDecisions_SchemaVersion", "[SchemaVersion] > 0");
                table.HasCheckConstraint("CK_CivilConfigurationDecisions_Period", "[EffectiveTo] IS NULL OR [EffectiveFrom] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.HasCheckConstraint("CK_CivilConfigurationDecisions_Status", "[Status] IN (0, 1, 2, 3)");
            });
            entity.HasOne(item => item.Profile).WithMany(item => item.Decisions).HasForeignKey(item => item.ProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CivilEngineeringConfigurationDecision>().WithMany().HasForeignKey(item => item.SourceDecisionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CivilEngineeringConfigurationEvidenceLink>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.DecisionId });
            entity.HasIndex(item => new { item.TenantId, item.DecisionId, item.CentralDocumentVersionId })
                .IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasOne(item => item.Profile).WithMany(item => item.EvidenceLinks).HasForeignKey(item => item.ProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Decision).WithMany(item => item.EvidenceLinks).HasForeignKey(item => item.DecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CivilEngineeringConfigurationRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.ProfileId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.HasOne<CivilEngineeringConfigurationProfile>().WithMany().HasForeignKey(item => item.ProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CivilEngineeringConfigurationDecision>().WithMany().HasForeignKey(item => item.DecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDesignCase>(entity =>
        {
            entity.Property(item => item.ReferenceNumber).IsUnicode(false);
            entity.Property(item => item.InitiationSourceReference).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.ReferenceNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.InitiationSource, item.InitiationSourceId }).IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [InitiationSource] IS NOT NULL AND [InitiationSourceId] IS NOT NULL AND [Status] <> 'Approved' AND [Status] <> 'Rejected' AND [Status] <> 'Cancelled'");
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.Stage });
            entity.HasIndex(item => new { item.TenantId, item.CurrentAssigneeUserId, item.Stage });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDesignCases_Lifecycle");
                table.HasCheckConstraint(
                    "CK_ProjectCivilDesignCases_Stage",
                    "[Stage] IN ('DraftDirective','SceInformationGathering','CivilEngineerDesign','SceDesignReview','Drafting','SceDrawingReview','HodFinalReview','Approved','Rejected','Cancelled')");
                table.HasCheckConstraint(
                    "CK_ProjectCivilDesignCases_Assignment",
                    "[HodUserId] <> [SupervisingCivilEngineerUserId] AND ([CivilEngineerUserId] IS NULL OR ([CivilEngineerUserId] <> [HodUserId] AND [CivilEngineerUserId] <> [SupervisingCivilEngineerUserId])) AND ([DraftsmanUserId] IS NULL OR ([DraftsmanUserId] <> [HodUserId] AND [DraftsmanUserId] <> [SupervisingCivilEngineerUserId] AND ([CivilEngineerUserId] IS NULL OR [DraftsmanUserId] <> [CivilEngineerUserId])))");
            });
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.EstateManagedAsset).WithMany().HasForeignKey(item => item.EstateManagedAssetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.EngineeringCategory).WithMany().HasForeignKey(item => item.EngineeringCategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationProfile).WithMany().HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationDecision).WithMany().HasForeignKey(item => item.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDesignEvidence>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDesignEvidence_Lineage");
                table.HasTrigger("TR_ProjectCivilDesignEvidence_AppendOnly");
            });
            entity.HasIndex(item => new { item.TenantId, item.DesignCaseId, item.CentralDocumentVersionId, item.EvidenceType })
                .IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasOne(item => item.DesignCase).WithMany(item => item.Evidence).HasForeignKey(item => item.DesignCaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilPlanningGisValidation>(entity =>
        {
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.SpatialReferenceSnapshot).IsUnicode(false);
            entity.Property(item => item.GisProviderSnapshot).IsUnicode(false);
            entity.Property(item => item.GisFeatureIdSnapshot).IsUnicode(false);
            entity.Property(item => item.GisSourceCrsSnapshot).IsUnicode(false);
            entity.Property(item => item.ZoningClassificationSnapshot).IsUnicode(false);
            entity.Property(item => item.PlanningComplianceSnapshot).IsUnicode(false);
            entity.Property(item => item.CorrelationId).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.DesignCaseId })
                .IsUnique().HasFilter("[IsDeleted] = 0 AND [Status] IN (0,1)");
            entity.HasIndex(item => new { item.TenantId, item.DevelopmentApprovalFileId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilPlanningGisValidations_Lineage");
                table.HasTrigger("TR_ProjectCivilPlanningGisValidations_Lifecycle");
                table.HasTrigger("TR_ProjectCivilPlanningGisValidations_EvidenceBinding");
                table.HasCheckConstraint("CK_ProjectCivilPlanningGisValidations_Status", "[Status] IN (0,1,2,3)");
                table.HasCheckConstraint("CK_ProjectCivilPlanningGisValidations_Layout", "[LayoutConformity] IN (0,1,2)");
            });
            entity.HasOne(item => item.DesignCase).WithMany(item => item.PlanningGisValidations).HasForeignKey(item => item.DesignCaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.EstateManagedAsset).WithMany().HasForeignKey(item => item.EstateManagedAssetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.DevelopmentApprovalFile).WithMany().HasForeignKey(item => item.DevelopmentApprovalFileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PlanningCondition).WithMany().HasForeignKey(item => item.PlanningConditionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.DevelopmentConstraint).WithMany().HasForeignKey(item => item.DevelopmentConstraintId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LandUseImpact).WithMany().HasForeignKey(item => item.LandUseImpactId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilPlanningGisValidationRevision>(entity =>
        {
            entity.Property(item => item.CorrelationId).IsUnicode(false);
            entity.HasIndex(item => new { item.TenantId, item.PlanningGisValidationId, item.CreatedAt });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilPlanningGisValidationRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilPlanningGisValidationRevisions_AppendOnly");
            });
            entity.HasOne(item => item.PlanningGisValidation).WithMany(item => item.Revisions).HasForeignKey(item => item.PlanningGisValidationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDesignRevision>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDesignRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilDesignRevisions_AppendOnly");
            });
            entity.HasIndex(item => new { item.TenantId, item.DesignCaseId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.HasOne(item => item.DesignCase).WithMany(item => item.Revisions).HasForeignKey(item => item.DesignCaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilReconnaissanceReport>(entity =>
        {
            entity.Property(item => item.ReportNumber).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.DesignCaseId, item.ReportNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.DesignCaseId, item.Status, item.VisitDate });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilReconnaissanceReports_Lifecycle");
                table.HasCheckConstraint(
                    "CK_ProjectCivilReconnaissanceReports_Status",
                    "[Status] IN ('Draft','Completed')");
            });
            entity.HasOne(item => item.DesignCase).WithMany(item => item.ReconnaissanceReports).HasForeignKey(item => item.DesignCaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SiteReconnaissanceTemplate).WithMany().HasForeignKey(item => item.SiteReconnaissanceTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CrossSectionTemplate).WithMany().HasForeignKey(item => item.CrossSectionTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilReconnaissanceItem>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.ReportId, item.DisplayOrder });
            entity.HasIndex(item => new { item.TenantId, item.CentralDocumentVersionId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilReconnaissanceItems_Lineage");
                table.HasCheckConstraint("CK_ProjectCivilReconnaissanceItems_Order", "[DisplayOrder] >= 0");
                table.HasCheckConstraint(
                    "CK_ProjectCivilReconnaissanceItems_Shape",
                    "([Kind] = 0 AND [InformationSourceSectionId] IS NULL AND [CentralDocumentRecordId] IS NULL AND [CentralDocumentVersionId] IS NULL AND [ConstraintCategory] IS NOT NULL AND [Severity] IS NOT NULL AND [ResolutionStatus] IS NOT NULL) OR ([Kind] = 1 AND [InformationSourceSectionId] IS NOT NULL AND [CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL AND [ConstraintCategory] IS NULL AND [Severity] IS NULL AND [ResolutionStatus] IS NULL AND [BlocksDesign] = 0) OR ([Kind] = 2 AND [InformationSourceSectionId] IS NULL AND [CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL AND [ConstraintCategory] IS NULL AND [Severity] IS NULL AND [ResolutionStatus] IS NULL AND [BlocksDesign] = 0)");
            });
            entity.HasOne(item => item.Report).WithMany(item => item.Items).HasForeignKey(item => item.ReportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.InformationSourceSection).WithMany().HasForeignKey(item => item.InformationSourceSectionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilReconnaissanceRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.ReportId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilReconnaissanceRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilReconnaissanceRevisions_AppendOnly");
            });
            entity.HasOne(item => item.Report).WithMany(item => item.Revisions).HasForeignKey(item => item.ReportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectRfi>(entity =>
        {
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique()
                .HasFilter("[CivilDesignCaseId] IS NOT NULL AND [ClientRequestId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.CivilDesignCaseId, item.Status, item.ResponseDueDate });
            entity.HasIndex(item => new { item.TenantId, item.RequestedSectionId, item.Status });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectRfis_CivilDesignInputLifecycle");
                table.HasCheckConstraint(
                    "CK_ProjectRfis_CivilDesignInputShape",
                    "([CivilDesignCaseId] IS NULL AND [RequestedSectionId] IS NULL AND [RequestedByUserId] IS NULL AND [ClientRequestId] IS NULL AND [RequestHash] IS NULL) OR ([CivilDesignCaseId] IS NOT NULL AND [RequestedSectionId] IS NOT NULL AND [RequestedByUserId] IS NOT NULL AND [ClientRequestId] IS NOT NULL AND [RequestHash] IS NOT NULL)");
            });
            entity.HasOne(item => item.CivilDesignCase).WithMany(item => item.InformationRequests)
                .HasForeignKey(item => item.CivilDesignCaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.RequestedSection).WithMany()
                .HasForeignKey(item => item.RequestedSectionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDesignInputResponse>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.ProjectRfiId, item.ResponseSequence }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.CentralDocumentVersionId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDesignInputResponses_Lineage");
                table.HasTrigger("TR_ProjectCivilDesignInputResponses_AppendOnly");
                table.HasCheckConstraint("CK_ProjectCivilDesignInputResponses_Sequence", "[ResponseSequence] > 0");
            });
            entity.HasOne(item => item.ProjectRfi).WithMany(item => item.CivilDesignInputResponses)
                .HasForeignKey(item => item.ProjectRfiId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany()
                .HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany()
                .HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.RespondedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilEngineeringDocument>(entity =>
        {
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.FileExtension).IsUnicode(false);
            entity.Property(item => item.ExpectedDocumentReference).IsUnicode(false);
            entity.Property(item => item.DocumentReferenceSnapshot).IsUnicode(false);
            entity.Property(item => item.DmsVersionSnapshot).IsUnicode(false);
            entity.Property(item => item.MetadataTemplateCodeSnapshot).IsUnicode(false);
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.CentralDocumentVersionId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.DocumentKey, item.RevisionNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.SupersedesDocumentId }).IsUnique()
                .HasFilter("[SupersedesDocumentId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.DesignCaseId, item.Status });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilEngineeringDocuments_Lineage");
                table.HasTrigger("TR_ProjectCivilEngineeringDocuments_Lifecycle");
                table.HasCheckConstraint("CK_ProjectCivilEngineeringDocuments_Sequence", "[SequenceNumber] BETWEEN 1 AND 999999 AND [RevisionNumber] BETWEEN 0 AND 9999");
                table.HasCheckConstraint("CK_ProjectCivilEngineeringDocuments_OwnerReviewer", "[OwnerUserId] <> [ReviewerUserId]");
                table.HasCheckConstraint("CK_ProjectCivilEngineeringDocuments_Status", "[Status] IN (0,1,2,3,4,5)");
            });
            entity.HasOne(item => item.DesignCase).WithMany(item => item.EngineeringDocuments)
                .HasForeignKey(item => item.DesignCaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProjectPackage).WithMany().HasForeignKey(item => item.ProjectPackageId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SupersedesDocument).WithMany().HasForeignKey(item => item.SupersedesDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationProfile).WithMany().HasForeignKey(item => item.ConfigurationProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationDecision).WithMany().HasForeignKey(item => item.ConfigurationDecisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.MetadataTemplate).WithMany().HasForeignKey(item => item.MetadataTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.SubmittedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ReviewedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilEngineeringDocumentRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.EngineeringDocumentId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilEngineeringDocumentRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilEngineeringDocumentRevisions_AppendOnly");
            });
            entity.HasOne(item => item.EngineeringDocument).WithMany(item => item.Revisions)
                .HasForeignKey(item => item.EngineeringDocumentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilProjectEngineerAssignment>(entity =>
        {
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.IsActive })
                .IsUnique().HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.EffectiveFrom });
            entity.HasIndex(item => new { item.TenantId, item.AssignedUserId, item.IsActive });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilProjectEngineerAssignments_Lineage");
                table.HasTrigger("TR_ProjectCivilProjectEngineerAssignments_Lifecycle");
                table.HasCheckConstraint("CK_ProjectCivilProjectEngineerAssignments_Period", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.HasCheckConstraint("CK_ProjectCivilProjectEngineerAssignments_Authority", "[Authority] IN (0,1,2)");
                table.HasCheckConstraint("CK_ProjectCivilProjectEngineerAssignments_ActivePeriod", "([IsActive] = 1 AND [EffectiveTo] IS NULL) OR ([IsActive] = 0 AND [EffectiveTo] IS NOT NULL)");
            });
            entity.HasOne(item => item.Project).WithMany(item => item.CivilProjectEngineerAssignments)
                .HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProjectMember).WithMany().HasForeignKey(item => item.ProjectMemberId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationProfile).WithMany().HasForeignKey(item => item.ConfigurationProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationDecision).WithMany().HasForeignKey(item => item.ConfigurationDecisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.AssignedUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilProjectEngineerAssignmentRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.AssignmentId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilProjectEngineerAssignmentRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilProjectEngineerAssignmentRevisions_AppendOnly");
            });
            entity.HasOne(item => item.Assignment).WithMany(item => item.Revisions)
                .HasForeignKey(item => item.AssignmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilSiteInstructionRouting>(entity =>
        {
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ProjectSiteInstructionId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.SupersedesRoutingId }).IsUnique().HasFilter("[SupersedesRoutingId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilSiteInstructionRoutings_Lineage");
                table.HasTrigger("TR_ProjectCivilSiteInstructionRoutings_Lifecycle");
                table.HasCheckConstraint("CK_ProjectCivilSiteInstructionRoutings_Status", "[Status] IN ('PendingApproval','AwaitingContractorAcknowledgement','ContractorResponded','AwaitingEngineeringReview','AwaitingEngineeringFollowUp','Closed','Rejected','Superseded')");
            });
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProjectSiteInstruction).WithMany().HasForeignKey(item => item.ProjectSiteInstructionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProjectEngineerAssignment).WithMany().HasForeignKey(item => item.ProjectEngineerAssignmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SupersedesRouting).WithMany().HasForeignKey(item => item.SupersedesRoutingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationProfile).WithMany().HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationDecision).WithMany().HasForeignKey(item => item.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilSiteInstructionEvidence>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.RoutingId, item.CentralDocumentVersionId, item.EvidenceRole }).IsUnique();
            entity.ToTable(table => table.HasTrigger("TR_ProjectCivilSiteInstructionEvidence_Lineage"));
            entity.HasOne(item => item.Routing).WithMany(item => item.Evidence).HasForeignKey(item => item.RoutingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilSiteInstructionResponse>(entity =>
        {
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.HasIndex(item => new { item.TenantId, item.RoutingId, item.Sequence }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.RoutingId, item.ClientRequestId }).IsUnique();
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilSiteInstructionResponses_Lineage");
                table.HasTrigger("TR_ProjectCivilSiteInstructionResponses_AppendOnly");
            });
            entity.HasOne(item => item.Routing).WithMany(item => item.Responses).HasForeignKey(item => item.RoutingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilSiteInstructionRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.RoutingId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilSiteInstructionRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilSiteInstructionRevisions_AppendOnly");
            });
            entity.HasOne(item => item.Routing).WithMany(item => item.Revisions).HasForeignKey(item => item.RoutingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilRfiRouting>(entity =>
        {
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ProjectRfiId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilRfiRoutings_Lineage");
                table.HasTrigger("TR_ProjectCivilRfiRoutings_Lifecycle");
                table.HasCheckConstraint("CK_ProjectCivilRfiRoutings_Status", "[Status] IN ('AwaitingProjectEngineerResponse','AwaitingProjectManagerApproval','ReturnedToProjectEngineer','Answered','Closed')");
            });
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProjectRfi).WithMany().HasForeignKey(item => item.ProjectRfiId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProjectEngineerAssignment).WithMany().HasForeignKey(item => item.ProjectEngineerAssignmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationProfile).WithMany().HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationDecision).WithMany().HasForeignKey(item => item.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilRfiEvidence>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.RoutingId, item.CentralDocumentVersionId, item.EvidenceRole }).IsUnique();
            entity.ToTable(table => table.HasTrigger("TR_ProjectCivilRfiEvidence_Lineage"));
            entity.HasOne(item => item.Routing).WithMany(item => item.Evidence).HasForeignKey(item => item.RoutingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilRfiResponse>(entity =>
        {
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.HasIndex(item => new { item.TenantId, item.RoutingId, item.Sequence }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.RoutingId, item.ClientRequestId }).IsUnique();
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilRfiResponses_Lineage");
                table.HasTrigger("TR_ProjectCivilRfiResponses_AppendOnly");
                table.HasCheckConstraint("CK_ProjectCivilRfiResponses_Sequence", "[Sequence] > 0");
            });
            entity.HasOne(item => item.Routing).WithMany(item => item.Responses).HasForeignKey(item => item.RoutingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilRfiRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.RoutingId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilRfiRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilRfiRevisions_AppendOnly");
            });
            entity.HasOne(item => item.Routing).WithMany(item => item.Revisions).HasForeignKey(item => item.RoutingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilQualityTestReport>(entity =>
        {
            entity.Property(item => item.ReportReference).IsUnicode(false);
            entity.Property(item => item.SourceType).IsUnicode(false);
            entity.Property(item => item.ResultStatus).IsUnicode(false);
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.EvidenceMetadataTemplateCodeSnapshot).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.ReportReference }).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status, item.TestedAt });
            entity.HasIndex(item => new { item.TenantId, item.ReviewerUserId, item.Status });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilQualityTestReports_Lineage");
                table.HasTrigger("TR_ProjectCivilQualityTestReports_Lifecycle");
                table.HasCheckConstraint("CK_ProjectCivilQualityTestReports_Status", "([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [ApprovedById] IS NULL AND [ApprovedAt] IS NULL AND [RejectionReason] IS NULL) OR ([Status] = 'Approved' AND [ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [RejectionReason] IS NULL) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [ApprovedById] IS NULL AND [ApprovedAt] IS NULL AND [RejectionReason] IS NOT NULL AND LEN(LTRIM(RTRIM([RejectionReason]))) > 0)");
                table.HasCheckConstraint("CK_ProjectCivilQualityTestReports_Source", "[SourceType] IN ('Laboratory','Contractor','Consultant','Internal') AND (([SourceType] = 'Internal' AND [SourceBusinessPartnerId] IS NULL) OR ([SourceType] <> 'Internal' AND [SourceBusinessPartnerId] IS NOT NULL))");
                table.HasCheckConstraint("CK_ProjectCivilQualityTestReports_Result", "[ResultStatus] IN ('Pass','Fail','Inconclusive')");
                table.HasCheckConstraint("CK_ProjectCivilQualityTestReports_Endorsement", "([EndorsementDocumentRecordId] IS NULL AND [EndorsementDocumentVersionId] IS NULL) OR ([EndorsementDocumentRecordId] IS NOT NULL AND [EndorsementDocumentVersionId] IS NOT NULL)");
            });
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProjectPhase).WithMany().HasForeignKey(item => item.ProjectPhaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProjectPackage).WithMany().HasForeignKey(item => item.ProjectPackageId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProjectPaymentCertificate).WithMany().HasForeignKey(item => item.ProjectPaymentCertificateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<BusinessPartner>().WithMany().HasForeignKey(item => item.SourceBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationProfile).WithMany().HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationDecision).WithMany().HasForeignKey(item => item.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.EndorsementDocumentRecord).WithMany().HasForeignKey(item => item.EndorsementDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.EndorsementDocumentVersion).WithMany().HasForeignKey(item => item.EndorsementDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CentralDocumentMetadataTemplate>().WithMany().HasForeignKey(item => item.EvidenceMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilQualityTestRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.TestReportId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilQualityTestRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilQualityTestRevisions_AppendOnly");
            });
            entity.HasOne(item => item.TestReport).WithMany(item => item.Revisions).HasForeignKey(item => item.TestReportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilInspectionControl>(entity =>
        {
            entity.Property(item => item.Stage).IsUnicode(false);
            entity.Property(item => item.Status).IsUnicode(false);
            entity.Property(item => item.PlanApprovalStatus).IsUnicode(false);
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.EvidenceMetadataTemplateCodeSnapshot).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.QualityCheckpointId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.NonConformanceId }).IsUnique().HasFilter("[NonConformanceId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.Stage, item.ScheduledAt });
            entity.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId }).IsUnique().HasFilter("[WorkflowInstanceId] IS NOT NULL AND [IsDeleted] = 0");
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilInspectionControls_Lineage");
                table.HasTrigger("TR_ProjectCivilInspectionControls_Lifecycle");
                table.HasTrigger("TR_ProjectCivilInspectionControls_Workflow");
                table.HasCheckConstraint("CK_ProjectCivilInspectionControls_Stage", "[Stage] IN ('PendingApproval','Scheduled','CorrectiveActionRequired','ReinspectionScheduled','Passed','Closed','Rejected')");
                table.HasCheckConstraint("CK_ProjectCivilInspectionControls_Status", "[Status] IN ('PendingApproval','Scheduled','Active','Blocked','Passed','Closed','Rejected')");
                table.HasCheckConstraint("CK_ProjectCivilInspectionControls_PlanApproval", "[PlanApprovalStatus] IN ('Pending','Approved','Rejected')");
                table.HasCheckConstraint("CK_ProjectCivilInspectionControls_Evidence", "([InspectionDocumentRecordId] IS NULL AND [InspectionDocumentVersionId] IS NULL) OR ([InspectionDocumentRecordId] IS NOT NULL AND [InspectionDocumentVersionId] IS NOT NULL)");
                table.HasCheckConstraint("CK_ProjectCivilInspectionControls_CorrectiveEvidence", "([CorrectiveActionDocumentRecordId] IS NULL AND [CorrectiveActionDocumentVersionId] IS NULL) OR ([CorrectiveActionDocumentRecordId] IS NOT NULL AND [CorrectiveActionDocumentVersionId] IS NOT NULL)");
                table.HasCheckConstraint("CK_ProjectCivilInspectionControls_ReinspectionEvidence", "([ReinspectionDocumentRecordId] IS NULL AND [ReinspectionDocumentVersionId] IS NULL) OR ([ReinspectionDocumentRecordId] IS NOT NULL AND [ReinspectionDocumentVersionId] IS NOT NULL)");
                table.HasCheckConstraint("CK_ProjectCivilInspectionControls_ClosureEvidence", "([ClosureDocumentRecordId] IS NULL AND [ClosureDocumentVersionId] IS NULL) OR ([ClosureDocumentRecordId] IS NOT NULL AND [ClosureDocumentVersionId] IS NOT NULL)");
            });
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.QualityCheckpoint).WithMany().HasForeignKey(item => item.QualityCheckpointId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PlanningGisValidation).WithMany().HasForeignKey(item => item.PlanningGisValidationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.NonConformance).WithMany().HasForeignKey(item => item.NonConformanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PlanDocumentRecord).WithMany().HasForeignKey(item => item.PlanDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PlanDocumentVersion).WithMany().HasForeignKey(item => item.PlanDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CentralDocumentMetadataTemplate>().WithMany().HasForeignKey(item => item.EvidenceMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CivilEngineeringConfigurationProfile>().WithMany().HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CivilEngineeringConfigurationDecision>().WithMany().HasForeignKey(item => item.SupervisionConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CivilEngineeringConfigurationDecision>().WithMany().HasForeignKey(item => item.QualityConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.InspectorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ReinspectionInspectorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ClosedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Workflow.WorkflowDefinition>().WithMany().HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Workflow.WorkflowInstance>().WithMany().HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.PlanSubmittedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.PlanApprovedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilInspectionRevision>(entity =>
        {
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.HasIndex(item => new { item.TenantId, item.InspectionControlId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilInspectionRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilInspectionRevisions_AppendOnly");
            });
            entity.HasOne(item => item.InspectionControl).WithMany(item => item.Revisions).HasForeignKey(item => item.InspectionControlId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilIpcEndorsement>(entity =>
        {
            entity.Property(item => item.Status).IsUnicode(false);
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.EvidenceMetadataTemplateCodeSnapshot).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProjectPaymentCertificateId, item.Sequence }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status, item.SubmittedAt });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilIpcEndorsements_Lineage");
                table.HasTrigger("TR_ProjectCivilIpcEndorsements_Lifecycle");
                table.HasCheckConstraint("CK_ProjectCivilIpcEndorsements_Sequence", "[Sequence] > 0");
                table.HasCheckConstraint("CK_ProjectCivilIpcEndorsements_Evidence", "([EvidenceDocumentRecordId] IS NULL AND [EvidenceDocumentVersionId] IS NULL) OR ([EvidenceDocumentRecordId] IS NOT NULL AND [EvidenceDocumentVersionId] IS NOT NULL)");
                table.HasCheckConstraint("CK_ProjectCivilIpcEndorsements_Status", "([Status] = 'AwaitingProjectEngineerReview' AND [ReviewedById] IS NULL AND [ReviewedAt] IS NULL AND [ReviewNotes] IS NULL AND [EvidenceDocumentRecordId] IS NULL AND [EvidenceDocumentVersionId] IS NULL) OR ([Status] = 'Endorsed' AND [ReviewedById] IS NOT NULL AND [ReviewedAt] IS NOT NULL AND [ReviewNotes] IS NOT NULL AND LEN(LTRIM(RTRIM([ReviewNotes]))) > 0) OR ([Status] = 'ReturnedToProjectsCoordinator' AND [ReviewedById] IS NOT NULL AND [ReviewedAt] IS NOT NULL AND [ReviewNotes] IS NOT NULL AND LEN(LTRIM(RTRIM([ReviewNotes]))) > 0 AND [EvidenceDocumentRecordId] IS NULL AND [EvidenceDocumentVersionId] IS NULL)");
            });
            entity.HasOne(item => item.ProjectPaymentCertificate).WithMany().HasForeignKey(item => item.ProjectPaymentCertificateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProjectEngineerAssignment).WithMany().HasForeignKey(item => item.ProjectEngineerAssignmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationProfile).WithMany().HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationDecision).WithMany().HasForeignKey(item => item.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.EvidenceMetadataTemplate).WithMany().HasForeignKey(item => item.EvidenceMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.EvidenceDocumentRecord).WithMany().HasForeignKey(item => item.EvidenceDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.EvidenceDocumentVersion).WithMany().HasForeignKey(item => item.EvidenceDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilIpcEndorsementRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.IpcEndorsementId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilIpcEndorsementRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilIpcEndorsementRevisions_AppendOnly");
            });
            entity.HasOne(item => item.IpcEndorsement).WithMany(item => item.Revisions).HasForeignKey(item => item.IpcEndorsementId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilWeeklySupervisionReport>(entity =>
        {
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.EvidenceMetadataTemplateCodeSnapshot).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.WeekStart }).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.Status, item.DueAt });
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.SiteStatus, item.WeekStart });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilWeeklySupervisionReports_Lineage");
                table.HasTrigger("TR_ProjectCivilWeeklySupervisionReports_Lifecycle");
                table.HasCheckConstraint("CK_ProjectCivilWeeklySupervisionReports_Week", "DATEDIFF(day, [WeekStart], [WeekEnd]) = 6 AND DATEDIFF(day, CONVERT(date, '19000101', 112), CONVERT(date, [WeekStart])) % 7 = 0");
                table.HasCheckConstraint("CK_ProjectCivilWeeklySupervisionReports_Status", "([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [ApprovedById] IS NULL AND [ApprovedAt] IS NULL AND [RejectionReason] IS NULL) OR ([Status] = 'Approved' AND [ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [RejectionReason] IS NULL) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [ApprovedById] IS NULL AND [ApprovedAt] IS NULL AND [RejectionReason] IS NOT NULL AND LEN(LTRIM(RTRIM([RejectionReason]))) > 0)");
                table.HasCheckConstraint("CK_ProjectCivilWeeklySupervisionReports_ProgressControl", "[HasGovernedProgressControl] = 0 OR ([ProjectMilestoneId] IS NOT NULL AND [OverallProgressPercent] IS NOT NULL AND [SiteStatus] IN ('OnTrack','AtRisk','Delayed','Stopped') AND (([SiteStatus] = 'OnTrack' AND [DelayReason] IS NULL AND [RecoveryActionItemId] IS NULL) OR ([SiteStatus] IN ('AtRisk','Delayed','Stopped') AND LEN(LTRIM(RTRIM(ISNULL([DelayReason],'')))) >= 3 AND [RecoveryActionItemId] IS NOT NULL)) AND (([IsProgressCorrection] = 0 AND [ProgressCorrectionDecisionId] IS NULL) OR ([IsProgressCorrection] = 1 AND (([Status] <> 'Approved' AND [ProgressCorrectionDecisionId] IS NULL) OR ([Status] = 'Approved' AND [ProgressCorrectionDecisionId] IS NOT NULL)))))");
                table.HasCheckConstraint("CK_ProjectCivilWeeklySupervisionReports_MilestoneScheduleSnapshot", "[HasGovernedProgressControl] = 0 OR [MilestoneTargetDateSnapshot] IS NOT NULL");
            });
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProjectEngineerAssignment).WithMany().HasForeignKey(item => item.ProjectEngineerAssignmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectMilestone>().WithMany().HasForeignKey(item => item.ProjectMilestoneId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectRisk>().WithMany().HasForeignKey(item => item.ProjectRiskId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectIssue>().WithMany().HasForeignKey(item => item.ProjectIssueId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectTaskDependency>().WithMany().HasForeignKey(item => item.ProjectTaskDependencyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectActionItem>().WithMany().HasForeignKey(item => item.RecoveryActionItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectDecision>().WithMany().HasForeignKey(item => item.ProgressCorrectionDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationProfile).WithMany().HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationDecision).WithMany().HasForeignKey(item => item.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CentralDocumentMetadataTemplate>().WithMany().HasForeignKey(item => item.EvidenceMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilWeeklySupervisionActivity>(entity =>
        {
            entity.Property(item => item.ActorType).IsUnicode(false);
            entity.HasIndex(item => new { item.TenantId, item.ReportId, item.Sequence }).IsUnique();
            entity.ToTable(table => table.HasTrigger("TR_ProjectCivilWeeklySupervisionActivities_Lineage"));
            entity.HasOne(item => item.Report).WithMany(item => item.Activities).HasForeignKey(item => item.ReportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ActivityCategory).WithMany().HasForeignKey(item => item.ActivityCategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<BusinessPartner>().WithMany().HasForeignKey(item => item.ContractorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilWeeklySupervisionEvidence>(entity =>
        {
            entity.Property(item => item.EvidenceRole).IsUnicode(false);
            entity.HasIndex(item => new { item.TenantId, item.ReportId, item.CentralDocumentVersionId }).IsUnique();
            entity.ToTable(table => table.HasTrigger("TR_ProjectCivilWeeklySupervisionEvidence_Lineage"));
            entity.HasOne(item => item.Report).WithMany(item => item.Evidence).HasForeignKey(item => item.ReportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.LinkedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilWeeklySupervisionReview>(entity =>
        {
            entity.Property(item => item.Action).IsUnicode(false);
            entity.Property(item => item.Outcome).IsUnicode(false);
            entity.HasIndex(item => new { item.TenantId, item.ReportId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ReportId, item.Sequence }).IsUnique();
            entity.ToTable(table => table.HasTrigger("TR_ProjectCivilWeeklySupervisionReviews_Lineage"));
            entity.HasOne(item => item.Report).WithMany(item => item.Reviews).HasForeignKey(item => item.ReportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilWeeklySupervisionRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.ReportId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilWeeklySupervisionRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilWeeklySupervisionRevisions_AppendOnly");
            });
            entity.HasOne(item => item.Report).WithMany(item => item.Revisions).HasForeignKey(item => item.ReportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CivilEngineeringMaintenanceIntake>(entity =>
        {
            entity.Property(item => item.IntakeNumber).IsUnicode(false);
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.EvidenceMetadataTemplateCodeSnapshot).IsUnicode(false);
            entity.Property(item => item.Status).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.IntakeNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.Status, item.WorkClassification });
            entity.HasIndex(item => new { item.TenantId, item.MaintenanceAssetId, item.EstateManagedAssetId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_CivilEngineeringMaintenanceIntakes_Lineage");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceIntakes_Target", "([MaintenanceAssetId] IS NOT NULL AND [EstateManagedAssetId] IS NULL) OR ([MaintenanceAssetId] IS NULL AND [EstateManagedAssetId] IS NOT NULL)");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceIntakes_SourceLink", "([WorkClassification] = 2 AND [Source] = 1 AND [MaintenanceScheduleId] IS NOT NULL AND [HelpdeskTicketId] IS NULL) OR ([WorkClassification] = 3 AND [MaintenanceScheduleId] IS NULL AND [HelpdeskTicketId] IS NULL) OR ([WorkClassification] = 5 AND [Source] IN (2,3) AND [MaintenanceScheduleId] IS NULL AND [HelpdeskTicketId] IS NOT NULL)");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceIntakes_Status", "[Status] IN ('Logged','AssessmentInProgress','AssessmentReturned','Assessed')");
            });
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.MaintenanceAsset).WithMany().HasForeignKey(item => item.MaintenanceAssetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.EstateManagedAsset).WithMany().HasForeignKey(item => item.EstateManagedAssetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.MaintenanceSchedule).WithMany().HasForeignKey(item => item.MaintenanceScheduleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.HelpdeskTicket).WithMany().HasForeignKey(item => item.HelpdeskTicketId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.RequesterUser).WithMany().HasForeignKey(item => item.RequesterUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PriorityLevel).WithMany().HasForeignKey(item => item.PriorityLevelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationProfile).WithMany().HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationDecision).WithMany().HasForeignKey(item => item.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.EvidenceMetadataTemplate).WithMany().HasForeignKey(item => item.EvidenceMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CivilEngineeringMaintenanceIntakeRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.IntakeId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_CivilEngineeringMaintenanceIntakeRevisions_Lineage");
                table.HasTrigger("TR_CivilEngineeringMaintenanceIntakeRevisions_AppendOnly");
            });
            entity.HasOne(item => item.Intake).WithMany(item => item.Revisions).HasForeignKey(item => item.IntakeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CivilEngineeringMaintenanceAssessment>(entity =>
        {
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.Stage).IsUnicode(false);
            entity.Property(item => item.Status).IsUnicode(false);
            entity.Property(item => item.ApprovalStatus).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.IntakeId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.CurrentAssigneeUserId, item.Stage });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_CivilEngineeringMaintenanceAssessments_Lineage");
                table.HasTrigger("TR_CivilEngineeringMaintenanceAssessments_Lifecycle");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceAssessments_Stage", "[Stage] IN ('SceAssignment','CivilEngineerAssessment','SceAssessmentReview','HodFinalReview','Approved','Rejected')");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceAssessments_Status", "[Status] IN ('InProgress','PendingApproval','Approved','Rejected') AND [ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceAssessments_Assignment", "[HodUserId]<>[SupervisingCivilEngineerUserId] AND ([CivilEngineerUserId] IS NULL OR ([CivilEngineerUserId]<>[HodUserId] AND [CivilEngineerUserId]<>[SupervisingCivilEngineerUserId]))");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceAssessments_Evidence", "([CentralDocumentRecordId] IS NULL AND [CentralDocumentVersionId] IS NULL) OR ([CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL)");
            });
            entity.HasOne(item => item.Intake).WithMany().HasForeignKey(item => item.IntakeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.HodUser).WithMany().HasForeignKey(item => item.HodUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SupervisingCivilEngineerUser).WithMany().HasForeignKey(item => item.SupervisingCivilEngineerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CivilEngineerUser).WithMany().HasForeignKey(item => item.CivilEngineerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CurrentAssigneeUser).WithMany().HasForeignKey(item => item.CurrentAssigneeUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.DefectCategory).WithMany().HasForeignKey(item => item.DefectCategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CivilEngineeringMaintenanceAssessmentRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.AssessmentId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_CivilEngineeringMaintenanceAssessmentRevisions_Lineage");
                table.HasTrigger("TR_CivilEngineeringMaintenanceAssessmentRevisions_AppendOnly");
            });
            entity.HasOne(item => item.Assessment).WithMany(item => item.Revisions).HasForeignKey(item => item.AssessmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CivilEngineeringMaintenanceCostingHandoff>(entity =>
        {
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.Stage).IsUnicode(false);
            entity.Property(item => item.Status).IsUnicode(false);
            entity.Property(item => item.ApprovalStatus).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.AssessmentId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.Stage });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_CivilEngineeringMaintenanceCostingHandoffs_Lineage");
                table.HasTrigger("TR_CivilEngineeringMaintenanceCostingHandoffs_Lifecycle");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceCostingHandoffs_Stage", "[Stage] IN ('Draft','CostingReview','ProcurementAndAward','Awarded','Rejected')");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceCostingHandoffs_Status", "[Status] IN ('Draft','PendingApproval','Approved','Awarded','Rejected') AND [ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
            });
            entity.HasOne(item => item.Assessment).WithMany().HasForeignKey(item => item.AssessmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.QuantitySurveyEstimateVersion).WithMany().HasForeignKey(item => item.QuantitySurveyEstimateVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProjectBudgetRevision).WithMany().HasForeignKey(item => item.ProjectBudgetRevisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PurchaseRequisition).WithMany().HasForeignKey(item => item.PurchaseRequisitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Contract).WithMany().HasForeignKey(item => item.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationProfile).WithMany().HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationDecision).WithMany().HasForeignKey(item => item.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CivilEngineeringMaintenanceCostingHandoffRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.HandoffId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_CivilEngineeringMaintenanceCostingHandoffRevisions_Lineage");
                table.HasTrigger("TR_CivilEngineeringMaintenanceCostingHandoffRevisions_AppendOnly");
            });
            entity.HasOne(item => item.Handoff).WithMany(item => item.Revisions).HasForeignKey(item => item.HandoffId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CivilEngineeringMaintenanceExecutionLink>(entity =>
        {
            entity.Property(item => item.LinkMode).IsUnicode(false);
            entity.Property(item => item.Stage).IsUnicode(false);
            entity.Property(item => item.Status).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.HandoffId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.Stage });
            entity.HasIndex(item => new { item.TenantId, item.JobCardId }).IsUnique().HasFilter("[JobCardId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.WorkOrderId }).IsUnique().HasFilter("[WorkOrderId] IS NOT NULL AND [IsDeleted] = 0");
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_CivilEngineeringMaintenanceExecutionLinks_Lineage");
                table.HasTrigger("TR_CivilEngineeringMaintenanceExecutionLinks_Lifecycle");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceExecutionLinks_Mode", "[LinkMode] IN ('CreateJobCard','LinkExisting')");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceExecutionLinks_Target", "[JobCardId] IS NOT NULL OR [WorkOrderId] IS NOT NULL");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceExecutionLinks_Stage", "[Stage] IN ('AwaitingJobCardApproval','AwaitingWorkOrder','WorkInProgress','Completed','Blocked') AND [Status] IN ('Pending','Active','Completed','Blocked')");
            });
            entity.HasOne(item => item.Handoff).WithMany().HasForeignKey(item => item.HandoffId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.MaintenanceAsset).WithMany().HasForeignKey(item => item.MaintenanceAssetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.JobCard).WithMany().HasForeignKey(item => item.JobCardId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WorkOrder).WithMany().HasForeignKey(item => item.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CivilEngineeringMaintenanceExecutionLinkRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.ExecutionLinkId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_CivilEngineeringMaintenanceExecutionLinkRevisions_Lineage");
                table.HasTrigger("TR_CivilEngineeringMaintenanceExecutionLinkRevisions_AppendOnly");
            });
            entity.HasOne(item => item.ExecutionLink).WithMany(item => item.Revisions).HasForeignKey(item => item.ExecutionLinkId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CivilEngineeringMaintenanceCompletionControl>(entity =>
        {
            entity.Property(item => item.Stage).IsUnicode(false);
            entity.Property(item => item.Status).IsUnicode(false);
            entity.Property(item => item.InspectionStatus).IsUnicode(false);
            entity.Property(item => item.PaymentDirectionStatus).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ExecutionLinkId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.Stage });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_CivilEngineeringMaintenanceCompletionControls_Lineage");
                table.HasTrigger("TR_CivilEngineeringMaintenanceCompletionControls_Lifecycle");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceCompletionControls_Stage", "[Stage] IN ('SceReview','HodReview','AwaitingInspectionDirection','InspectionInProgress','AwaitingPaymentDirection','AwaitingClosure','RemediationRequired','Closed','Returned') AND [Status] IN ('Pending','Active','Returned','Blocked','Closed')");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceCompletionControls_Inspection", "[InspectionStatus] IN ('NotDirected','Directed','Passed','Failed')");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceCompletionControls_Payment", "[PaymentDirectionStatus] IN ('NotDirected','Directed')");
                table.HasCheckConstraint("CK_CivilEngineeringMaintenanceCompletionControls_Evidence", "[CompletionDocumentRecordId] IS NOT NULL AND [CompletionDocumentVersionId] IS NOT NULL AND ([InspectionDirectionDocumentRecordId] IS NULL AND [InspectionDirectionDocumentVersionId] IS NULL OR [InspectionDirectionDocumentRecordId] IS NOT NULL AND [InspectionDirectionDocumentVersionId] IS NOT NULL) AND ([InspectionOutcomeDocumentRecordId] IS NULL AND [InspectionOutcomeDocumentVersionId] IS NULL OR [InspectionOutcomeDocumentRecordId] IS NOT NULL AND [InspectionOutcomeDocumentVersionId] IS NOT NULL) AND ([PaymentDirectionDocumentRecordId] IS NULL AND [PaymentDirectionDocumentVersionId] IS NULL OR [PaymentDirectionDocumentRecordId] IS NOT NULL AND [PaymentDirectionDocumentVersionId] IS NOT NULL) AND ([ClosureDocumentRecordId] IS NULL AND [ClosureDocumentVersionId] IS NULL OR [ClosureDocumentRecordId] IS NOT NULL AND [ClosureDocumentVersionId] IS NOT NULL)");
            });
            entity.HasOne(item => item.ExecutionLink).WithMany().HasForeignKey(item => item.ExecutionLinkId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.MaintenanceAsset).WithMany().HasForeignKey(item => item.MaintenanceAssetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.JobCard).WithMany().HasForeignKey(item => item.JobCardId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WorkOrder).WithMany().HasForeignKey(item => item.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CivilEngineerUser).WithMany().HasForeignKey(item => item.CivilEngineerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SupervisingCivilEngineerUser).WithMany().HasForeignKey(item => item.SupervisingCivilEngineerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.HodUser).WithMany().HasForeignKey(item => item.HodUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CivilEngineeringMaintenanceCompletionRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.CompletionControlId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_CivilEngineeringMaintenanceCompletionRevisions_Lineage");
                table.HasTrigger("TR_CivilEngineeringMaintenanceCompletionRevisions_AppendOnly");
            });
            entity.HasOne(item => item.CompletionControl).WithMany(item => item.Revisions).HasForeignKey(item => item.CompletionControlId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDevelopmentApprovalFile>(entity =>
        {
            entity.Property(item => item.FileNumber).IsUnicode(false);
            entity.Property(item => item.ApplicationReference).IsUnicode(false);
            entity.Property(item => item.CurrentSection).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.MetadataTemplateCodeSnapshot).IsUnicode(false);
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.FileNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.EstateManagedAssetId, item.Status });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalFiles_Lineage");
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalFiles_Lifecycle");
                table.HasCheckConstraint("CK_ProjectCivilDevelopmentApprovalFiles_Status", "[Status] IN (0,1,2)");
                table.HasCheckConstraint("CK_ProjectCivilDevelopmentApprovalFiles_Section", "[CurrentSection] = 'BuildingInspectorate'");
                table.HasCheckConstraint("CK_ProjectCivilDevelopmentApprovalFiles_Dates", "CONVERT(date, [DueDate]) >= CONVERT(date, [CreatedAt]) AND ([SiteInspectionDueDate] IS NULL OR [SiteInspectionDueDate] <= [DueDate]) AND ([SiteInspectedAt] IS NULL OR [SiteInspectedAt] >= [CreatedAt])");
            });
            entity.HasOne(item => item.ApplicantBusinessPartner).WithMany().HasForeignKey(item => item.ApplicantBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.EstateManagedAsset).WithMany().HasForeignKey(item => item.EstateManagedAssetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationProfile).WithMany().HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PermittingConfigurationDecision).WithMany().HasForeignKey(item => item.PermittingConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.DocumentConfigurationDecision).WithMany().HasForeignKey(item => item.DocumentConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.MetadataTemplate).WithMany().HasForeignKey(item => item.MetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDevelopmentApprovalEvidence>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.DevelopmentApprovalFileId, item.CentralDocumentVersionId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.DevelopmentApprovalFileId, item.Kind });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalEvidence_Lineage");
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalEvidence_AppendOnly");
                table.HasCheckConstraint("CK_ProjectCivilDevelopmentApprovalEvidence_Kind", "[Kind] IN (0,1)");
            });
            entity.HasOne(item => item.DevelopmentApprovalFile).WithMany(item => item.Evidence).HasForeignKey(item => item.DevelopmentApprovalFileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDevelopmentApprovalFileRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.DevelopmentApprovalFileId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalFileRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalFileRevisions_AppendOnly");
            });
            entity.HasOne(item => item.DevelopmentApprovalFile).WithMany(item => item.Revisions).HasForeignKey(item => item.DevelopmentApprovalFileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDevelopmentApprovalFileHandoff>(entity =>
        {
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.DevelopmentApprovalFileId, item.SequenceNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.DevelopmentApprovalFileId, item.CreatedAt });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalFileHandoffs_Lineage");
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalFileHandoffs_AppendOnly");
                table.HasCheckConstraint("CK_ProjectCivilDevelopmentApprovalFileHandoffs_Sequence", "[SequenceNumber] BETWEEN 1 AND 999999");
                table.HasCheckConstraint("CK_ProjectCivilDevelopmentApprovalFileHandoffs_Sections", "[FromSection] BETWEEN 0 AND 6 AND [ToSection] BETWEEN 0 AND 6 AND [FromSection] <> [ToSection]");
                table.HasCheckConstraint("CK_ProjectCivilDevelopmentApprovalFileHandoffs_DueDate", "CONVERT(date, [DueDate]) >= CONVERT(date, [CreatedAt])");
            });
            entity.HasOne(item => item.DevelopmentApprovalFile).WithMany(item => item.Handoffs).HasForeignKey(item => item.DevelopmentApprovalFileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.FromUser).WithMany().HasForeignKey(item => item.FromUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.RecipientRole).WithMany().HasForeignKey(item => item.RecipientRoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.RecipientUser).WithMany().HasForeignKey(item => item.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDevelopmentApprovalHandoffEvidence>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.HandoffId, item.CentralDocumentVersionId }).IsUnique();
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalHandoffEvidence_Lineage");
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalHandoffEvidence_AppendOnly");
            });
            entity.HasOne(item => item.Handoff).WithMany(item => item.Evidence).HasForeignKey(item => item.HandoffId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDevelopmentApprovalEngineeringReview>(entity =>
        {
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.Status).IsUnicode(false);
            entity.Property(item => item.ApprovalStatus).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.SourceHandoffId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.DevelopmentApprovalFileId, item.Stage });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalEngineeringReviews_Lineage");
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalEngineeringReviews_Lifecycle");
                table.HasCheckConstraint("CK_ProjectCivilDevelopmentApprovalEngineeringReviews_Stage", "[Stage] IN (0,1,2,3,4)");
                table.HasCheckConstraint("CK_ProjectCivilDevelopmentApprovalEngineeringReviews_Outcome", "[RecommendedOutcome] IN (0,1,2,3)");
                table.HasCheckConstraint("CK_ProjectCivilDevelopmentApprovalEngineeringReviews_Status", "[Status] IN ('CorrectionRequested','PendingApproval','Approved','Rejected','Draft') AND [ApprovalStatus] IN ('NotApplicable','Pending','Approved','Rejected','Draft')");
            });
            entity.HasOne(item => item.DevelopmentApprovalFile).WithMany(item => item.EngineeringReviews).HasForeignKey(item => item.DevelopmentApprovalFileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SourceHandoff).WithMany().HasForeignKey(item => item.SourceHandoffId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReviewerUser).WithMany().HasForeignKey(item => item.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CommentCategory).WithMany().HasForeignKey(item => item.CommentCategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDevelopmentApprovalEngineeringReviewRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.EngineeringReviewId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalEngineeringReviewRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalEngineeringReviewRevisions_AppendOnly");
            });
            entity.HasOne(item => item.EngineeringReview).WithMany(item => item.Revisions).HasForeignKey(item => item.EngineeringReviewId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDevelopmentApprovalEngineeringReviewDecision>(entity =>
        {
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.WorkflowAction).IsUnicode(false);
            entity.Property(item => item.WorkflowOutcome).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.EngineeringReviewId }).IsUnique();
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_Lineage");
                table.HasTrigger("TR_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_AppendOnly");
                table.HasCheckConstraint("CK_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_Outcome", "[Outcome] IN (0,1,2)");
            });
            entity.HasOne(item => item.EngineeringReview).WithMany(item => item.HodDecisions).HasForeignKey(item => item.EngineeringReviewId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.DecidedBy).WithMany().HasForeignKey(item => item.DecidedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowInstance>().WithMany().HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDirectTaskControl>(entity =>
        {
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.Status).IsUnicode(false);
            entity.Property(item => item.ApprovalStatus).IsUnicode(false);
            entity.Property(item => item.ProgressPercent).HasPrecision(5, 2);
            entity.Property(item => item.LastFeedbackRequestHash).IsUnicode(false);
            entity.Property(item => item.UrgentEscalationRequestHash).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.WorkItemId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.AssignedToUserId, item.DueDate });
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.IsUrgentPath, item.UrgentResponseDueAt, item.UrgentEscalatedAt });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDirectTaskControls_Lineage");
                table.HasTrigger("TR_ProjectCivilDirectTaskControls_Lifecycle");
                table.HasTrigger("TR_ProjectCivilDirectTaskControls_FeedbackLifecycle");
                table.HasTrigger("TR_ProjectCivilDirectTaskControls_UrgentPath");
                table.HasCheckConstraint("CK_ProjectCivilDirectTaskControls_Urgency", "[Urgency] IN (0,1,2,3)");
                table.HasCheckConstraint("CK_ProjectCivilDirectTaskControls_Evidence", "([CentralDocumentRecordId] IS NULL AND [CentralDocumentVersionId] IS NULL) OR ([CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL)");
                table.HasCheckConstraint("CK_ProjectCivilDirectTaskControls_Status", "[Status] IN ('Assigned','InProgress','PendingAcceptance','Accepted','Returned','Cancelled') AND [ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                table.HasCheckConstraint("CK_ProjectCivilDirectTaskControls_Progress", "[ProgressPercent] >= 0 AND [ProgressPercent] <= 100");
                table.HasCheckConstraint("CK_ProjectCivilDirectTaskControls_UrgentPath", "([IsUrgentPath] = 0 AND [UrgencyReason] IS NULL AND [UrgentResponseDueAt] IS NULL AND [UrgentEscalatedAt] IS NULL AND [UrgentEscalationClientRequestId] IS NULL AND [UrgentEscalationRequestHash] IS NULL) OR ([IsUrgentPath] = 1 AND [Urgency] IN (2,3) AND [UrgencyReason] IS NOT NULL AND LEN(LTRIM(RTRIM([UrgencyReason]))) BETWEEN 5 AND 1000 AND [UrgentResponseDueAt] IS NOT NULL AND [DueDate] <= [UrgentResponseDueAt])");
            });
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WorkItem).WithMany().HasForeignKey(item => item.WorkItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.AssignedToUser).WithMany().HasForeignKey(item => item.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.AssignedRole).WithMany().HasForeignKey(item => item.AssignedRoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationProfile).WithMany().HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationDecision).WithMany().HasForeignKey(item => item.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.FeedbackMetadataTemplate).WithMany().HasForeignKey(item => item.FeedbackMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowInstance>().WithMany().HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDirectTaskRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.DirectTaskControlId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDirectTaskRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilDirectTaskRevisions_AppendOnly");
            });
            entity.HasOne(item => item.DirectTaskControl).WithMany(item => item.Revisions).HasForeignKey(item => item.DirectTaskControlId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilDirectTaskFeedbackEntry>(entity =>
        {
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.WorkflowOutcome).IsUnicode(false);
            entity.Property(item => item.ProgressPercent).HasPrecision(5, 2);
            entity.Property(item => item.MeasurementValue).HasPrecision(18, 4);
            entity.HasIndex(item => new { item.TenantId, item.DirectTaskControlId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.DirectTaskControlId, item.Sequence }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.DirectTaskControlId, item.CreatedAt });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilDirectTaskFeedbackEntries_Lineage");
                table.HasTrigger("TR_ProjectCivilDirectTaskFeedbackEntries_AppendOnly");
                table.HasCheckConstraint("CK_ProjectCivilDirectTaskFeedbackEntries_Action", "[Action] IN (0,1,2,3,4)");
                table.HasCheckConstraint("CK_ProjectCivilDirectTaskFeedbackEntries_Progress", "[ProgressPercent] IS NULL OR ([ProgressPercent] >= 0 AND [ProgressPercent] <= 100)");
                table.HasCheckConstraint("CK_ProjectCivilDirectTaskFeedbackEntries_Measurement", "([MeasurementValue] IS NULL AND [MeasurementUnitId] IS NULL) OR ([MeasurementValue] IS NOT NULL AND [MeasurementValue] >= 0 AND [MeasurementUnitId] IS NOT NULL)");
                table.HasCheckConstraint("CK_ProjectCivilDirectTaskFeedbackEntries_Evidence", "([CentralDocumentRecordId] IS NULL AND [CentralDocumentVersionId] IS NULL) OR ([CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL)");
            });
            entity.HasOne(item => item.DirectTaskControl).WithMany(item => item.FeedbackEntries).HasForeignKey(item => item.DirectTaskControlId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ActorUser).WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.MeasurementUnit).WithMany().HasForeignKey(item => item.MeasurementUnitId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilMigrationBatch>(entity =>
        {
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status, item.CreatedAt });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilMigrationBatches_Lineage");
                table.HasTrigger("TR_ProjectCivilMigrationBatches_Lifecycle");
                table.HasCheckConstraint("CK_ProjectCivilMigrationBatches_Status", "[Status] IN (0,1,2,3)");
                table.HasCheckConstraint("CK_ProjectCivilMigrationBatches_Counts", "[RecordCount] > 0 AND [ErrorCount] >= 0 AND [ErrorCount] <= [RecordCount]");
                table.HasCheckConstraint("CK_ProjectCivilMigrationBatches_ReconciliationEvidence", "([ReconciliationDocumentRecordId] IS NULL AND [ReconciliationDocumentVersionId] IS NULL) OR ([ReconciliationDocumentRecordId] IS NOT NULL AND [ReconciliationDocumentVersionId] IS NOT NULL)");
                table.HasCheckConstraint("CK_ProjectCivilMigrationBatches_State", "([Status] IN (0,1) AND [ReconciledByUserId] IS NULL AND [ReconciledAt] IS NULL AND [ReconciliationDocumentRecordId] IS NULL AND [ReconciliationDocumentVersionId] IS NULL AND [ReconciliationDeclaration] IS NULL AND [SignedOffByUserId] IS NULL AND [SignedOffAt] IS NULL AND [SignOffDeclaration] IS NULL) OR ([Status] = 2 AND [ReconciledByUserId] IS NOT NULL AND [ReconciledAt] IS NOT NULL AND [ReconciliationDocumentRecordId] IS NOT NULL AND [ReconciliationDocumentVersionId] IS NOT NULL AND LEN(LTRIM(RTRIM([ReconciliationDeclaration]))) >= 5 AND [SignedOffByUserId] IS NULL AND [SignedOffAt] IS NULL AND [SignOffDeclaration] IS NULL) OR ([Status] = 3 AND [ReconciledByUserId] IS NOT NULL AND [ReconciledAt] IS NOT NULL AND [ReconciliationDocumentRecordId] IS NOT NULL AND [ReconciliationDocumentVersionId] IS NOT NULL AND LEN(LTRIM(RTRIM([ReconciliationDeclaration]))) >= 5 AND [SignedOffByUserId] IS NOT NULL AND [SignedOffAt] IS NOT NULL AND LEN(LTRIM(RTRIM([SignOffDeclaration]))) >= 5)");
            });
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationProfile).WithMany().HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationDecision).WithMany().HasForeignKey(item => item.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReconciliationEvidenceTemplate).WithMany().HasForeignKey(item => item.ReconciliationEvidenceTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReconciliationDocumentRecord).WithMany().HasForeignKey(item => item.ReconciliationDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReconciliationDocumentVersion).WithMany().HasForeignKey(item => item.ReconciliationDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ReconciledByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.SignedOffByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilMigrationRecord>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.MigrationBatchId, item.Sequence }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.MigrationBatchId, item.SourceReference }).IsUnique();
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilMigrationRecords_Lineage");
                table.HasTrigger("TR_ProjectCivilMigrationRecords_AppendOnly");
                table.HasCheckConstraint("CK_ProjectCivilMigrationRecords_Type", "[RecordType] IN (0,1,2,3,4,5,6,7)");
                table.HasCheckConstraint("CK_ProjectCivilMigrationRecords_Evidence", "([CentralDocumentRecordId] IS NULL AND [CentralDocumentVersionId] IS NULL) OR ([CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL)");
                table.HasCheckConstraint("CK_ProjectCivilMigrationRecords_PhysicalReference", "([RecordType] = 7 AND [PhysicalFileReference] IS NOT NULL) OR [RecordType] <> 7");
            });
            entity.HasOne(item => item.MigrationBatch).WithMany(item => item.Records).HasForeignKey(item => item.MigrationBatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilMigrationValidationIssue>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.MigrationBatchId, item.Sequence, item.Code });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilMigrationValidationIssues_Lineage");
                table.HasTrigger("TR_ProjectCivilMigrationValidationIssues_AppendOnly");
            });
            entity.HasOne(item => item.MigrationBatch).WithMany(item => item.ValidationIssues).HasForeignKey(item => item.MigrationBatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.MigrationRecord).WithMany().HasForeignKey(item => item.MigrationRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilMigrationRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.MigrationBatchId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilMigrationRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilMigrationRevisions_AppendOnly");
            });
            entity.HasOne(item => item.MigrationBatch).WithMany(item => item.Revisions).HasForeignKey(item => item.MigrationBatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilExtensionOfTimeControl>(entity =>
        {
            entity.Property(item => item.Status).IsUnicode(false);
            entity.Property(item => item.ApprovalStatus).IsUnicode(false);
            entity.Property(item => item.PolicyHash).IsUnicode(false);
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.Property(item => item.LastMutationRequestHash).IsUnicode(false);
            entity.Property(item => item.EvidenceMetadataTemplateCodeSnapshot).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProjectExtensionOfTimeId }).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status, item.SubmittedAt });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilExtensionOfTimeControls_Lineage");
                table.HasTrigger("TR_ProjectCivilExtensionOfTimeControls_Lifecycle");
                table.HasCheckConstraint("CK_ProjectCivilExtensionOfTimeControls_State", "([Status] = 'PendingApproval' AND [ApprovalStatus] = 'Pending' AND [ApprovedById] IS NULL AND [ApprovedAt] IS NULL AND [RejectionReason] IS NULL) OR ([Status] = 'Approved' AND [ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [RejectionReason] IS NULL) OR ([Status] = 'Rejected' AND [ApprovalStatus] = 'Rejected' AND [ApprovedById] IS NULL AND [ApprovedAt] IS NULL AND [RejectionReason] IS NOT NULL AND LEN(LTRIM(RTRIM([RejectionReason]))) >= 3)");
                table.HasCheckConstraint("CK_ProjectCivilExtensionOfTimeControls_Variation", "([HasCostImpact] = 0) OR ([QuantitySurveyVariationOrderId] IS NOT NULL)");
            });
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ExtensionOfTime).WithOne().HasForeignKey<ProjectCivilExtensionOfTimeControl>(item => item.ProjectExtensionOfTimeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Contract).WithMany().HasForeignKey(item => item.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.QuantitySurveyVariationOrder).WithMany().HasForeignKey(item => item.QuantitySurveyVariationOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.EvidenceDocumentRecord).WithMany().HasForeignKey(item => item.EvidenceDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.EvidenceDocumentVersion).WithMany().HasForeignKey(item => item.EvidenceDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationProfile).WithMany().HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ConfigurationDecision).WithMany().HasForeignKey(item => item.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowInstance>().WithMany().HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CentralDocumentMetadataTemplate>().WithMany().HasForeignKey(item => item.EvidenceMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.SubmittedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectCivilExtensionOfTimeRevision>(entity =>
        {
            entity.Property(item => item.RequestHash).IsUnicode(false);
            entity.HasIndex(item => new { item.TenantId, item.ExtensionOfTimeControlId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProjectCivilExtensionOfTimeRevisions_Lineage");
                table.HasTrigger("TR_ProjectCivilExtensionOfTimeRevisions_AppendOnly");
            });
            entity.HasOne(item => item.ExtensionOfTimeControl).WithMany(item => item.Revisions).HasForeignKey(item => item.ExtensionOfTimeControlId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
