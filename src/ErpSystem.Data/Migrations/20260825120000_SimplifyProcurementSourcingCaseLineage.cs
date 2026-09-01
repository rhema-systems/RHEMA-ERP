using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

public partial class SimplifyProcurementSourcingCaseLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "SourcePlanId",
            table: "ProcurementSourcingCases",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.AlterColumn<Guid>(
            name: "SourcePlanItemId",
            table: "ProcurementSourcingCases",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.AlterColumn<Guid>(
            name: "AuthorityRouteId",
            table: "ProcurementSourcingCases",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.AlterColumn<string>(
            name: "AuthorityRouteReference",
            table: "ProcurementSourcingCases",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100);

        CreateLifecycleTrigger(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS
            (
                SELECT 1 FROM [dbo].[ProcurementSourcingCases]
                WHERE [SourcePlanId] IS NULL OR [SourcePlanItemId] IS NULL
                   OR [AuthorityRouteId] IS NULL OR [AuthorityRouteReference] IS NULL
            )
                THROW 51230, 'Cannot restore mandatory advanced sourcing-case lineage while simplified sourcing cases exist.', 1;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "SourcePlanId",
            table: "ProcurementSourcingCases",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "SourcePlanItemId",
            table: "ProcurementSourcingCases",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "AuthorityRouteId",
            table: "ProcurementSourcingCases",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "AuthorityRouteReference",
            table: "ProcurementSourcingCases",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100,
            oldNullable: true);

        CreateLifecycleTrigger(migrationBuilder);
    }

    private static void CreateLifecycleTrigger(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementSourcingCases_Lifecycle]
            ON [dbo].[ProcurementSourcingCases]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id] WHERE i.[Id] IS NULL)
                    THROW 51060, 'Procurement sourcing cases cannot be deleted.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN [dbo].[PurchaseRequisitions] pr ON pr.[Id] = i.[PurchaseRequisitionId]
                    LEFT JOIN [dbo].[ProcurementRequisitionSourcingReleases] sr ON sr.[Id] = i.[SourcingReleaseId]
                    LEFT JOIN [dbo].[ProcurementPlans] pp ON pp.[Id] = i.[SourcePlanId]
                    LEFT JOIN [dbo].[ProcurementPlanItems] pi ON pi.[Id] = i.[SourcePlanItemId]
                    LEFT JOIN [dbo].[ProcurementPolicySets] ps ON ps.[Id] = i.[PolicySetId]
                    LEFT JOIN [dbo].[ProcurementPolicyMethodRules] mr ON mr.[Id] = i.[MethodRuleId]
                    LEFT JOIN [dbo].[ProcurementPolicyThresholdRules] tr ON tr.[Id] = i.[ThresholdRuleId]
                    LEFT JOIN [dbo].[ProcurementRequisitionAuthorityRoutes] ar ON ar.[Id] = i.[AuthorityRouteId]
                    LEFT JOIN [dbo].[ProcurementPolicyExceptionRules] er ON er.[Id] = i.[ApprovedExceptionRuleId]
                    WHERE i.[IsDeleted] = 1
                       OR pr.[Id] IS NULL OR pr.[TenantId] <> i.[TenantId] OR pr.[IsDeleted] = 1
                       OR pr.[Status] <> 'Approved'
                       OR ISNULL(pr.[SourcePlanId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourcePlanId], '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(pr.[SourcePlanItemId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourcePlanItemId], '00000000-0000-0000-0000-000000000000')
                       OR pr.[ProcurementCategory] <> i.[Category] OR pr.[TotalAmount] <> i.[EstimatedValue]
                       OR UPPER(LTRIM(RTRIM(pr.[Currency]))) <> i.[CurrencyCode]
                       OR sr.[Id] IS NULL OR sr.[TenantId] <> i.[TenantId] OR sr.[IsDeleted] = 1
                       OR sr.[PurchaseRequisitionId] <> i.[PurchaseRequisitionId]
                       OR ISNULL(sr.[SourcePlanId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourcePlanId], '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(sr.[SourcePlanItemId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourcePlanItemId], '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(sr.[AuthorityRouteId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[AuthorityRouteId], '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(sr.[AuthorityRouteReference], '') <> ISNULL(i.[AuthorityRouteReference], '')
                       OR sr.[ControlFingerprint] <> i.[SourceControlFingerprint]
                       OR ISNULL(sr.[ApprovedExceptionRuleId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[ApprovedExceptionRuleId], '00000000-0000-0000-0000-000000000000')
                       OR ISNULL(sr.[ExceptionApprovalReference], '') <> ISNULL(i.[ExceptionApprovalReference], '')
                       OR (i.[SourcePlanId] IS NOT NULL AND (pp.[Id] IS NULL OR pp.[TenantId] <> i.[TenantId] OR pp.[IsDeleted] = 1))
                       OR (i.[SourcePlanItemId] IS NOT NULL AND (pi.[Id] IS NULL OR pi.[TenantId] <> i.[TenantId] OR pi.[IsDeleted] = 1
                           OR (i.[SourcePlanId] IS NOT NULL AND pi.[ProcurementPlanId] <> i.[SourcePlanId])))
                       OR ps.[Id] IS NULL OR ps.[TenantId] <> i.[TenantId] OR ps.[IsDeleted] = 1
                       OR ps.[Code] <> i.[PolicyCode] OR ps.[Version] <> i.[PolicyVersion]
                       OR ps.[LifecycleStatus] <> 1 OR ps.[PublishedAt] IS NULL OR ps.[EffectiveFrom] > i.[CreatedAt]
                       OR (ps.[EffectiveTo] IS NOT NULL AND ps.[EffectiveTo] < i.[CreatedAt])
                       OR mr.[Id] IS NULL OR mr.[TenantId] <> i.[TenantId] OR mr.[IsDeleted] = 1
                       OR mr.[PolicySetId] <> i.[PolicySetId] OR mr.[RuleCode] <> i.[MethodRuleCode]
                       OR mr.[Method] <> i.[SelectedMethod] OR mr.[Category] <> i.[Category] OR mr.[IsAllowed] = 0 OR mr.[IsEnabled] = 0
                       OR mr.[EffectiveFrom] > i.[CreatedAt] OR (mr.[EffectiveTo] IS NOT NULL AND mr.[EffectiveTo] < i.[CreatedAt])
                       OR tr.[Id] IS NULL OR tr.[TenantId] <> i.[TenantId] OR tr.[IsDeleted] = 1
                       OR tr.[PolicySetId] <> i.[PolicySetId] OR tr.[RuleCode] <> i.[ThresholdRuleCode]
                       OR tr.[Method] <> i.[SelectedMethod] OR tr.[Category] <> i.[Category] OR tr.[CurrencyCode] <> i.[CurrencyCode] OR tr.[IsEnabled] = 0
                       OR tr.[EffectiveFrom] > i.[CreatedAt] OR (tr.[EffectiveTo] IS NOT NULL AND tr.[EffectiveTo] < i.[CreatedAt])
                       OR i.[EstimatedValue] < tr.[LowerBound] OR (i.[EstimatedValue] = tr.[LowerBound] AND tr.[LowerInclusive] = 0)
                       OR (tr.[UpperBound] IS NOT NULL AND (i.[EstimatedValue] > tr.[UpperBound] OR (i.[EstimatedValue] = tr.[UpperBound] AND tr.[UpperInclusive] = 0)))
                       OR (i.[AuthorityRouteId] IS NULL AND i.[AuthorityRouteReference] IS NOT NULL)
                       OR (i.[AuthorityRouteId] IS NOT NULL AND
                           (i.[AuthorityRouteReference] IS NULL OR ar.[Id] IS NULL OR ar.[TenantId] <> i.[TenantId] OR ar.[IsDeleted] = 1
                            OR ar.[PurchaseRequisitionId] <> i.[PurchaseRequisitionId] OR ar.[PolicySetId] <> i.[PolicySetId]
                            OR ar.[RouteReference] <> i.[AuthorityRouteReference] OR ar.[Amount] <> i.[EstimatedValue]
                            OR ar.[Category] <> i.[Category] OR ar.[CurrencyCode] <> i.[CurrencyCode]))
                       OR (i.[ApprovedExceptionRuleId] IS NOT NULL AND
                           (er.[Id] IS NULL OR er.[TenantId] <> i.[TenantId] OR er.[IsDeleted] = 1
                            OR er.[PolicySetId] <> i.[PolicySetId]
                            OR ISNULL(pr.[ExceptionEvidenceReference], '') <> ISNULL(i.[ExceptionEvidenceReference], '')))
                )
                    THROW 51061, 'Procurement sourcing-case tenant, release, policy, rule, threshold, or optional lineage is invalid.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE (d.[Id] IS NULL AND
                           (i.[Status] <> 0 OR i.[StartedAtUtc] IS NOT NULL OR i.[ClosedAtUtc] IS NOT NULL OR i.[ClosureReason] IS NOT NULL))
                       OR (d.[Id] IS NOT NULL AND
                           (d.[TenantId] <> i.[TenantId]
                            OR d.[PurchaseRequisitionId] <> i.[PurchaseRequisitionId]
                            OR d.[SourcingReleaseId] <> i.[SourcingReleaseId]
                            OR d.[CaseSequence] <> i.[CaseSequence] OR d.[CaseNumber] <> i.[CaseNumber]
                            OR ISNULL(d.[SourcePlanId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourcePlanId], '00000000-0000-0000-0000-000000000000')
                            OR ISNULL(d.[SourcePlanItemId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[SourcePlanItemId], '00000000-0000-0000-0000-000000000000')
                            OR d.[Category] <> i.[Category] OR d.[SelectedMethod] <> i.[SelectedMethod]
                            OR d.[EstimatedValue] <> i.[EstimatedValue] OR d.[CurrencyCode] <> i.[CurrencyCode]
                            OR d.[PolicySetId] <> i.[PolicySetId] OR d.[PolicyCode] <> i.[PolicyCode] OR d.[PolicyVersion] <> i.[PolicyVersion]
                            OR d.[MethodRuleId] <> i.[MethodRuleId] OR d.[MethodRuleCode] <> i.[MethodRuleCode]
                            OR d.[ThresholdRuleId] <> i.[ThresholdRuleId] OR d.[ThresholdRuleCode] <> i.[ThresholdRuleCode]
                            OR ISNULL(d.[AuthorityRouteId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[AuthorityRouteId], '00000000-0000-0000-0000-000000000000')
                            OR ISNULL(d.[AuthorityRouteReference], '') <> ISNULL(i.[AuthorityRouteReference], '')
                            OR ISNULL(d.[ApprovedExceptionRuleId], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[ApprovedExceptionRuleId], '00000000-0000-0000-0000-000000000000')
                            OR ISNULL(d.[ExceptionApprovalReference], '') <> ISNULL(i.[ExceptionApprovalReference], '')
                            OR ISNULL(d.[ExceptionEvidenceReference], '') <> ISNULL(i.[ExceptionEvidenceReference], '')
                            OR d.[Justification] <> i.[Justification] OR d.[CreatedByName] <> i.[CreatedByName]
                            OR d.[SourceControlFingerprint] <> i.[SourceControlFingerprint] OR d.[CaseFingerprint] <> i.[CaseFingerprint]
                            OR d.[SnapshotJson] <> i.[SnapshotJson] OR d.[IntegrityHash] <> i.[IntegrityHash]
                            OR d.[CreatedAt] <> i.[CreatedAt] OR ISNULL(d.[CreatedById], '00000000-0000-0000-0000-000000000000') <> ISNULL(i.[CreatedById], '00000000-0000-0000-0000-000000000000')
                            OR i.[IsDeleted] <> 0
                            OR NOT ((d.[Status] = 0 AND i.[Status] IN (1, 3)) OR (d.[Status] = 1 AND i.[Status] IN (2, 3)))
                            OR (i.[Status] = 1 AND (i.[StartedAtUtc] IS NULL OR i.[StartedByName] IS NULL OR i.[ClosedAtUtc] IS NOT NULL))
                            OR (i.[Status] IN (2, 3) AND (i.[ClosedAtUtc] IS NULL OR i.[ClosedByName] IS NULL OR LEN(LTRIM(RTRIM(i.[ClosureReason]))) < 5))))
                )
                    THROW 51062, 'Procurement sourcing-case immutable fields or lifecycle transition are invalid.', 1;
            END
            """);
    }
}
