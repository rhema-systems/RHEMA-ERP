using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912155000_OptionalControlledTenderApproval")]
public sealed class OptionalControlledTenderApproval : Migration
{
    private const string Readiness = "TR_ProcurementAwardReadinessDecisions_Immutable";
    private const string ExistingTransition = "(d.[Status] = 3 AND i.[Status] = 4)";
    private const string DirectTransition = "(d.[Status] = 3 AND (i.[Status] = 4 OR (i.[Status] = 5 AND i.ApprovalRequired = 0)))";
    private const string ExistingRecommendation = """
        AND c.AuthorityRouteId = i.AuthorityRouteId
                            AND c.AuthorityRouteReference = i.AuthorityRouteReference
                            AND c.WorkflowDefinitionId = i.WorkflowDefinitionId
                            AND c.WorkflowInstanceId = i.WorkflowInstanceId
        """;
    private const string DirectRecommendation = """
        AND c.AuthorityRouteId = i.AuthorityRouteId
                            AND c.AuthorityRouteReference = i.AuthorityRouteReference
                            AND (c.WorkflowDefinitionId = i.WorkflowDefinitionId
                                OR (c.ApprovalRequired = 0 AND c.WorkflowDefinitionId IS NULL AND i.WorkflowDefinitionId IS NULL))
                            AND (c.WorkflowInstanceId = i.WorkflowInstanceId
                                OR (c.ApprovalRequired = 0 AND c.WorkflowInstanceId IS NULL AND i.WorkflowInstanceId IS NULL))
        """;
    private const string ExistingWorkflowRequirement = """
        OR (i.SourceType = 1 AND EXISTS (
                           SELECT 1
                           FROM [dbo].[ProcurementTenderControls] c
                           WHERE c.TenderId = i.SourceId
                             AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                       )))
        """;
    private const string DirectWorkflowRequirement = """
        OR (i.SourceType = 1 AND EXISTS (
                           SELECT 1
                           FROM [dbo].[ProcurementTenderControls] c
                           WHERE c.TenderId = i.SourceId
                             AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                             AND c.ApprovalRequired = 1
                       )))
        """;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("ApprovalRequired", "ProcurementTenderControls", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProcurementTenderControls_ApprovalPolicy
            ON dbo.ProcurementTenderControls AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id
                    WHERE (d.Id IS NULL AND i.ApprovalRequired = 0)
                       OR (d.Id IS NOT NULL AND i.ApprovalRequired <> d.ApprovalRequired
                           AND NOT (d.ApprovalRequired = 1 AND i.ApprovalRequired = 0
                               AND d.Status = 3 AND i.Status = 5
                               AND d.WorkflowInstanceId IS NULL AND d.ApprovedById IS NULL
                               AND d.ApprovedAtUtc IS NULL AND d.AuthorityApprovalReference IS NULL
                               AND NOT EXISTS (SELECT 1 FROM OPENJSON(d.ApprovalActorsJson)))))
                    THROW 52550, 'TENDER_APPROVAL_POLICY_IMMUTABLE: retained approval history cannot be reclassified.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.ApprovalRequired = 0
                    AND (i.WorkflowInstanceId IS NOT NULL OR i.ApprovedById IS NOT NULL
                        OR i.ApprovedAtUtc IS NOT NULL OR i.AuthorityApprovalReference IS NOT NULL
                        OR NOT ISJSON(i.ApprovalActorsJson) = 1
                        OR EXISTS (SELECT 1 FROM OPENJSON(i.ApprovalActorsJson))))
                    THROW 52551, 'TENDER_DIRECT_APPROVAL_METADATA: direct completion cannot claim workflow or internal approval.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE d.ApprovalRequired = 1 AND i.ApprovalRequired = 0
                      AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId, N'TenderAward', i.TenderId) = 1)
                    THROW 52552, 'TENDER_APPROVAL_STILL_REQUIRED: the active route or retained instance must be completed.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i WHERE i.ApprovalRequired = 0
                      AND EXISTS (SELECT 1 FROM dbo.ProcurementRequisitionAuthorityRouteSteps s
                          WHERE s.AuthorityRouteId = i.AuthorityRouteId AND s.TenantId = i.TenantId AND s.IsDeleted = 0
                            AND (UPPER(s.AuthorityName) LIKE '%PPA%' OR UPPER(s.AuthorityRole) LIKE '%PPA%'
                                OR UPPER(s.AuthorityName) LIKE '%CENTRAL%' OR UPPER(s.AuthorityRole) LIKE '%CENTRAL%'))
                      AND NULLIF(LTRIM(RTRIM(i.PpaApprovalReference)), '') IS NULL)
                    THROW 52553, 'TENDER_PPA_APPROVAL_REQUIRED: disabling internal approval does not waive statutory PPA evidence.', 1;
            END;
            """);
        Patch(migrationBuilder, "TR_ProcurementTenderControls_Lifecycle", ExistingTransition, DirectTransition);
        Patch(migrationBuilder, Readiness, ExistingRecommendation, DirectRecommendation);
        Patch(migrationBuilder, Readiness, ExistingWorkflowRequirement, DirectWorkflowRequirement);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProcurementAwardReadinessDecisions_TenderApprovalPolicy
            ON dbo.ProcurementAwardReadinessDecisions AFTER INSERT
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    JOIN dbo.ProcurementTenderControls c ON c.TenderId = i.SourceId
                        AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                    WHERE i.SourceType = 1 AND i.Status = 1
                      AND ((c.ApprovalRequired = 0 AND (
                            ISNULL(JSON_VALUE(i.AuthorityLineageJson, '$.approvalRequired'), '') <> 'false'
                            OR i.WorkflowInstanceId IS NOT NULL
                            OR JSON_VALUE(i.AuthorityLineageJson, '$.workflowStatus') IS NOT NULL
                            OR JSON_VALUE(i.AuthorityLineageJson, '$.approvalReference') IS NOT NULL
                            OR JSON_QUERY(i.AuthorityLineageJson, '$.approvalActorUserIds') IS NULL
                            OR LEFT(LTRIM(JSON_QUERY(i.AuthorityLineageJson, '$.approvalActorUserIds')), 1) <> '['
                            OR EXISTS (SELECT 1 FROM OPENJSON(i.AuthorityLineageJson, '$.approvalActorUserIds'))
                            OR JSON_VALUE(i.AuthorityLineageJson, '$.approvedByUserId') IS NOT NULL
                            OR JSON_VALUE(i.AuthorityLineageJson, '$.approvedAtUtc') IS NOT NULL))
                           OR (c.ApprovalRequired = 1
                               AND JSON_VALUE(i.AuthorityLineageJson, '$.approvalRequired') = 'false')))
                    THROW 52554, 'TENDER_READINESS_APPROVAL_POLICY: the decision must retain its exact source-owned approval policy.', 1;
            END;
            """);
    }

    private static void Patch(MigrationBuilder builder, string trigger, string before, string after) =>
        OptionalRfqEvaluationApproval.Patch(builder, trigger, before, after);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.ProcurementTenderControls WHERE ApprovalRequired = 0) THROW 52555, 'Direct-completed tender recommendations prevent this approval-policy downgrade.', 1;");
        migrationBuilder.Sql("DROP TRIGGER dbo.TR_ProcurementAwardReadinessDecisions_TenderApprovalPolicy;");
        Patch(migrationBuilder, Readiness, DirectWorkflowRequirement, ExistingWorkflowRequirement);
        Patch(migrationBuilder, Readiness, DirectRecommendation, ExistingRecommendation);
        Patch(migrationBuilder, "TR_ProcurementTenderControls_Lifecycle", DirectTransition, ExistingTransition);
        migrationBuilder.Sql("DROP TRIGGER dbo.TR_ProcurementTenderControls_ApprovalPolicy;");
        migrationBuilder.DropColumn("ApprovalRequired", "ProcurementTenderControls");
    }
}
