using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Civil-only status linkage for approved maintenance scopes. The referenced QS, budget,
/// procurement and contract records remain owned and mutated by their existing modules.
/// </summary>
public partial class AddCivilEngineeringMaintenanceCostingHandoffs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE CivilEngineeringMaintenanceCostingHandoffs (
                Id uniqueidentifier NOT NULL PRIMARY KEY, AssessmentId uniqueidentifier NOT NULL, ProjectId uniqueidentifier NOT NULL,
                QuantitySurveyEstimateVersionId uniqueidentifier NOT NULL, ProjectBudgetRevisionId uniqueidentifier NULL,
                PurchaseRequisitionId uniqueidentifier NULL, ContractId uniqueidentifier NULL,
                ConfigurationProfileId uniqueidentifier NOT NULL, ConfigurationDecisionId uniqueidentifier NOT NULL,
                CostingWorkflowDefinitionId uniqueidentifier NOT NULL, ContractorEngagementWorkflowDefinitionId uniqueidentifier NOT NULL,
                WorkflowInstanceId uniqueidentifier NULL, PolicyHash varchar(64) NOT NULL,
                Stage varchar(40) NOT NULL, Status varchar(30) NOT NULL, ApprovalStatus varchar(30) NOT NULL,
                ApprovedById uniqueidentifier NULL, ApprovedAt datetime2 NULL, RejectionReason nvarchar(2000) NULL,
                LastRevalidatedAt datetime2 NULL, LastRevalidationSummary nvarchar(1000) NULL,
                ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL,
                LastMutationClientRequestId uniqueidentifier NULL, LastMutationRequestHash varchar(64) NULL,
                CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL,
                DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_CivilEngineeringMaintenanceCostingHandoffs_Assessment FOREIGN KEY (AssessmentId) REFERENCES CivilEngineeringMaintenanceAssessments(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCostingHandoffs_Project FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCostingHandoffs_Estimate FOREIGN KEY (QuantitySurveyEstimateVersionId) REFERENCES QuantitySurveyEstimateVersions(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCostingHandoffs_ProjectBudget FOREIGN KEY (ProjectBudgetRevisionId) REFERENCES ProjectBudgetRevisions(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCostingHandoffs_Requisition FOREIGN KEY (PurchaseRequisitionId) REFERENCES PurchaseRequisitions(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCostingHandoffs_Contract FOREIGN KEY (ContractId) REFERENCES Contracts(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCostingHandoffs_Profile FOREIGN KEY (ConfigurationProfileId) REFERENCES CivilEngineeringConfigurationProfiles(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCostingHandoffs_Decision FOREIGN KEY (ConfigurationDecisionId) REFERENCES CivilEngineeringConfigurationDecisions(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCostingHandoffs_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE UNIQUE INDEX UX_CivilEngineeringMaintenanceCostingHandoffs_Tenant_Assessment ON CivilEngineeringMaintenanceCostingHandoffs(TenantId, AssessmentId);
            CREATE UNIQUE INDEX UX_CivilEngineeringMaintenanceCostingHandoffs_Tenant_ClientRequest ON CivilEngineeringMaintenanceCostingHandoffs(TenantId, ClientRequestId);
            CREATE INDEX IX_CivilEngineeringMaintenanceCostingHandoffs_Tenant_Project_Stage ON CivilEngineeringMaintenanceCostingHandoffs(TenantId, ProjectId, Stage);
            """);
        migrationBuilder.Sql("""
            CREATE TABLE CivilEngineeringMaintenanceCostingHandoffRevisions (
                Id uniqueidentifier NOT NULL PRIMARY KEY, HandoffId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL,
                FromStage varchar(40) NULL, ToStage varchar(40) NOT NULL, ActorUserId uniqueidentifier NOT NULL,
                ActorName nvarchar(300) NOT NULL, ActorRoles nvarchar(500) NULL, Reason nvarchar(2000) NULL,
                CorrelationId nvarchar(100) NOT NULL, BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL,
                DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_CivilEngineeringMaintenanceCostingHandoffRevisions_Handoff FOREIGN KEY (HandoffId) REFERENCES CivilEngineeringMaintenanceCostingHandoffs(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCostingHandoffRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCostingHandoffRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_CivilEngineeringMaintenanceCostingHandoffRevisions_Tenant_Handoff_CreatedAt ON CivilEngineeringMaintenanceCostingHandoffRevisions(TenantId, HandoffId, CreatedAt);
            CREATE INDEX IX_CivilEngineeringMaintenanceCostingHandoffRevisions_Tenant_Correlation ON CivilEngineeringMaintenanceCostingHandoffRevisions(TenantId, CorrelationId);
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceCostingHandoffs_Lineage ON CivilEngineeringMaintenanceCostingHandoffs AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN CivilEngineeringMaintenanceAssessments assessment ON assessment.Id=value.AssessmentId AND assessment.TenantId=value.TenantId AND assessment.IsDeleted=0
                LEFT JOIN CivilEngineeringMaintenanceIntakes intake ON intake.Id=assessment.IntakeId AND intake.TenantId=value.TenantId AND intake.IsDeleted=0
                LEFT JOIN Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN QuantitySurveyEstimateVersions estimate ON estimate.Id=value.QuantitySurveyEstimateVersionId AND estimate.TenantId=value.TenantId AND estimate.ProjectId=value.ProjectId AND estimate.IsDeleted=0
                LEFT JOIN ProjectBudgetRevisions budget ON budget.Id=value.ProjectBudgetRevisionId AND budget.TenantId=value.TenantId AND budget.ProjectId=value.ProjectId AND budget.IsDeleted=0
                LEFT JOIN PurchaseRequisitions requisition ON requisition.Id=value.PurchaseRequisitionId AND requisition.TenantId=value.TenantId AND requisition.ProjectId=value.ProjectId AND requisition.IsDeleted=0
                LEFT JOIN Contracts contract ON contract.Id=value.ContractId AND contract.TenantId=value.TenantId AND contract.IsDeleted=0
                LEFT JOIN CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0
                LEFT JOIN CivilEngineeringConfigurationDecisions decision ON decision.Id=value.ConfigurationDecisionId AND decision.ProfileId=value.ConfigurationProfileId AND decision.TenantId=value.TenantId AND decision.ConfigurationKey='CIV-CFG-008' AND decision.IsDeleted=0
                LEFT JOIN WorkflowDefinitions costing ON costing.Id=value.CostingWorkflowDefinitionId AND costing.TenantId=value.TenantId AND costing.IsDeleted=0 AND costing.IsActive=1 AND costing.LifecycleStatus=1
                LEFT JOIN WorkflowDefinitions contractor ON contractor.Id=value.ContractorEngagementWorkflowDefinitionId AND contractor.TenantId=value.TenantId AND contractor.IsDeleted=0 AND contractor.IsActive=1 AND contractor.LifecycleStatus=1
                WHERE assessment.Id IS NULL OR intake.Id IS NULL OR project.Id IS NULL OR estimate.Id IS NULL OR profile.Id IS NULL OR decision.Id IS NULL OR costing.Id IS NULL OR contractor.Id IS NULL
                  OR (value.ProjectBudgetRevisionId IS NOT NULL AND budget.Id IS NULL)
                  OR (value.PurchaseRequisitionId IS NOT NULL AND requisition.Id IS NULL)
                  OR (value.ContractId IS NOT NULL AND (contract.Id IS NULL OR ISNULL(CONVERT(varchar(36), project.ContractId), '')<>CONVERT(varchar(36), contract.Id)))
                  OR (intake.ProjectId IS NOT NULL AND intake.ProjectId<>value.ProjectId)
                  OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND (assessment.Stage<>'Approved' OR assessment.Status<>'Approved' OR value.Stage<>'Draft' OR value.Status<>'Draft' OR value.ApprovalStatus<>'Draft' OR value.WorkflowInstanceId IS NOT NULL))
              ) THROW 52220, 'Civil costing handoff tenant, approved assessment, cross-owner reference, configuration, workflow, or initial lifecycle lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceCostingHandoffs_Lifecycle ON CivilEngineeringMaintenanceCostingHandoffs AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id)) THROW 52221, 'Civil costing handoff records cannot be deleted.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.TenantId<>prior.TenantId OR value.AssessmentId<>prior.AssessmentId OR value.ProjectId<>prior.ProjectId OR value.QuantitySurveyEstimateVersionId<>prior.QuantitySurveyEstimateVersionId OR ISNULL(CONVERT(varchar(36),value.ProjectBudgetRevisionId),'')<>ISNULL(CONVERT(varchar(36),prior.ProjectBudgetRevisionId),'') OR ISNULL(CONVERT(varchar(36),value.PurchaseRequisitionId),'')<>ISNULL(CONVERT(varchar(36),prior.PurchaseRequisitionId),'') OR ISNULL(CONVERT(varchar(36),value.ContractId),'')<>ISNULL(CONVERT(varchar(36),prior.ContractId),'') OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.ConfigurationDecisionId<>prior.ConfigurationDecisionId OR value.CostingWorkflowDefinitionId<>prior.CostingWorkflowDefinitionId OR value.ContractorEngagementWorkflowDefinitionId<>prior.ContractorEngagementWorkflowDefinitionId OR value.PolicyHash<>prior.PolicyHash OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted) THROW 52222, 'Civil costing handoff identity and frozen owner/configuration lineage are immutable.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE NOT ((prior.Stage='Draft' AND value.Stage IN ('CostingReview','ProcurementAndAward')) OR (prior.Stage='CostingReview' AND value.Stage IN ('ProcurementAndAward','Rejected')) OR (prior.Stage='ProcurementAndAward' AND value.Stage='Awarded') OR value.Stage=prior.Stage)) THROW 52223, 'Invalid Civil costing handoff lifecycle transition.', 1;
              IF EXISTS (SELECT 1 FROM inserted value WHERE (value.Stage='Draft' AND (value.Status<>'Draft' OR value.ApprovalStatus<>'Draft' OR value.WorkflowInstanceId IS NOT NULL)) OR (value.Stage='CostingReview' AND (value.Status<>'PendingApproval' OR value.ApprovalStatus<>'Pending' OR value.WorkflowInstanceId IS NULL)) OR (value.Stage='ProcurementAndAward' AND (value.Status<>'Approved' OR value.ApprovalStatus<>'Approved' OR value.WorkflowInstanceId IS NULL)) OR (value.Stage='Awarded' AND (value.Status<>'Awarded' OR value.ApprovalStatus<>'Approved' OR value.WorkflowInstanceId IS NULL)) OR (value.Stage='Rejected' AND (value.Status<>'Rejected' OR value.ApprovalStatus<>'Rejected' OR value.WorkflowInstanceId IS NULL))) THROW 52224, 'Civil costing handoff stage and workflow approval status are inconsistent.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.Stage=prior.Stage AND (ISNULL(CONVERT(varchar(36),value.WorkflowInstanceId),'')<>ISNULL(CONVERT(varchar(36),prior.WorkflowInstanceId),'') OR value.Status<>prior.Status OR value.ApprovalStatus<>prior.ApprovalStatus OR ISNULL(CONVERT(varchar(36),value.ApprovedById),'')<>ISNULL(CONVERT(varchar(36),prior.ApprovedById),'') OR ISNULL(CONVERT(varchar(33),value.ApprovedAt,126),'')<>ISNULL(CONVERT(varchar(33),prior.ApprovedAt,126),'') OR ISNULL(value.RejectionReason,'')<>ISNULL(prior.RejectionReason,'') OR value.CreatedAt<>prior.CreatedAt OR ISNULL(value.CreatedBy,'')<>ISNULL(prior.CreatedBy,'') OR ISNULL(CONVERT(varchar(36),value.CreatedById),'')<>ISNULL(CONVERT(varchar(36),prior.CreatedById),''))) THROW 52225, 'Civil costing handoff content may change only through an allowed lifecycle transition.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceCostingHandoffRevisions_Lineage ON CivilEngineeringMaintenanceCostingHandoffRevisions AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN CivilEngineeringMaintenanceCostingHandoffs handoff ON handoff.Id=value.HandoffId AND handoff.TenantId=value.TenantId LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1 WHERE handoff.Id IS NULL OR actor.Id IS NULL) THROW 52226, 'Civil costing handoff revision lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceCostingHandoffRevisions_AppendOnly ON CivilEngineeringMaintenanceCostingHandoffRevisions AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON; THROW 52227, 'Civil costing handoff revisions are append-only.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceCostingHandoffRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceCostingHandoffRevisions_Lineage;
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceCostingHandoffs_Lifecycle;
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceCostingHandoffs_Lineage;
            DROP TABLE IF EXISTS CivilEngineeringMaintenanceCostingHandoffRevisions;
            DROP TABLE IF EXISTS CivilEngineeringMaintenanceCostingHandoffs;
            """);
    }
}
