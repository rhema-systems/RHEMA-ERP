using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

public partial class AddQuantitySurveyRetentionReleaseLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "QuantitySurveyConfigurationProfileId",
            table: "ProcurementWorksCloseoutActions",
            type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "QuantitySurveyConfigurationProfileVersion",
            table: "ProcurementWorksCloseoutActions",
            type: "int",
            nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "QuantitySurveyRetentionDecisionId",
            table: "ProcurementWorksCloseoutActions",
            type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "QuantitySurveyRetentionPolicyHash",
            table: "ProcurementWorksCloseoutActions",
            type: "varchar(64)",
            unicode: false,
            maxLength: 64,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RequestHash",
            table: "ProcurementWorksCloseoutActions",
            type: "varchar(64)",
            unicode: false,
            maxLength: 64,
            nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "RetentionReleaseStage",
            table: "ProcurementWorksCloseoutActions",
            type: "int",
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "RetentionHeldSnapshot",
            table: "ProcurementWorksCloseoutActions",
            type: "decimal(18,2)",
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "RetentionReleasedBefore",
            table: "ProcurementWorksCloseoutActions",
            type: "decimal(18,2)",
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "RetentionStageLimitAmount",
            table: "ProcurementWorksCloseoutActions",
            type: "decimal(18,2)",
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "RetentionReleasedAfter",
            table: "ProcurementWorksCloseoutActions",
            type: "decimal(18,2)",
            nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "UsesRetentionBond",
            table: "ProcurementWorksCloseoutActions",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddCheckConstraint(
            name: "CK_ProcurementWorksCloseoutActions_QsRetention",
            table: "ProcurementWorksCloseoutActions",
            sql: "[RequestHash] IS NULL OR (LEN([RequestHash]) = 64 AND (([ActionType] = 5 AND [RetentionReleaseStage] BETWEEN 0 AND 3 AND [QuantitySurveyConfigurationProfileId] IS NOT NULL AND [QuantitySurveyConfigurationProfileVersion] > 0 AND [QuantitySurveyRetentionDecisionId] IS NOT NULL AND LEN([QuantitySurveyRetentionPolicyHash]) = 64 AND [Amount] > 0 AND [RetentionHeldSnapshot] >= 0 AND [RetentionReleasedBefore] >= 0 AND [RetentionStageLimitAmount] >= [Amount] AND ABS([RetentionReleasedAfter] - ([RetentionReleasedBefore] + [Amount])) <= 0.01 AND [RetentionReleasedAfter] <= [RetentionHeldSnapshot]) OR ([ActionType] <> 5 AND [RetentionReleaseStage] IS NULL AND [QuantitySurveyConfigurationProfileId] IS NULL AND [QuantitySurveyConfigurationProfileVersion] IS NULL AND [QuantitySurveyRetentionDecisionId] IS NULL AND [QuantitySurveyRetentionPolicyHash] IS NULL AND [RetentionHeldSnapshot] IS NULL AND [RetentionReleasedBefore] IS NULL AND [RetentionStageLimitAmount] IS NULL AND [RetentionReleasedAfter] IS NULL AND [UsesRetentionBond] = 0)))");

        migrationBuilder.CreateIndex(
            name: "IX_ProcurementWorksCloseoutActions_QuantitySurveyConfigurationProfileId",
            table: "ProcurementWorksCloseoutActions",
            column: "QuantitySurveyConfigurationProfileId");
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementWorksCloseoutActions_QuantitySurveyRetentionDecisionId",
            table: "ProcurementWorksCloseoutActions",
            column: "QuantitySurveyRetentionDecisionId");
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementWorksCloseoutActions_TenantId_ContractId_RetentionReleaseStage_Status",
            table: "ProcurementWorksCloseoutActions",
            columns: new[] { "TenantId", "ContractId", "RetentionReleaseStage", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_ProcurementWorksCloseoutActions_TenantId_ContractId_RetentionReleaseStage_ProjectHandoverItemId",
            table: "ProcurementWorksCloseoutActions",
            columns: new[] { "TenantId", "ContractId", "RetentionReleaseStage", "ProjectHandoverItemId" },
            unique: true,
            filter: "[ActionType] = 5 AND [Status] = 1 AND [ProjectHandoverItemId] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.AddForeignKey(
            name: "FK_ProcurementWorksCloseoutActions_QuantitySurveyConfigurationProfiles_QuantitySurveyConfigurationProfileId",
            table: "ProcurementWorksCloseoutActions",
            column: "QuantitySurveyConfigurationProfileId",
            principalTable: "QuantitySurveyConfigurationProfiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_ProcurementWorksCloseoutActions_QuantitySurveyConfigurationDecisions_QuantitySurveyRetentionDecisionId",
            table: "ProcurementWorksCloseoutActions",
            column: "QuantitySurveyRetentionDecisionId",
            principalTable: "QuantitySurveyConfigurationDecisions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementWorksCloseoutActions_QS0504RetentionGuard]
            ON [dbo].[ProcurementWorksCloseoutActions]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE ISNULL(i.[RetentionReleaseStage], -1) <> ISNULL(d.[RetentionReleaseStage], -1)
                       OR ISNULL(i.[QuantitySurveyConfigurationProfileId], '00000000-0000-0000-0000-000000000000')
                          <> ISNULL(d.[QuantitySurveyConfigurationProfileId], '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(i.[QuantitySurveyConfigurationProfileVersion], -1)
                          <> ISNULL(d.[QuantitySurveyConfigurationProfileVersion], -1)
                       OR ISNULL(i.[QuantitySurveyRetentionDecisionId], '00000000-0000-0000-0000-000000000000')
                          <> ISNULL(d.[QuantitySurveyRetentionDecisionId], '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(i.[QuantitySurveyRetentionPolicyHash], '') <> ISNULL(d.[QuantitySurveyRetentionPolicyHash], '')
                       OR ISNULL(i.[RequestHash], '') <> ISNULL(d.[RequestHash], '')
                       OR ISNULL(i.[RetentionHeldSnapshot], -1) <> ISNULL(d.[RetentionHeldSnapshot], -1)
                       OR ISNULL(i.[RetentionReleasedBefore], -1) <> ISNULL(d.[RetentionReleasedBefore], -1)
                       OR ISNULL(i.[RetentionStageLimitAmount], -1) <> ISNULL(d.[RetentionStageLimitAmount], -1)
                       OR ISNULL(i.[RetentionReleasedAfter], -1) <> ISNULL(d.[RetentionReleasedAfter], -1)
                       OR i.[UsesRetentionBond] <> d.[UsesRetentionBond]
                )
                    THROW 55041, 'QS-0504 retention request and policy lineage is immutable.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN [dbo].[QuantitySurveyConfigurationProfiles] p
                      ON p.[Id] = i.[QuantitySurveyConfigurationProfileId]
                     AND p.[TenantId] = i.[TenantId]
                     AND p.[Version] = i.[QuantitySurveyConfigurationProfileVersion]
                     AND p.[LifecycleStatus] = 1
                     AND p.[IsDeleted] = 0
                     AND p.[EffectiveFrom] <= COALESCE(i.[EffectiveAtUtc], i.[SubmittedAtUtc])
                     AND (p.[EffectiveTo] IS NULL OR p.[EffectiveTo] >= COALESCE(i.[EffectiveAtUtc], i.[SubmittedAtUtc]))
                    LEFT JOIN [dbo].[QuantitySurveyConfigurationDecisions] d
                      ON d.[Id] = i.[QuantitySurveyRetentionDecisionId]
                     AND d.[TenantId] = i.[TenantId]
                     AND d.[ProfileId] = p.[Id]
                     AND d.[DecisionKey] = 'QS-DEC-009'
                     AND d.[Status] = 2
                     AND d.[ApprovalStatus] = 1
                     AND d.[EvidenceStatus] = 2
                     AND d.[IsDeleted] = 0
                     AND (d.[EffectiveFrom] IS NULL OR d.[EffectiveFrom] <= COALESCE(i.[EffectiveAtUtc], i.[SubmittedAtUtc]))
                     AND (d.[EffectiveTo] IS NULL OR d.[EffectiveTo] >= COALESCE(i.[EffectiveAtUtc], i.[SubmittedAtUtc]))
                    LEFT JOIN [dbo].[WorkflowDefinitions] wd
                      ON wd.[Id] = i.[WorkflowDefinitionId]
                     AND wd.[TenantId] = i.[TenantId]
                     AND wd.[LifecycleStatus] = 1
                     AND wd.[IsActive] = 1
                     AND wd.[IsDeleted] = 0
                    LEFT JOIN [dbo].[WorkflowEntityTypes] wet
                      ON wet.[Id] = wd.[EntityTypeId]
                     AND wet.[TenantId] = i.[TenantId]
                     AND wet.[Code] = 'QS_RETENTION_RELEASE'
                     AND wet.[IsActive] = 1
                     AND wet.[IsDeleted] = 0
                    LEFT JOIN [dbo].[ProjectHandoverItems] hi
                      ON hi.[Id] = i.[ProjectHandoverItemId]
                     AND hi.[TenantId] = i.[TenantId]
                     AND hi.[ProjectId] = i.[ProjectId]
                     AND UPPER(hi.[HandoverType]) = 'PRACTICALCOMPLETION'
                     AND UPPER(hi.[Status]) = 'COMPLETED'
                     AND hi.[IsDeleted] = 0
                    LEFT JOIN [dbo].[Contracts] bc
                      ON bc.[Id] = i.[ContractId]
                     AND bc.[TenantId] = i.[TenantId]
                     AND bc.[IsDeleted] = 0
                    LEFT JOIN [dbo].[PerformanceBondRequests] pb
                      ON pb.[Id] = i.[PerformanceBondRequestId]
                     AND pb.[TenantId] = i.[TenantId]
                     AND pb.[TenderAwardId] = bc.[TenderAwardId]
                     AND pb.[BusinessPartnerId] = bc.[BusinessPartnerId]
                     AND UPPER(pb.[Status]) = 'APPROVED'
                     AND pb.[IsDeleted] = 0
                    WHERE i.[RequestHash] IS NOT NULL
                      AND i.[ActionType] = 5
                      AND
                      (
                          p.[Id] IS NULL OR d.[Id] IS NULL OR wd.[Id] IS NULL OR wet.[Id] IS NULL
                          OR (i.[RetentionReleaseStage] IN (0,1) AND hi.[Id] IS NULL)
                          OR (i.[RetentionReleaseStage] = 1 AND hi.[ProjectUnitId] IS NULL)
                          OR (i.[UsesRetentionBond] = 1 AND pb.[Id] IS NULL)
                      )
                )
                    THROW 55042, 'QS-0504 requires same-tenant effective DEC-009 policy, QS workflow, controlled stage source and approved bond lineage.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.[Id] = i.[Id]
                    LEFT JOIN [dbo].[WorkflowInstances] wi
                      ON wi.[Id] = i.[WorkflowInstanceId]
                     AND wi.[TenantId] = i.[TenantId]
                     AND wi.[EntityId] = i.[Id]
                     AND wi.[WorkflowDefinitionId] = i.[WorkflowDefinitionId]
                    LEFT JOIN [dbo].[WorkflowEntityTypes] wet
                      ON wet.[Id] = wi.[EntityTypeId]
                     AND wet.[TenantId] = i.[TenantId]
                    OUTER APPLY
                    (
                        SELECT SUM(a.[Amount]) AS ApprovedAmount
                        FROM [dbo].[ProcurementWorksCloseoutActions] a
                        WHERE a.[TenantId] = i.[TenantId]
                          AND a.[ContractId] = i.[ContractId]
                          AND a.[ActionType] = 5
                          AND a.[Status] = 1
                          AND a.[IsDeleted] = 0
                    ) total
                    WHERE i.[RequestHash] IS NOT NULL
                      AND i.[ActionType] = 5
                      AND d.[Status] <> 1
                      AND i.[Status] = 1
                      AND
                      (
                          wi.[Id] IS NULL OR wi.[Status] <> 2
                          OR wet.[Id] IS NULL OR wet.[Code] <> 'QS_RETENTION_RELEASE'
                          OR ISNULL(total.[ApprovedAmount], 0) > i.[RetentionHeldSnapshot] + 0.01
                      )
                )
                    THROW 55043, 'QS-0504 approval requires the completed QS workflow and cannot release more than the governed retention held.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementWorksCloseoutActions_QS0504RetentionGuard];");
        migrationBuilder.DropForeignKey(
            name: "FK_ProcurementWorksCloseoutActions_QuantitySurveyConfigurationProfiles_QuantitySurveyConfigurationProfileId",
            table: "ProcurementWorksCloseoutActions");
        migrationBuilder.DropForeignKey(
            name: "FK_ProcurementWorksCloseoutActions_QuantitySurveyConfigurationDecisions_QuantitySurveyRetentionDecisionId",
            table: "ProcurementWorksCloseoutActions");
        migrationBuilder.DropIndex(
            name: "IX_ProcurementWorksCloseoutActions_QuantitySurveyConfigurationProfileId",
            table: "ProcurementWorksCloseoutActions");
        migrationBuilder.DropIndex(
            name: "IX_ProcurementWorksCloseoutActions_QuantitySurveyRetentionDecisionId",
            table: "ProcurementWorksCloseoutActions");
        migrationBuilder.DropIndex(
            name: "IX_ProcurementWorksCloseoutActions_TenantId_ContractId_RetentionReleaseStage_Status",
            table: "ProcurementWorksCloseoutActions");
        migrationBuilder.DropIndex(
            name: "IX_ProcurementWorksCloseoutActions_TenantId_ContractId_RetentionReleaseStage_ProjectHandoverItemId",
            table: "ProcurementWorksCloseoutActions");
        migrationBuilder.DropCheckConstraint(
            name: "CK_ProcurementWorksCloseoutActions_QsRetention",
            table: "ProcurementWorksCloseoutActions");
        migrationBuilder.DropColumn("QuantitySurveyConfigurationProfileId", "ProcurementWorksCloseoutActions");
        migrationBuilder.DropColumn("QuantitySurveyConfigurationProfileVersion", "ProcurementWorksCloseoutActions");
        migrationBuilder.DropColumn("QuantitySurveyRetentionDecisionId", "ProcurementWorksCloseoutActions");
        migrationBuilder.DropColumn("QuantitySurveyRetentionPolicyHash", "ProcurementWorksCloseoutActions");
        migrationBuilder.DropColumn("RequestHash", "ProcurementWorksCloseoutActions");
        migrationBuilder.DropColumn("RetentionReleaseStage", "ProcurementWorksCloseoutActions");
        migrationBuilder.DropColumn("RetentionHeldSnapshot", "ProcurementWorksCloseoutActions");
        migrationBuilder.DropColumn("RetentionReleasedBefore", "ProcurementWorksCloseoutActions");
        migrationBuilder.DropColumn("RetentionStageLimitAmount", "ProcurementWorksCloseoutActions");
        migrationBuilder.DropColumn("RetentionReleasedAfter", "ProcurementWorksCloseoutActions");
        migrationBuilder.DropColumn("UsesRetentionBond", "ProcurementWorksCloseoutActions");
    }
}
