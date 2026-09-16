using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260722074659_AddProcurementRequisitionSourcingReleases")]
public partial class AddProcurementRequisitionSourcingReleases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE [RequestForQuotations] DROP CONSTRAINT [FK_RequestForQuotations_PurchaseRequisitions_SourcePurchaseRequisitionId];");
        migrationBuilder.Sql("ALTER TABLE [Tenders] ADD [SourcePurchaseRequisitionId] uniqueidentifier NULL;");
        migrationBuilder.Sql("ALTER TABLE [Tenders] ADD [SourcingReleaseId] uniqueidentifier NULL;");
        migrationBuilder.Sql("ALTER TABLE [RequestForQuotations] ADD [SourcingReleaseId] uniqueidentifier NULL;");
        migrationBuilder.Sql("""
            CREATE TABLE [ProcurementRequisitionSourcingReleases] (
                [Id] uniqueidentifier NOT NULL,
                [PurchaseRequisitionId] uniqueidentifier NOT NULL,
                [AttemptNumber] int NOT NULL,
                [ReleaseReference] varchar(100) NOT NULL,
                [SourcePlanId] uniqueidentifier NOT NULL,
                [SourcePlanItemId] uniqueidentifier NOT NULL,
                [AppSubmissionId] uniqueidentifier NULL,
                [AppSubmissionAttemptNumber] int NULL,
                [AppAcknowledgementReference] nvarchar(100) NULL,
                [ApprovedExceptionRuleId] uniqueidentifier NULL,
                [ExceptionWorkflowInstanceId] uniqueidentifier NULL,
                [ExceptionApprovalReference] nvarchar(200) NULL,
                [SpecificationTemplateId] uniqueidentifier NOT NULL,
                [SpecificationTemplateCode] varchar(50) NOT NULL,
                [SpecificationTemplateVersion] int NOT NULL,
                [BudgetCommitmentId] uniqueidentifier NOT NULL,
                [BudgetCommitmentReference] nvarchar(100) NOT NULL,
                [AuthorityRouteId] uniqueidentifier NOT NULL,
                [AuthorityRouteReference] nvarchar(100) NOT NULL,
                [WorkflowInstanceId] uniqueidentifier NOT NULL,
                [ReleasedAtUtc] datetime2 NOT NULL,
                [ReleasedById] uniqueidentifier NOT NULL,
                [ReleasedByName] nvarchar(300) NOT NULL,
                [ReleaseReason] nvarchar(500) NOT NULL,
                [CorrelationId] nvarchar(100) NOT NULL,
                [ControlFingerprint] char(64) NOT NULL,
                [SnapshotJson] nvarchar(max) NOT NULL,
                [IntegrityHash] char(64) NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [UpdatedAt] datetime2 NULL,
                [CreatedBy] nvarchar(max) NULL,
                [UpdatedBy] nvarchar(max) NULL,
                [CreatedById] uniqueidentifier NULL,
                [LastModifiedById] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] nvarchar(max) NULL,
                [TenantId] uniqueidentifier NOT NULL,
                CONSTRAINT [PK_ProcurementRequisitionSourcingReleases] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_ProcurementRequisitionSourcingReleases_Attempt] CHECK ([AttemptNumber] > 0),
                CONSTRAINT [CK_ProcurementRequisitionSourcingReleases_Hashes] CHECK (LEN([ControlFingerprint]) = 64 AND LEN([IntegrityHash]) = 64),
                CONSTRAINT [CK_ProcurementRequisitionSourcingReleases_Lineage] CHECK ([SourcePlanId] <> '00000000-0000-0000-0000-000000000000' AND [SourcePlanItemId] <> '00000000-0000-0000-0000-000000000000' AND [SpecificationTemplateId] <> '00000000-0000-0000-0000-000000000000' AND [BudgetCommitmentId] <> '00000000-0000-0000-0000-000000000000' AND [AuthorityRouteId] <> '00000000-0000-0000-0000-000000000000' AND [WorkflowInstanceId] <> '00000000-0000-0000-0000-000000000000'),
                CONSTRAINT [FK_ProcurementRequisitionSourcingReleases_ProcurementAppSubmissions_AppSubmissionId] FOREIGN KEY ([AppSubmissionId]) REFERENCES [ProcurementAppSubmissions] ([Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_ProcurementRequisitionSourcingReleases_ProcurementBudgetCommitments_BudgetCommitmentId] FOREIGN KEY ([BudgetCommitmentId]) REFERENCES [ProcurementBudgetCommitments] ([Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_ProcurementRequisitionSourcingReleases_ProcurementPolicyExceptionRules_ApprovedExceptionRuleId] FOREIGN KEY ([ApprovedExceptionRuleId]) REFERENCES [ProcurementPolicyExceptionRules] ([Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_ProcurementRequisitionSourcingReleases_ProcurementRequisitionAuthorityRoutes_AuthorityRouteId] FOREIGN KEY ([AuthorityRouteId]) REFERENCES [ProcurementRequisitionAuthorityRoutes] ([Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_ProcurementRequisitionSourcingReleases_ProcurementSpecificationTemplates_SpecificationTemplateId] FOREIGN KEY ([SpecificationTemplateId]) REFERENCES [ProcurementSpecificationTemplates] ([Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_ProcurementRequisitionSourcingReleases_PurchaseRequisitions_PurchaseRequisitionId] FOREIGN KEY ([PurchaseRequisitionId]) REFERENCES [PurchaseRequisitions] ([Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_ProcurementRequisitionSourcingReleases_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_ProcurementRequisitionSourcingReleases_Users_ReleasedById] FOREIGN KEY ([ReleasedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_ProcurementRequisitionSourcingReleases_WorkflowInstances_ExceptionWorkflowInstanceId] FOREIGN KEY ([ExceptionWorkflowInstanceId]) REFERENCES [WorkflowInstances] ([Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_ProcurementRequisitionSourcingReleases_WorkflowInstances_WorkflowInstanceId] FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [WorkflowInstances] ([Id]) ON DELETE NO ACTION
            );
            """);
        migrationBuilder.Sql("CREATE INDEX [IX_Tenders_SourcePurchaseRequisitionId] ON [Tenders] ([SourcePurchaseRequisitionId]);");
        migrationBuilder.Sql("CREATE INDEX [IX_Tenders_SourcingReleaseId] ON [Tenders] ([SourcingReleaseId]);");
        migrationBuilder.Sql("CREATE INDEX [IX_RequestForQuotations_SourcingReleaseId] ON [RequestForQuotations] ([SourcingReleaseId]);");
        migrationBuilder.Sql("CREATE INDEX [IX_ProcurementRequisitionSourcingReleases_ApprovedExceptionRuleId] ON [ProcurementRequisitionSourcingReleases] ([ApprovedExceptionRuleId]);");
        migrationBuilder.Sql("CREATE INDEX [IX_ProcurementRequisitionSourcingReleases_AppSubmissionId] ON [ProcurementRequisitionSourcingReleases] ([AppSubmissionId]);");
        migrationBuilder.Sql("CREATE INDEX [IX_ProcurementRequisitionSourcingReleases_AuthorityRouteId] ON [ProcurementRequisitionSourcingReleases] ([AuthorityRouteId]);");
        migrationBuilder.Sql("CREATE INDEX [IX_ProcurementRequisitionSourcingReleases_BudgetCommitmentId] ON [ProcurementRequisitionSourcingReleases] ([BudgetCommitmentId]);");
        migrationBuilder.Sql("CREATE INDEX [IX_ProcurementRequisitionSourcingReleases_ExceptionWorkflowInstanceId] ON [ProcurementRequisitionSourcingReleases] ([ExceptionWorkflowInstanceId]);");
        migrationBuilder.Sql("CREATE INDEX [IX_ProcurementRequisitionSourcingReleases_PurchaseRequisitionId] ON [ProcurementRequisitionSourcingReleases] ([PurchaseRequisitionId]);");
        migrationBuilder.Sql("CREATE INDEX [IX_ProcurementRequisitionSourcingReleases_ReleasedById] ON [ProcurementRequisitionSourcingReleases] ([ReleasedById]);");
        migrationBuilder.Sql("CREATE INDEX [IX_ProcurementRequisitionSourcingReleases_SpecificationTemplateId] ON [ProcurementRequisitionSourcingReleases] ([SpecificationTemplateId]);");
        migrationBuilder.Sql("CREATE UNIQUE INDEX [IX_ProcurementRequisitionSourcingReleases_TenantId_PurchaseRequisitionId_AttemptNumber] ON [ProcurementRequisitionSourcingReleases] ([TenantId], [PurchaseRequisitionId], [AttemptNumber]);");
        migrationBuilder.Sql("CREATE UNIQUE INDEX [IX_ProcurementRequisitionSourcingReleases_TenantId_PurchaseRequisitionId_ControlFingerprint] ON [ProcurementRequisitionSourcingReleases] ([TenantId], [PurchaseRequisitionId], [ControlFingerprint]);");
        migrationBuilder.Sql("CREATE INDEX [IX_ProcurementRequisitionSourcingReleases_TenantId_ReleasedAtUtc] ON [ProcurementRequisitionSourcingReleases] ([TenantId], [ReleasedAtUtc]);");
        migrationBuilder.Sql("CREATE UNIQUE INDEX [IX_ProcurementRequisitionSourcingReleases_TenantId_ReleaseReference] ON [ProcurementRequisitionSourcingReleases] ([TenantId], [ReleaseReference]);");
        migrationBuilder.Sql("CREATE INDEX [IX_ProcurementRequisitionSourcingReleases_WorkflowInstanceId] ON [ProcurementRequisitionSourcingReleases] ([WorkflowInstanceId]);");
        migrationBuilder.Sql("ALTER TABLE [RequestForQuotations] ADD CONSTRAINT [FK_RequestForQuotations_ProcurementRequisitionSourcingReleases_SourcingReleaseId] FOREIGN KEY ([SourcingReleaseId]) REFERENCES [ProcurementRequisitionSourcingReleases] ([Id]) ON DELETE NO ACTION;");
        migrationBuilder.Sql("ALTER TABLE [RequestForQuotations] ADD CONSTRAINT [FK_RequestForQuotations_PurchaseRequisitions_SourcePurchaseRequisitionId] FOREIGN KEY ([SourcePurchaseRequisitionId]) REFERENCES [PurchaseRequisitions] ([Id]) ON DELETE NO ACTION;");
        migrationBuilder.Sql("ALTER TABLE [Tenders] ADD CONSTRAINT [FK_Tenders_ProcurementRequisitionSourcingReleases_SourcingReleaseId] FOREIGN KEY ([SourcingReleaseId]) REFERENCES [ProcurementRequisitionSourcingReleases] ([Id]) ON DELETE NO ACTION;");
        migrationBuilder.Sql("ALTER TABLE [Tenders] ADD CONSTRAINT [FK_Tenders_PurchaseRequisitions_SourcePurchaseRequisitionId] FOREIGN KEY ([SourcePurchaseRequisitionId]) REFERENCES [PurchaseRequisitions] ([Id]) ON DELETE NO ACTION;");
        migrationBuilder.Sql("""
            CREATE TRIGGER [dbo].[TR_ProcurementRequisitionSourcingReleases_NoMutation]
            ON [dbo].[ProcurementRequisitionSourcingReleases]
            INSTEAD OF UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51040, 'Procurement requisition sourcing releases are immutable.', 1;
            END
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER [dbo].[TR_ProcurementRequisitionSourcingReleases_TenantGuard]
            ON [dbo].[ProcurementRequisitionSourcingReleases]
            AFTER INSERT
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN [dbo].[PurchaseRequisitions] pr ON pr.[Id] = i.[PurchaseRequisitionId]
                    LEFT JOIN [dbo].[ProcurementPlans] pp ON pp.[Id] = i.[SourcePlanId]
                    LEFT JOIN [dbo].[ProcurementPlanItems] pi ON pi.[Id] = i.[SourcePlanItemId]
                    LEFT JOIN [dbo].[ProcurementSpecificationTemplates] st ON st.[Id] = i.[SpecificationTemplateId]
                    LEFT JOIN [dbo].[ProcurementBudgetCommitments] bc ON bc.[Id] = i.[BudgetCommitmentId]
                    LEFT JOIN [dbo].[ProcurementRequisitionAuthorityRoutes] ar ON ar.[Id] = i.[AuthorityRouteId]
                    LEFT JOIN [dbo].[ProcurementPolicySets] ps ON ps.[Id] = ar.[PolicySetId]
                    LEFT JOIN [dbo].[ProcurementConfigurationProfiles] cp ON cp.[Id] = ar.[SourceConfigurationProfileId]
                    LEFT JOIN [dbo].[WorkflowInstances] wi ON wi.[Id] = i.[WorkflowInstanceId]
                    LEFT JOIN [dbo].[ProcurementAppSubmissions] aps ON aps.[Id] = i.[AppSubmissionId]
                    LEFT JOIN [dbo].[ProcurementPolicyExceptionRules] er ON er.[Id] = i.[ApprovedExceptionRuleId]
                    LEFT JOIN [dbo].[WorkflowInstances] ewi ON ewi.[Id] = i.[ExceptionWorkflowInstanceId]
                    WHERE pr.[Id] IS NULL OR pr.[TenantId] <> i.[TenantId] OR pr.[IsDeleted] = 1
                       OR pr.[Status] <> 'Approved' OR pr.[ApprovedAt] IS NULL OR pr.[ApprovedById] IS NULL
                       OR pr.[SourcePlanId] <> i.[SourcePlanId] OR pr.[SourcePlanItemId] <> i.[SourcePlanItemId]
                       OR pr.[SpecificationTemplateId] <> i.[SpecificationTemplateId]
                       OR pr.[SpecificationTemplateCode] <> i.[SpecificationTemplateCode]
                       OR pr.[SpecificationTemplateVersion] <> i.[SpecificationTemplateVersion]
                       OR pp.[Id] IS NULL OR pp.[TenantId] <> i.[TenantId] OR pp.[IsDeleted] = 1
                       OR pi.[Id] IS NULL OR pi.[TenantId] <> i.[TenantId] OR pi.[IsDeleted] = 1 OR pi.[ProcurementPlanId] <> i.[SourcePlanId]
                       OR st.[Id] IS NULL OR st.[TenantId] <> i.[TenantId] OR st.[IsDeleted] = 1
                       OR st.[TemplateCode] <> i.[SpecificationTemplateCode] OR st.[Version] <> i.[SpecificationTemplateVersion]
                       OR st.[Status] NOT IN (2, 3) OR st.[PublishedAtUtc] IS NULL
                       OR bc.[Id] IS NULL OR bc.[TenantId] <> i.[TenantId] OR bc.[IsDeleted] = 1
                       OR bc.[PurchaseRequisitionId] <> i.[PurchaseRequisitionId] OR bc.[Status] <> 1 OR bc.[ReservationReference] <> i.[BudgetCommitmentReference]
                       OR ar.[Id] IS NULL OR ar.[TenantId] <> i.[TenantId] OR ar.[IsDeleted] = 1
                       OR ar.[PurchaseRequisitionId] <> i.[PurchaseRequisitionId] OR ar.[RouteReference] <> i.[AuthorityRouteReference]
                       OR ar.[Amount] <> pr.[TotalAmount] OR ar.[Category] <> pr.[ProcurementCategory]
                       OR ar.[CurrencyCode] <> UPPER(LTRIM(RTRIM(pr.[Currency])))
                       OR ps.[Id] IS NULL OR ps.[TenantId] <> i.[TenantId] OR ps.[IsDeleted] = 1
                       OR ps.[LifecycleStatus] <> 1 OR ps.[PublishedAt] IS NULL OR ps.[EffectiveFrom] > i.[ReleasedAtUtc]
                       OR (ps.[EffectiveTo] IS NOT NULL AND ps.[EffectiveTo] < i.[ReleasedAtUtc])
                       OR ps.[SourceConfigurationProfileId] <> ar.[SourceConfigurationProfileId]
                       OR cp.[Id] IS NULL OR cp.[TenantId] <> i.[TenantId] OR cp.[IsDeleted] = 1
                       OR cp.[LifecycleStatus] <> 1 OR cp.[PublishedAt] IS NULL OR cp.[EffectiveFrom] > i.[ReleasedAtUtc]
                       OR (cp.[EffectiveTo] IS NOT NULL AND cp.[EffectiveTo] < i.[ReleasedAtUtc])
                       OR wi.[Id] IS NULL OR wi.[TenantId] <> i.[TenantId] OR wi.[IsDeleted] = 1
                       OR wi.[EntityId] <> i.[PurchaseRequisitionId] OR wi.[WorkflowDefinitionId] <> ar.[WorkflowDefinitionId]
                       OR wi.[Status] <> 2 OR wi.[CompletedDate] IS NULL OR ISJSON(i.[SnapshotJson]) <> 1
                       OR (i.[AppSubmissionId] IS NULL AND i.[ApprovedExceptionRuleId] IS NULL)
                       OR (i.[AppSubmissionId] IS NOT NULL AND i.[ApprovedExceptionRuleId] IS NOT NULL)
                       OR (i.[AppSubmissionId] IS NOT NULL AND
                           (aps.[Id] IS NULL OR aps.[TenantId] <> i.[TenantId] OR aps.[IsDeleted] = 1
                            OR aps.[ProcurementPlanId] <> i.[SourcePlanId] OR aps.[Status] <> 2
                            OR aps.[AttemptNumber] <> i.[AppSubmissionAttemptNumber]
                            OR aps.[AcknowledgementReference] <> i.[AppAcknowledgementReference] OR aps.[AcknowledgedAtUtc] IS NULL))
                       OR (i.[ApprovedExceptionRuleId] IS NOT NULL AND
                           (er.[Id] IS NULL OR er.[TenantId] <> i.[TenantId] OR er.[IsDeleted] = 1
                            OR pr.[ApprovedExceptionRuleId] <> i.[ApprovedExceptionRuleId]
                            OR pr.[ExceptionWorkflowInstanceId] <> i.[ExceptionWorkflowInstanceId]
                            OR pr.[ExceptionApprovalReference] <> i.[ExceptionApprovalReference]
                            OR pr.[ExceptionApprovedAtUtc] IS NULL OR pr.[ExceptionEvidenceReference] IS NULL
                            OR ewi.[Id] IS NULL OR ewi.[TenantId] <> i.[TenantId] OR ewi.[IsDeleted] = 1
                            OR ewi.[EntityId] <> i.[PurchaseRequisitionId] OR ewi.[Status] <> 2 OR ewi.[CompletedDate] IS NULL))
                       OR EXISTS
                          (
                              SELECT 1
                              FROM [dbo].[WorkflowApprovals] wa
                              INNER JOIN [dbo].[WorkflowStepInstances] wsi ON wsi.[Id] = wa.[StepInstanceId]
                              WHERE wsi.[WorkflowInstanceId] = i.[WorkflowInstanceId]
                                AND wa.[TenantId] = i.[TenantId] AND wa.[IsDeleted] = 0
                                AND wa.[Status] = 1 AND wa.[ProcessedById] = pr.[RequestedById]
                          )
                )
                BEGIN
                    THROW 51041, 'Sourcing release tenant, outcome, or evidence lineage is invalid.', 1;
                END
            END
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER [dbo].[TR_RequestForQuotations_SourcingReleaseGuard]
            ON [dbo].[RequestForQuotations]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    LEFT JOIN [dbo].[ProcurementRequisitionSourcingReleases] sr ON sr.[Id] = i.[SourcingReleaseId]
                    WHERE (i.[SourcePurchaseRequisitionId] IS NULL AND i.[SourcingReleaseId] IS NOT NULL)
                       OR (i.[SourcePurchaseRequisitionId] IS NOT NULL AND i.[SourcingReleaseId] IS NULL)
                       OR (i.[SourcingReleaseId] IS NOT NULL AND
                           (sr.[Id] IS NULL OR sr.[TenantId] <> i.[TenantId] OR sr.[IsDeleted] = 1 OR sr.[PurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId]))
                       OR (d.[Id] IS NOT NULL AND (d.[SourcePurchaseRequisitionId] IS NOT NULL OR d.[SourcingReleaseId] IS NOT NULL) AND
                           (ISNULL(d.[SourcePurchaseRequisitionId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourcePurchaseRequisitionId], '00000000-0000-0000-0000-000000000000')
                            OR ISNULL(d.[SourcingReleaseId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourcingReleaseId], '00000000-0000-0000-0000-000000000000')))
                )
                BEGIN
                    THROW 51042, 'RFQ sourcing requires one immutable same-tenant requisition release.', 1;
                END
            END
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER [dbo].[TR_Tenders_SourcingReleaseGuard]
            ON [dbo].[Tenders]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    LEFT JOIN [dbo].[ProcurementRequisitionSourcingReleases] sr ON sr.[Id] = i.[SourcingReleaseId]
                    WHERE (i.[SourcePurchaseRequisitionId] IS NULL AND i.[SourcingReleaseId] IS NOT NULL)
                       OR (i.[SourcePurchaseRequisitionId] IS NOT NULL AND i.[SourcingReleaseId] IS NULL)
                       OR (i.[SourcingReleaseId] IS NOT NULL AND
                           (sr.[Id] IS NULL OR sr.[TenantId] <> i.[TenantId] OR sr.[IsDeleted] = 1 OR sr.[PurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId]))
                       OR (d.[Id] IS NOT NULL AND (d.[SourcePurchaseRequisitionId] IS NOT NULL OR d.[SourcingReleaseId] IS NOT NULL) AND
                           (ISNULL(d.[SourcePurchaseRequisitionId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourcePurchaseRequisitionId], '00000000-0000-0000-0000-000000000000')
                            OR ISNULL(d.[SourcingReleaseId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourcingReleaseId], '00000000-0000-0000-0000-000000000000')))
                )
                BEGIN
                    THROW 51042, 'Tender sourcing requires one immutable same-tenant requisition release.', 1;
                END
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_RequestForQuotations_SourcingReleaseGuard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_Tenders_SourcingReleaseGuard];");
        migrationBuilder.Sql("ALTER TABLE [RequestForQuotations] DROP CONSTRAINT [FK_RequestForQuotations_ProcurementRequisitionSourcingReleases_SourcingReleaseId];");
        migrationBuilder.Sql("ALTER TABLE [RequestForQuotations] DROP CONSTRAINT [FK_RequestForQuotations_PurchaseRequisitions_SourcePurchaseRequisitionId];");
        migrationBuilder.Sql("ALTER TABLE [Tenders] DROP CONSTRAINT [FK_Tenders_ProcurementRequisitionSourcingReleases_SourcingReleaseId];");
        migrationBuilder.Sql("ALTER TABLE [Tenders] DROP CONSTRAINT [FK_Tenders_PurchaseRequisitions_SourcePurchaseRequisitionId];");
        migrationBuilder.Sql("DROP TABLE [ProcurementRequisitionSourcingReleases];");
        migrationBuilder.Sql("DROP INDEX [IX_Tenders_SourcePurchaseRequisitionId] ON [Tenders];");
        migrationBuilder.Sql("DROP INDEX [IX_Tenders_SourcingReleaseId] ON [Tenders];");
        migrationBuilder.Sql("DROP INDEX [IX_RequestForQuotations_SourcingReleaseId] ON [RequestForQuotations];");
        migrationBuilder.Sql("ALTER TABLE [Tenders] DROP COLUMN [SourcePurchaseRequisitionId];");
        migrationBuilder.Sql("ALTER TABLE [Tenders] DROP COLUMN [SourcingReleaseId];");
        migrationBuilder.Sql("ALTER TABLE [RequestForQuotations] DROP COLUMN [SourcingReleaseId];");
        migrationBuilder.Sql("ALTER TABLE [RequestForQuotations] ADD CONSTRAINT [FK_RequestForQuotations_PurchaseRequisitions_SourcePurchaseRequisitionId] FOREIGN KEY ([SourcePurchaseRequisitionId]) REFERENCES [PurchaseRequisitions] ([Id]);");
    }
}
