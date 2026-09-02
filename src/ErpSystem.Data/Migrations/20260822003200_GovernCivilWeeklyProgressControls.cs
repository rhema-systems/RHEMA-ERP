using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Extends the existing Projects-owned Civil weekly-supervision report with governed
/// milestone, recovery-action and management-correction lineage. Historical reports remain
/// immutable and readable; only new reports opt into the governed progress-control envelope.
/// </summary>
public partial class GovernCivilWeeklyProgressControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE dbo.ProjectCivilWeeklySupervisionReports ADD
              ProjectMilestoneId uniqueidentifier NULL,
              ProjectRiskId uniqueidentifier NULL,
              ProjectIssueId uniqueidentifier NULL,
              ProjectTaskDependencyId uniqueidentifier NULL,
              SiteStatus varchar(30) NOT NULL CONSTRAINT DF_ProjectCivilWeeklySupervisionReports_SiteStatus DEFAULT 'OnTrack',
              DelayReason nvarchar(2000) NULL,
              RecoveryActionItemId uniqueidentifier NULL,
              IsProgressCorrection bit NOT NULL CONSTRAINT DF_ProjectCivilWeeklySupervisionReports_IsProgressCorrection DEFAULT 0,
              ProgressCorrectionDecisionId uniqueidentifier NULL,
              HasGovernedProgressControl bit NOT NULL CONSTRAINT DF_ProjectCivilWeeklySupervisionReports_HasGovernedProgressControl DEFAULT 0;

            ALTER TABLE dbo.ProjectCivilWeeklySupervisionReports ADD
              CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Milestone FOREIGN KEY (ProjectMilestoneId) REFERENCES dbo.ProjectMilestones(Id),
              CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Risk FOREIGN KEY (ProjectRiskId) REFERENCES dbo.ProjectRisks(Id),
              CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Issue FOREIGN KEY (ProjectIssueId) REFERENCES dbo.ProjectIssues(Id),
              CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Dependency FOREIGN KEY (ProjectTaskDependencyId) REFERENCES dbo.ProjectTaskDependencies(Id),
              CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_RecoveryAction FOREIGN KEY (RecoveryActionItemId) REFERENCES dbo.ProjectActionItems(Id),
              CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_ProgressCorrectionDecision FOREIGN KEY (ProgressCorrectionDecisionId) REFERENCES dbo.ProjectDecisions(Id),
              CONSTRAINT CK_ProjectCivilWeeklySupervisionReports_ProgressControl CHECK
              ([HasGovernedProgressControl] = 0 OR
                ([ProjectMilestoneId] IS NOT NULL AND [OverallProgressPercent] IS NOT NULL
                 AND [SiteStatus] IN ('OnTrack','AtRisk','Delayed','Stopped')
                 AND (([SiteStatus] = 'OnTrack' AND [DelayReason] IS NULL AND [RecoveryActionItemId] IS NULL)
                   OR ([SiteStatus] IN ('AtRisk','Delayed','Stopped')
                       AND LEN(LTRIM(RTRIM(ISNULL([DelayReason],'')))) >= 3 AND [RecoveryActionItemId] IS NOT NULL))
                 AND (([IsProgressCorrection] = 0 AND [ProgressCorrectionDecisionId] IS NULL)
                   OR ([IsProgressCorrection] = 1 AND (([Status] <> 'Approved' AND [ProgressCorrectionDecisionId] IS NULL)
                     OR ([Status] = 'Approved' AND [ProgressCorrectionDecisionId] IS NOT NULL))))));
            CREATE INDEX IX_ProjectCivilWeeklySupervisionReports_TenantId_ProjectId_SiteStatus_WeekStart
              ON dbo.ProjectCivilWeeklySupervisionReports(TenantId, ProjectId, SiteStatus, WeekStart);
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilWeeklySupervisionReports_Lineage ON dbo.ProjectCivilWeeklySupervisionReports AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN dbo.Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN dbo.ProjectCivilProjectEngineerAssignments assignment ON assignment.Id=value.ProjectEngineerAssignmentId AND assignment.ProjectId=value.ProjectId AND assignment.TenantId=value.TenantId AND assignment.IsDeleted=0
                LEFT JOIN dbo.CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0
                LEFT JOIN dbo.CivilEngineeringConfigurationDecisions decision ON decision.Id=value.ConfigurationDecisionId AND decision.ProfileId=value.ConfigurationProfileId AND decision.TenantId=value.TenantId AND decision.ConfigurationKey='CIV-CFG-006' AND decision.IsDeleted=0
                LEFT JOIN dbo.WorkflowDefinitions workflow ON workflow.Id=value.WorkflowDefinitionId AND workflow.TenantId=value.TenantId AND workflow.IsDeleted=0
                LEFT JOIN dbo.CentralDocumentMetadataTemplates template ON template.Id=value.EvidenceMetadataTemplateId AND template.TenantId=value.TenantId AND template.IsDeleted=0
                LEFT JOIN dbo.ProjectMilestones milestone ON milestone.Id=value.ProjectMilestoneId AND milestone.ProjectId=value.ProjectId AND milestone.TenantId=value.TenantId AND milestone.IsDeleted=0
                LEFT JOIN dbo.ProjectRisks risk ON risk.Id=value.ProjectRiskId AND risk.ProjectId=value.ProjectId AND risk.TenantId=value.TenantId AND risk.IsDeleted=0
                LEFT JOIN dbo.ProjectIssues issue ON issue.Id=value.ProjectIssueId AND issue.ProjectId=value.ProjectId AND issue.TenantId=value.TenantId AND issue.IsDeleted=0
                LEFT JOIN dbo.ProjectTaskDependencies dependency ON dependency.Id=value.ProjectTaskDependencyId AND dependency.ProjectId=value.ProjectId AND dependency.TenantId=value.TenantId AND dependency.IsDeleted=0
                LEFT JOIN dbo.ProjectActionItems recovery ON recovery.Id=value.RecoveryActionItemId AND recovery.ProjectId=value.ProjectId AND recovery.TenantId=value.TenantId AND recovery.IsDeleted=0
                LEFT JOIN dbo.ProjectDecisions correction ON correction.Id=value.ProgressCorrectionDecisionId AND correction.ProjectId=value.ProjectId AND correction.TenantId=value.TenantId AND correction.IsDeleted=0
                WHERE project.Id IS NULL OR assignment.Id IS NULL OR profile.Id IS NULL OR decision.Id IS NULL OR workflow.Id IS NULL OR template.Id IS NULL
                   OR value.EvidenceMetadataTemplateCodeSnapshot<>template.TemplateCode
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND
                     (assignment.IsActive<>1 OR profile.LifecycleStatus<>1 OR decision.Status<>2 OR decision.ApprovalStatus<>1 OR decision.EvidenceStatus<>2
                      OR workflow.IsActive<>1 OR workflow.LifecycleStatus<>1 OR template.IsActive<>1 OR template.PublishedAt IS NULL
                      OR NOT EXISTS (SELECT 1 FROM dbo.WorkflowEntityTypes entityType WHERE entityType.Id=workflow.EntityTypeId AND entityType.TenantId=value.TenantId AND entityType.IsActive=1 AND entityType.IsDeleted=0 AND entityType.Code='PROJECT_WEEKLY_REPORT')
                      OR (SELECT COUNT(*) FROM dbo.WorkflowSteps step WHERE step.WorkflowDefinitionId=workflow.Id AND step.TenantId=value.TenantId AND step.IsDeleted=0)<2))
                   OR (value.HasGovernedProgressControl=1 AND
                     (milestone.Id IS NULL
                      OR (value.ProjectRiskId IS NOT NULL AND (risk.Id IS NULL OR risk.Status IN ('Closed','Resolved')))
                      OR (value.ProjectIssueId IS NOT NULL AND (issue.Id IS NULL OR issue.Status IN ('Closed','Resolved')))
                      OR (value.ProjectTaskDependencyId IS NOT NULL AND dependency.Id IS NULL)
                      OR (value.RecoveryActionItemId IS NOT NULL AND recovery.Id IS NULL)
                      OR (value.ProgressCorrectionDecisionId IS NOT NULL AND correction.Id IS NULL)
                      OR (value.IsProgressCorrection=1 AND value.Status='Approved' AND (correction.Id IS NULL OR correction.Status<>'Approved' OR correction.ApprovedAt IS NULL))))
              ) THROW 52143, 'Civil weekly-supervision governed progress, recovery-action, management-decision, or tenant lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilWeeklySupervisionReports_Lifecycle ON dbo.ProjectCivilWeeklySupervisionReports AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id)) THROW 52131, 'Civil weekly-supervision reports cannot be deleted.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                WHERE value.TenantId<>prior.TenantId OR value.ProjectId<>prior.ProjectId OR value.ProjectEngineerAssignmentId<>prior.ProjectEngineerAssignmentId
                   OR ISNULL(value.ProjectMilestoneId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ProjectMilestoneId,'00000000-0000-0000-0000-000000000000')
                   OR ISNULL(value.ProjectRiskId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ProjectRiskId,'00000000-0000-0000-0000-000000000000')
                   OR ISNULL(value.ProjectIssueId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ProjectIssueId,'00000000-0000-0000-0000-000000000000')
                   OR ISNULL(value.ProjectTaskDependencyId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ProjectTaskDependencyId,'00000000-0000-0000-0000-000000000000')
                   OR value.HasGovernedProgressControl<>prior.HasGovernedProgressControl OR value.SiteStatus<>prior.SiteStatus OR ISNULL(value.DelayReason,'')<>ISNULL(prior.DelayReason,'')
                   OR ISNULL(value.RecoveryActionItemId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.RecoveryActionItemId,'00000000-0000-0000-0000-000000000000')
                   OR value.IsProgressCorrection<>prior.IsProgressCorrection OR value.WeekStart<>prior.WeekStart OR value.WeekEnd<>prior.WeekEnd
                   OR ISNULL(value.OverallProgressPercent,-1)<>ISNULL(prior.OverallProgressPercent,-1) OR ISNULL(value.MaterialUsageSummary,'')<>ISNULL(prior.MaterialUsageSummary,'')
                   OR ISNULL(value.SafetyNotes,'')<>ISNULL(prior.SafetyNotes,'') OR ISNULL(value.TestSummary,'')<>ISNULL(prior.TestSummary,'') OR value.DueAt<>prior.DueAt
                   OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.ConfigurationDecisionId<>prior.ConfigurationDecisionId OR value.WorkflowDefinitionId<>prior.WorkflowDefinitionId
                   OR value.EvidenceMetadataTemplateId<>prior.EvidenceMetadataTemplateId OR value.EvidenceMetadataTemplateCodeSnapshot<>prior.EvidenceMetadataTemplateCodeSnapshot
                   OR ISNULL(value.WorkflowInstanceId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.WorkflowInstanceId,'00000000-0000-0000-0000-000000000000')
                   OR value.PolicyHash<>prior.PolicyHash OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted
              ) THROW 52132, 'Civil weekly-supervision identity, governed progress and frozen policy lineage are immutable.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                WHERE ISNULL(value.ProgressCorrectionDecisionId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ProgressCorrectionDecisionId,'00000000-0000-0000-0000-000000000000')
                  AND NOT (prior.ProgressCorrectionDecisionId IS NULL AND value.ProgressCorrectionDecisionId IS NOT NULL AND value.HasGovernedProgressControl=1
                           AND value.IsProgressCorrection=1 AND value.Status='Approved' AND value.ApprovalStatus='Approved')
              ) THROW 52144, 'A Civil weekly progress-correction decision can be appended only once with final approval.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE NOT ((prior.Status='PendingApproval' AND prior.ApprovalStatus='Pending' AND ((value.Status='PendingApproval' AND value.ApprovalStatus='Pending' AND value.ApprovedById IS NULL AND value.ApprovedAt IS NULL AND value.RejectionReason IS NULL) OR (value.Status='Approved' AND value.ApprovalStatus='Approved' AND value.ApprovedById IS NOT NULL AND value.ApprovedAt IS NOT NULL AND value.RejectionReason IS NULL) OR (value.Status='Rejected' AND value.ApprovalStatus='Rejected' AND value.ApprovedById IS NULL AND value.ApprovedAt IS NULL AND value.RejectionReason IS NOT NULL AND LEN(LTRIM(RTRIM(value.RejectionReason)))>0))) OR (value.Status=prior.Status AND value.ApprovalStatus=prior.ApprovalStatus AND ISNULL(value.ApprovedById,'00000000-0000-0000-0000-000000000000')=ISNULL(prior.ApprovedById,'00000000-0000-0000-0000-000000000000') AND ISNULL(value.ApprovedAt,'19000101')=ISNULL(prior.ApprovedAt,'19000101') AND ISNULL(value.RejectionReason,'')=ISNULL(prior.RejectionReason,'')))) THROW 52133, 'Invalid Civil weekly-supervision lifecycle transition.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE NOT ((prior.EscalatedAt IS NULL AND value.EscalatedAt IS NULL) OR (prior.EscalatedAt IS NULL AND value.EscalatedAt IS NOT NULL AND value.Status='PendingApproval' AND value.ApprovalStatus='Pending') OR (prior.EscalatedAt=value.EscalatedAt))) THROW 52134, 'Civil weekly-supervision overdue escalation is immutable once queued.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER dbo.TR_ProjectCivilWeeklySupervisionReports_Lineage;
            DROP TRIGGER dbo.TR_ProjectCivilWeeklySupervisionReports_Lifecycle;
            ALTER TABLE dbo.ProjectCivilWeeklySupervisionReports DROP CONSTRAINT CK_ProjectCivilWeeklySupervisionReports_ProgressControl;
            ALTER TABLE dbo.ProjectCivilWeeklySupervisionReports DROP CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Milestone;
            ALTER TABLE dbo.ProjectCivilWeeklySupervisionReports DROP CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Risk;
            ALTER TABLE dbo.ProjectCivilWeeklySupervisionReports DROP CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Issue;
            ALTER TABLE dbo.ProjectCivilWeeklySupervisionReports DROP CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Dependency;
            ALTER TABLE dbo.ProjectCivilWeeklySupervisionReports DROP CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_RecoveryAction;
            ALTER TABLE dbo.ProjectCivilWeeklySupervisionReports DROP CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_ProgressCorrectionDecision;
            DROP INDEX IX_ProjectCivilWeeklySupervisionReports_TenantId_ProjectId_SiteStatus_WeekStart ON dbo.ProjectCivilWeeklySupervisionReports;
            ALTER TABLE dbo.ProjectCivilWeeklySupervisionReports DROP CONSTRAINT DF_ProjectCivilWeeklySupervisionReports_SiteStatus;
            ALTER TABLE dbo.ProjectCivilWeeklySupervisionReports DROP CONSTRAINT DF_ProjectCivilWeeklySupervisionReports_IsProgressCorrection;
            ALTER TABLE dbo.ProjectCivilWeeklySupervisionReports DROP CONSTRAINT DF_ProjectCivilWeeklySupervisionReports_HasGovernedProgressControl;
            ALTER TABLE dbo.ProjectCivilWeeklySupervisionReports DROP COLUMN ProjectMilestoneId, ProjectRiskId, ProjectIssueId, ProjectTaskDependencyId, SiteStatus, DelayReason, RecoveryActionItemId, IsProgressCorrection, ProgressCorrectionDecisionId, HasGovernedProgressControl;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilWeeklySupervisionReports_Lineage ON dbo.ProjectCivilWeeklySupervisionReports AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value
                LEFT JOIN dbo.Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN dbo.ProjectCivilProjectEngineerAssignments assignment ON assignment.Id=value.ProjectEngineerAssignmentId AND assignment.ProjectId=value.ProjectId AND assignment.TenantId=value.TenantId AND assignment.IsDeleted=0
                LEFT JOIN dbo.CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0
                LEFT JOIN dbo.CivilEngineeringConfigurationDecisions decision ON decision.Id=value.ConfigurationDecisionId AND decision.ProfileId=value.ConfigurationProfileId AND decision.TenantId=value.TenantId AND decision.ConfigurationKey='CIV-CFG-006' AND decision.IsDeleted=0
                LEFT JOIN dbo.WorkflowDefinitions workflow ON workflow.Id=value.WorkflowDefinitionId AND workflow.TenantId=value.TenantId AND workflow.IsDeleted=0
                LEFT JOIN dbo.CentralDocumentMetadataTemplates template ON template.Id=value.EvidenceMetadataTemplateId AND template.TenantId=value.TenantId AND template.IsDeleted=0
                WHERE project.Id IS NULL OR assignment.Id IS NULL OR profile.Id IS NULL OR decision.Id IS NULL OR workflow.Id IS NULL OR template.Id IS NULL OR value.EvidenceMetadataTemplateCodeSnapshot<>template.TemplateCode
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND (assignment.IsActive<>1 OR profile.LifecycleStatus<>1 OR decision.Status<>2 OR decision.ApprovalStatus<>1 OR decision.EvidenceStatus<>2 OR workflow.IsActive<>1 OR workflow.LifecycleStatus<>1 OR template.IsActive<>1 OR template.PublishedAt IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.WorkflowEntityTypes entityType WHERE entityType.Id=workflow.EntityTypeId AND entityType.TenantId=value.TenantId AND entityType.IsActive=1 AND entityType.IsDeleted=0 AND entityType.Code='PROJECT_WEEKLY_REPORT') OR (SELECT COUNT(*) FROM dbo.WorkflowSteps step WHERE step.WorkflowDefinitionId=workflow.Id AND step.TenantId=value.TenantId AND step.IsDeleted=0)<2))
              ) THROW 52130, 'Civil weekly-supervision tenant, Project Engineer, workflow, template, or CIV-CFG-006 lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilWeeklySupervisionReports_Lifecycle ON dbo.ProjectCivilWeeklySupervisionReports AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id)) THROW 52131, 'Civil weekly-supervision reports cannot be deleted.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.TenantId<>prior.TenantId OR value.ProjectId<>prior.ProjectId OR value.ProjectEngineerAssignmentId<>prior.ProjectEngineerAssignmentId OR value.WeekStart<>prior.WeekStart OR value.WeekEnd<>prior.WeekEnd OR ISNULL(value.OverallProgressPercent,-1)<>ISNULL(prior.OverallProgressPercent,-1) OR ISNULL(value.MaterialUsageSummary,'')<>ISNULL(prior.MaterialUsageSummary,'') OR ISNULL(value.SafetyNotes,'')<>ISNULL(prior.SafetyNotes,'') OR ISNULL(value.TestSummary,'')<>ISNULL(prior.TestSummary,'') OR value.DueAt<>prior.DueAt OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.ConfigurationDecisionId<>prior.ConfigurationDecisionId OR value.WorkflowDefinitionId<>prior.WorkflowDefinitionId OR value.EvidenceMetadataTemplateId<>prior.EvidenceMetadataTemplateId OR value.EvidenceMetadataTemplateCodeSnapshot<>prior.EvidenceMetadataTemplateCodeSnapshot OR ISNULL(value.WorkflowInstanceId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.WorkflowInstanceId,'00000000-0000-0000-0000-000000000000') OR value.PolicyHash<>prior.PolicyHash OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted) THROW 52132, 'Civil weekly-supervision identity and frozen policy lineage are immutable.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE NOT ((prior.Status='PendingApproval' AND prior.ApprovalStatus='Pending' AND ((value.Status='PendingApproval' AND value.ApprovalStatus='Pending' AND value.ApprovedById IS NULL AND value.ApprovedAt IS NULL AND value.RejectionReason IS NULL) OR (value.Status='Approved' AND value.ApprovalStatus='Approved' AND value.ApprovedById IS NOT NULL AND value.ApprovedAt IS NOT NULL AND value.RejectionReason IS NULL) OR (value.Status='Rejected' AND value.ApprovalStatus='Rejected' AND value.ApprovedById IS NULL AND value.ApprovedAt IS NULL AND value.RejectionReason IS NOT NULL AND LEN(LTRIM(RTRIM(value.RejectionReason)))>0))) OR (value.Status=prior.Status AND value.ApprovalStatus=prior.ApprovalStatus AND ISNULL(value.ApprovedById,'00000000-0000-0000-0000-000000000000')=ISNULL(prior.ApprovedById,'00000000-0000-0000-0000-000000000000') AND ISNULL(value.ApprovedAt,'19000101')=ISNULL(prior.ApprovedAt,'19000101') AND ISNULL(value.RejectionReason,'')=ISNULL(prior.RejectionReason,'')))) THROW 52133, 'Invalid Civil weekly-supervision lifecycle transition.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE NOT ((prior.EscalatedAt IS NULL AND value.EscalatedAt IS NULL) OR (prior.EscalatedAt IS NULL AND value.EscalatedAt IS NOT NULL AND value.Status='PendingApproval' AND value.ApprovalStatus='Pending') OR (prior.EscalatedAt=value.EscalatedAt))) THROW 52134, 'Civil weekly-supervision overdue escalation is immutable once queued.', 1;
            END;
            """);
    }
}
