using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912153000_OptionalRfqEvaluationApproval")]
public sealed class OptionalRfqEvaluationApproval : Migration
{
    private const string Lifecycle = "TR_ProcurementRfqEvaluations_Lifecycle";
    private const string Readiness = "TR_ProcurementAwardReadinessDecisions_Immutable";
    private const string ExistingTransition = "(d.Status = 0 AND i.Status IN (0,1))";
    private const string OptionalTransition = "(d.Status = 0 AND (i.Status IN (0,1) OR (i.Status = 2 AND i.ApprovalRequired = 0)))";
    private const string ExistingScoreStatus = "WHERE i.Status = 1\n          AND (d.Id IS NULL OR d.Status = 0";
    private const string OptionalScoreStatus = "WHERE (i.Status = 1 OR (i.Status = 2 AND i.ApprovalRequired = 0))\n          AND (d.Id IS NULL OR d.Status = 0";
    private const string ExistingWorkflowRequirement = "(i.SourceType IN (0, 2)";
    private const string OptionalWorkflowRequirement = """
        ((i.SourceType = 2 OR (i.SourceType = 0 AND NOT EXISTS (
            SELECT 1 FROM dbo.ProcurementRfqEvaluations directRfq
            WHERE directRfq.RfqId = i.SourceId AND directRfq.TenantId = i.TenantId
              AND directRfq.IsDeleted = 0 AND directRfq.Status = 2
              AND directRfq.ApprovalRequired = 0)))
        """;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("ApprovalRequired", "ProcurementRfqEvaluations", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProcurementRfqEvaluations_ApprovalPolicy
            ON dbo.ProcurementRfqEvaluations AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id
                    WHERE (d.Id IS NULL AND i.ApprovalRequired = 0)
                       OR (d.Id IS NOT NULL AND i.ApprovalRequired <> d.ApprovalRequired
                           AND NOT (d.ApprovalRequired = 1 AND i.ApprovalRequired = 0
                               AND d.Status = 0 AND i.Status = 2
                               AND d.WorkflowInstanceId IS NULL AND d.ApprovedByUserId IS NULL
                               AND d.ApprovedAtUtc IS NULL AND d.ApprovalReference IS NULL
                               AND NOT EXISTS (SELECT 1 FROM OPENJSON(d.ApprovalActorsJson)))))
                    THROW 52540, 'RFQ_APPROVAL_POLICY_IMMUTABLE: retained approval history cannot be reclassified.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.ApprovalRequired = 0
                    AND (i.WorkflowInstanceId IS NOT NULL OR i.ApprovedByUserId IS NOT NULL
                        OR i.ApprovedAtUtc IS NOT NULL OR i.ApprovedByName IS NOT NULL
                        OR i.ApprovalReference IS NOT NULL
                        OR NOT ISJSON(i.ApprovalActorsJson) = 1
                        OR EXISTS (SELECT 1 FROM OPENJSON(i.ApprovalActorsJson))))
                    THROW 52541, 'RFQ_DIRECT_APPROVAL_METADATA: direct completion cannot claim workflow or human approval.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE d.ApprovalRequired = 1 AND i.ApprovalRequired = 0
                      AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId, N'TENDER_EVALUATION', i.RfqId) = 1)
                    THROW 52542, 'RFQ_APPROVAL_STILL_REQUIRED: the active route or retained instance must be completed.', 1;
            END;
            """);
        Patch(migrationBuilder, Lifecycle, ExistingTransition, OptionalTransition);
        Patch(migrationBuilder, Lifecycle, ExistingScoreStatus, OptionalScoreStatus);
        foreach (var field in new[] { "WorkflowDefinitionId", "WorkflowInstanceId" })
            Patch(migrationBuilder, Lifecycle, ScoreWorkflow(field, false), ScoreWorkflow(field, true));
        Patch(migrationBuilder, Readiness, ExistingWorkflowRequirement, OptionalWorkflowRequirement);
        // The public JSON flag must agree with the exact tenant-owned source, not decide policy itself.
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProcurementAwardReadinessDecisions_RfqApprovalPolicy
            ON dbo.ProcurementAwardReadinessDecisions AFTER INSERT
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    JOIN dbo.ProcurementRfqEvaluations e ON e.RfqId = i.SourceId
                        AND e.TenantId = i.TenantId AND e.IsDeleted = 0
                    WHERE i.SourceType = 0 AND i.Status = 1
                      AND ((e.ApprovalRequired = 0 AND (
                            ISNULL(JSON_VALUE(i.AuthorityLineageJson, '$.approvalRequired'), '') <> 'false'
                            OR i.WorkflowInstanceId IS NOT NULL
                            OR JSON_VALUE(i.AuthorityLineageJson, '$.workflowStatus') IS NOT NULL
                            OR JSON_VALUE(i.AuthorityLineageJson, '$.approvalReference') IS NOT NULL
                            OR JSON_QUERY(i.AuthorityLineageJson, '$.approvalActorUserIds') IS NULL
                            OR LEFT(LTRIM(JSON_QUERY(i.AuthorityLineageJson, '$.approvalActorUserIds')), 1) <> '['
                            OR EXISTS (SELECT 1 FROM OPENJSON(i.AuthorityLineageJson, '$.approvalActorUserIds'))
                            OR JSON_VALUE(i.AuthorityLineageJson, '$.approvedByUserId') IS NOT NULL
                            OR JSON_VALUE(i.AuthorityLineageJson, '$.approvedAtUtc') IS NOT NULL))
                           OR (e.ApprovalRequired = 1
                               AND JSON_VALUE(i.AuthorityLineageJson, '$.approvalRequired') = 'false')))
                    THROW 52543, 'RFQ_READINESS_APPROVAL_POLICY: the decision must retain its exact source-owned approval policy.', 1;
            END;
            """);
    }

    private static string ScoreWorkflow(string field, bool optional)
    {
        var jsonField = char.ToLowerInvariant(field[0]) + field[1..];
        var comparison = $"TRY_CONVERT(uniqueidentifier, JSON_VALUE(i.SnapshotJson, '$.{jsonField}')) = i.{field}";
        return optional
            ? $"({comparison} OR (i.ApprovalRequired = 0 AND i.{field} IS NULL AND JSON_VALUE(i.SnapshotJson, '$.{jsonField}') IS NULL))"
            : comparison;
    }

    internal static void Patch(MigrationBuilder builder, string trigger, string before, string after)
    {
        var oldSql = before.Replace("\r\n", "\n").Replace("'", "''");
        var newSql = after.Replace("\r\n", "\n").Replace("'", "''");
        builder.Sql($$"""
            DECLARE @definition nvarchar(max) = REPLACE(OBJECT_DEFINITION(OBJECT_ID(N'dbo.{{trigger}}')), CHAR(13), '');
            -- SQL script generation on Windows may reintroduce CRLF after the C# normalization.
            -- Normalize both runtime operands, not just the stored trigger definition.
            DECLARE @before nvarchar(max) = REPLACE(N'{{oldSql}}', CHAR(13), N'');
            DECLARE @after nvarchar(max) = REPLACE(N'{{newSql}}', CHAR(13), N'');
            IF @definition IS NULL OR CHARINDEX(@before, @definition) = 0
                THROW 52544, 'RFQ_APPROVAL_TRIGGER_BASELINE: expected protected trigger was not found; no guards were changed.', 1;
            SET @definition = REPLACE(@definition, @before, @after);
            DECLARE @start int = CHARINDEX(N'TRIGGER', UPPER(@definition));
            IF @start = 0 THROW 52544, 'RFQ_APPROVAL_TRIGGER_BASELINE: invalid trigger declaration.', 1;
            SET @definition = N'ALTER ' + SUBSTRING(@definition, @start, LEN(@definition));
            EXEC sys.sp_executesql @definition;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.ProcurementRfqEvaluations WHERE ApprovalRequired = 0) THROW 52545, 'Direct-completed RFQ evaluations prevent this approval-policy downgrade.', 1;");
        migrationBuilder.Sql("DROP TRIGGER dbo.TR_ProcurementAwardReadinessDecisions_RfqApprovalPolicy;");
        Patch(migrationBuilder, Readiness, OptionalWorkflowRequirement, ExistingWorkflowRequirement);
        foreach (var field in new[] { "WorkflowDefinitionId", "WorkflowInstanceId" })
            Patch(migrationBuilder, Lifecycle, ScoreWorkflow(field, true), ScoreWorkflow(field, false));
        Patch(migrationBuilder, Lifecycle, OptionalScoreStatus, ExistingScoreStatus);
        Patch(migrationBuilder, Lifecycle, OptionalTransition, ExistingTransition);
        migrationBuilder.Sql("DROP TRIGGER dbo.TR_ProcurementRfqEvaluations_ApprovalPolicy;");
        migrationBuilder.DropColumn("ApprovalRequired", "ProcurementRfqEvaluations");
    }
}
