using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912210000_OptionalExceptionalAndPrequalificationApproval")]
public sealed class OptionalExceptionalAndPrequalificationApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "ProcurementExceptionalSourcingControls", "ProcurementPrequalificationExercises" })
        {
            migrationBuilder.AddColumn<bool>("ApprovalRequired", table, "bit", nullable: false, defaultValue: true);
            migrationBuilder.AlterColumn<Guid>("WorkflowDefinitionId", table, "uniqueidentifier", nullable: true,
                oldClrType: typeof(Guid), oldType: "uniqueidentifier");
        }
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProcurementExceptionalSourcingControls_ApprovalPolicy
            ON dbo.ProcurementExceptionalSourcingControls AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                    WHERE (d.Id IS NULL AND i.ApprovalRequired=0)
                       OR (d.Id IS NOT NULL AND i.ApprovalRequired<>d.ApprovalRequired AND NOT (
                           d.ApprovalRequired=1 AND i.ApprovalRequired=0 AND d.Status=0 AND i.Status=2
                           AND d.WorkflowInstanceId IS NULL AND d.SubmittedForApprovalAtUtc IS NULL
                           AND d.SubmittedForApprovalById IS NULL AND d.ApprovedAtUtc IS NULL AND d.ApprovedById IS NULL
                           AND NOT EXISTS (SELECT 1 FROM OPENJSON(d.ApprovalActorsJson))
                           AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'TenderException',i.TenderId)=0)))
                    THROW 52620, 'Exceptional sourcing approval mode may only be captured by its first valid completion.', 1;
                IF EXISTS (SELECT 1 FROM inserted i
                    WHERE i.ApprovalRequired=0 AND (i.Status NOT IN (2,3,4,5,6,7,8)
                        OR i.WorkflowInstanceId IS NOT NULL OR i.ApprovedAtUtc IS NOT NULL OR i.ApprovedById IS NOT NULL
                        OR i.SubmittedForApprovalAtUtc IS NULL OR i.SubmittedForApprovalById IS NULL
                        OR EXISTS (SELECT 1 FROM OPENJSON(i.ApprovalActorsJson))))
                    THROW 52621, 'Direct sourcing must retain a completion owner without fabricated internal approval.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.WorkflowDefinitionId IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM dbo.WorkflowDefinitions w WHERE w.Id=i.WorkflowDefinitionId AND w.TenantId=i.TenantId))
                    THROW 52622, 'The selected sourcing workflow must belong to the source tenant.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProcurementPrequalificationExercises_ApprovalPolicy
            ON dbo.ProcurementPrequalificationExercises AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                    WHERE (d.Id IS NULL AND i.ApprovalRequired=0)
                       OR (d.Id IS NOT NULL AND i.ApprovalRequired<>d.ApprovalRequired AND NOT (
                           d.ApprovalRequired=1 AND i.ApprovalRequired=0 AND d.Status=3 AND i.Status=5
                           AND d.WorkflowInstanceId IS NULL AND d.SubmittedForApprovalAtUtc IS NULL
                           AND d.SubmittedForApprovalById IS NULL AND d.DecidedAtUtc IS NULL AND d.DecidedById IS NULL
                           AND d.DecisionReference IS NULL AND d.DecisionEvidenceReference IS NULL
                           AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'ProcurementSourcing',i.Id)=0)))
                    THROW 52623, 'Prequalification approval mode may only be captured by its first valid completion.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.ApprovalRequired=0 AND
                    (i.Status NOT IN (5,7) OR i.WorkflowInstanceId IS NOT NULL
                     OR i.SubmittedForApprovalAtUtc IS NULL OR i.SubmittedForApprovalById IS NULL
                     OR i.DecidedById IS NULL OR i.DecidedById<>i.SubmittedForApprovalById))
                    THROW 52624, 'Direct qualification must retain its actual business decision owner and no approval instance.', 1;
            END;
            """);
        Patch(migrationBuilder, "TR_ProcurementExceptionalSourcingControls_Lifecycle",
            "(d.[Status] = 0 AND i.[Status] = 1)",
            "(d.[Status] = 0 AND (i.[Status] = 1 OR (i.[Status] = 2 AND i.ApprovalRequired = 0)))");
        foreach (var trigger in new[] { "TR_ProcurementExceptionalSourcingControls_Lifecycle", "TR_ProcurementPrequalificationExercises_Lifecycle" })
            Patch(migrationBuilder, trigger, "i.[WorkflowDefinitionId] <> d.[WorkflowDefinitionId]",
                "ISNULL(CONVERT(nvarchar(36),i.[WorkflowDefinitionId]),'') <> ISNULL(CONVERT(nvarchar(36),d.[WorkflowDefinitionId]),'')");
        Patch(migrationBuilder, "TR_ProcurementPrequalificationExercises_Lifecycle",
            "WHERE w.[Id] IS NULL OR (i.[WorkflowInstanceId] IS NOT NULL AND wi.[Id] IS NULL)",
            "WHERE (i.[WorkflowDefinitionId] IS NOT NULL AND w.[Id] IS NULL) OR (i.[WorkflowInstanceId] IS NOT NULL AND wi.[Id] IS NULL)");
        Patch(migrationBuilder, "TR_ProcurementPrequalificationExercises_Lifecycle",
            "(d.[Status] = 3 AND i.[Status] = 4)",
            "(d.[Status] = 3 AND (i.[Status] = 4 OR (i.[Status] = 5 AND i.ApprovalRequired = 0)))");
        Patch(migrationBuilder, "TR_ProcurementPrequalificationExercises_Lifecycle",
            "WHERE d.[Status] = 3 AND i.[Status] = 4",
            "WHERE d.[Status] = 3 AND (i.[Status] = 4 OR (i.[Status] = 5 AND i.ApprovalRequired = 0))");
        PatchCheck(migrationBuilder, "ProcurementExceptionalSourcingControls", "CK_ProcurementExceptionalSourcingControls_Lifecycle",
            "[WorkflowInstanceId] IS NOT NULL", "([ApprovalRequired]=(0) OR [WorkflowInstanceId] IS NOT NULL)");
        PatchCheck(migrationBuilder, "ProcurementExceptionalSourcingControls", "CK_ProcurementExceptionalSourcingControls_Lifecycle",
            "[ApprovedAtUtc] IS NOT NULL AND [ApprovedById] IS NOT NULL",
            "([ApprovalRequired]=(0) OR [ApprovedAtUtc] IS NOT NULL AND [ApprovedById] IS NOT NULL)");
        PatchCheck(migrationBuilder, "ProcurementPrequalificationExercises", "CK_ProcurementPrequalificationExercises_Evidence",
            "[WorkflowInstanceId] IS NOT NULL", "([ApprovalRequired]=(0) OR [WorkflowInstanceId] IS NOT NULL)");
        migrationBuilder.Sql("""
            DECLARE @definition nvarchar(max)=REPLACE(OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_ProcurementAwardReadinessDecisions_Immutable')),CHAR(13),N'');
            DECLARE @readyScope int=CHARINDEX(N'THROW 51406,',@definition);
            DECLARE @start int=CHARINDEX(N'OR (i.SourceType = 2 AND NOT EXISTS (',@definition,@readyScope);
            DECLARE @finish int=CHARINDEX(N'THROW 51407,',@definition,@start);
            IF @definition IS NULL OR @readyScope=0 OR @start<=@readyScope OR @finish<=@start
                THROW 52625, 'Exceptional award-readiness source guard was not found; review the installed definition.', 1;
            DECLARE @fragment nvarchar(max)=SUBSTRING(@definition,@start,@finish-@start);
            IF CHARINDEX(N'AND c.WorkflowDefinitionId = i.WorkflowDefinitionId',@fragment)=0
                OR CHARINDEX(N'AND c.WorkflowInstanceId = i.WorkflowInstanceId',@fragment)=0
                THROW 52625, 'Exceptional source workflow identity checks were not found.', 1;
            SET @fragment=REPLACE(@fragment,N'AND c.WorkflowDefinitionId = i.WorkflowDefinitionId',
                N'AND (c.WorkflowDefinitionId = i.WorkflowDefinitionId OR (c.ApprovalRequired=0 AND c.WorkflowDefinitionId IS NULL AND i.WorkflowDefinitionId IS NULL))');
            SET @fragment=REPLACE(@fragment,N'AND c.WorkflowInstanceId = i.WorkflowInstanceId',
                N'AND (c.WorkflowInstanceId = i.WorkflowInstanceId OR (c.ApprovalRequired=0 AND c.WorkflowInstanceId IS NULL AND i.WorkflowInstanceId IS NULL))');
            SET @definition=STUFF(@definition,@start,@finish-@start,@fragment);
            SET @definition=STUFF(@definition,1,CHARINDEX(N'TRIGGER',UPPER(@definition))-1,N'ALTER ');
            EXEC sys.sp_executesql @definition;
            """);
        Patch(migrationBuilder, "TR_ProcurementAwardReadinessDecisions_Immutable",
            "i.SourceType = 2 OR (i.SourceType = 0",
            "(i.SourceType = 2 AND NOT EXISTS (SELECT 1 FROM dbo.ProcurementExceptionalSourcingControls directSource WHERE directSource.TenderId=i.SourceId AND directSource.TenantId=i.TenantId AND directSource.IsDeleted=0 AND directSource.Status=4 AND directSource.ApprovalRequired=0)) OR (i.SourceType = 0");
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProcurementAwardReadinessDecisions_ExceptionalApprovalPolicy
            ON dbo.ProcurementAwardReadinessDecisions AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted i
                    JOIN dbo.ProcurementExceptionalSourcingControls c ON c.TenderId=i.SourceId AND c.TenantId=i.TenantId AND c.IsDeleted=0
                    WHERE i.SourceType=2 AND (ISNULL(JSON_VALUE(i.AuthorityLineageJson,'$.approvalRequired'),'true')<>
                        CASE WHEN c.ApprovalRequired=1 THEN 'true' ELSE 'false' END
                    OR (c.ApprovalRequired=0 AND (i.WorkflowInstanceId IS NOT NULL
                        OR JSON_VALUE(i.AuthorityLineageJson,'$.workflowInstanceId') IS NOT NULL
                        OR JSON_VALUE(i.AuthorityLineageJson,'$.workflowStatus') IS NOT NULL
                        OR JSON_VALUE(i.AuthorityLineageJson,'$.approvalReference') IS NOT NULL
                        OR JSON_VALUE(i.AuthorityLineageJson,'$.approvedAtUtc') IS NOT NULL
                        OR JSON_VALUE(i.AuthorityLineageJson,'$.approvedByUserId') IS NOT NULL
                        OR ISNULL(JSON_QUERY(i.AuthorityLineageJson,'$.approvalActorUserIds'),'')<>'[]'))))
                    THROW 52626, 'Exceptional award readiness must retain the source approval mode without invented internal approval.', 1;
            END;
            """);
    }

    internal static void PatchCheck(MigrationBuilder migrationBuilder, string table, string constraint, string before, string after)
    {
        before = before.Replace("'", "''");
        after = after.Replace("'", "''");
        migrationBuilder.Sql($$"""
            DECLARE @definition nvarchar(max)=(SELECT definition FROM sys.check_constraints
                WHERE name=N'{{constraint}}' AND parent_object_id=OBJECT_ID(N'dbo.{{table}}'));
            DECLARE @before nvarchar(max)=REPLACE(N'{{before}}',CHAR(13),N'');
            DECLARE @after nvarchar(max)=REPLACE(N'{{after}}',CHAR(13),N'');
            SET @definition=REPLACE(@definition,CHAR(13),N'');
            IF @definition IS NULL OR CHARINDEX(@before,@definition)=0
                OR CHARINDEX(@before,@definition,CHARINDEX(@before,@definition)+LEN(@before))>0
                THROW 52627, 'The expected controlled evidence check was not found exactly once.', 1;
            SET @definition=REPLACE(@definition,@before,@after);
            DECLARE @sql nvarchar(max)=N'ALTER TABLE dbo.{{table}} DROP CONSTRAINT {{constraint}}; ALTER TABLE dbo.{{table}} WITH CHECK ADD CONSTRAINT {{constraint}} CHECK '+@definition;
            EXEC sys.sp_executesql @sql;
            """);
    }

    private static void Patch(MigrationBuilder migrationBuilder, string trigger, string before, string after) =>
        OptionalRfqEvaluationApproval.Patch(migrationBuilder, trigger, before, after);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Retained direct sourcing and qualification decisions require a reviewed forward-compatible rollback; approval history must not be fabricated.");
}
