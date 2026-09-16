using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

/// <summary>Retains historical approval while allowing centrally confirmed direct inspection completion.</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912130000_ReceiptInspectionOptionalWorkflow")]
public sealed class ReceiptInspectionOptionalWorkflow : Migration
{
    private const string CaseTrigger = "TR_ProcurementReceiptInspectionCases_TDC0502Protected";
    private const string ReceiptTrigger = "TR_PurchaseOrderReceiptItems_TDC0502AcceptanceProtected";
    private const string ActionTrigger = "TR_ProcurementReceiptInspectionActions_TDC0503SodHardStop";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "ApprovalRequired", table: "ProcurementReceiptInspectionCases",
            type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.AlterColumn<Guid>(name: "WorkflowDefinitionId", table: "ProcurementReceiptInspectionCases",
            type: "uniqueidentifier", nullable: true, oldClrType: typeof(Guid), oldType: "uniqueidentifier");
        migrationBuilder.DropCheckConstraint("CK_ProcurementReceiptInspectionActions_Type", "ProcurementReceiptInspectionActions");
        migrationBuilder.AddCheckConstraint("CK_ProcurementReceiptInspectionActions_Type", "ProcurementReceiptInspectionActions", "[ActionType] BETWEEN 0 AND 15");
        Patch(migrationBuilder, CaseTrigger, "(r.Id IS NULL OR w.Id IS NULL)",
            "(r.Id IS NULL OR (i.ApprovalRequired=1 AND w.Id IS NULL) OR (i.ApprovalRequired=0 AND i.WorkflowDefinitionId IS NOT NULL))");
        Patch(migrationBuilder, CaseTrigger, OldRebind, NewRebind);
        Patch(migrationBuilder, CaseTrigger, OldAcceptance, NewAcceptance);
        Patch(migrationBuilder, CaseTrigger, "SET NOCOUNT ON;", ModeGuard);
        Patch(migrationBuilder, ReceiptTrigger, OldReceiptAcceptance, NewReceiptAcceptance);
        Patch(migrationBuilder, ActionTrigger, "action.ActionType IN (3, 11, 12)", "action.ActionType IN (3, 11, 12, 15)");
        Patch(migrationBuilder, ActionTrigger, "SET NOCOUNT ON;", CompletionIdentityGuard);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Never fabricate a historical workflow or destroy direct-completion evidence for rollback.
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.ProcurementReceiptInspectionCases WHERE ApprovalRequired=0 OR WorkflowDefinitionId IS NULL)
                OR EXISTS (SELECT 1 FROM dbo.ProcurementReceiptInspectionActions WHERE ActionType=15)
                THROW 51983, 'Receipt optional-approval history exists. Roll forward; downgrade would falsify retained inspection history.', 1;
            """);
        Patch(migrationBuilder, ActionTrigger, CompletionIdentityGuard, "SET NOCOUNT ON;");
        Patch(migrationBuilder, ActionTrigger, "action.ActionType IN (3, 11, 12, 15)", "action.ActionType IN (3, 11, 12)");
        Patch(migrationBuilder, ReceiptTrigger, NewReceiptAcceptance, OldReceiptAcceptance);
        Patch(migrationBuilder, CaseTrigger, ModeGuard, "SET NOCOUNT ON;");
        Patch(migrationBuilder, CaseTrigger, NewAcceptance, OldAcceptance);
        Patch(migrationBuilder, CaseTrigger, NewRebind, OldRebind);
        Patch(migrationBuilder, CaseTrigger,
            "(r.Id IS NULL OR (i.ApprovalRequired=1 AND w.Id IS NULL) OR (i.ApprovalRequired=0 AND i.WorkflowDefinitionId IS NOT NULL))",
            "(r.Id IS NULL OR w.Id IS NULL)");
        migrationBuilder.DropCheckConstraint("CK_ProcurementReceiptInspectionActions_Type", "ProcurementReceiptInspectionActions");
        migrationBuilder.AddCheckConstraint("CK_ProcurementReceiptInspectionActions_Type", "ProcurementReceiptInspectionActions", "[ActionType] BETWEEN 0 AND 14");
        migrationBuilder.AlterColumn<Guid>(name: "WorkflowDefinitionId", table: "ProcurementReceiptInspectionCases",
            type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
        migrationBuilder.DropColumn(name: "ApprovalRequired", table: "ProcurementReceiptInspectionCases");
    }

    private static void Patch(MigrationBuilder builder, string trigger, string before, string after)
    {
        // The SQL generator rewrites multiline SQL to platform line endings.
        // Normalize at SQL execution as well, so Windows CRLF script literals
        // still match the LF-normalized retained trigger body exactly.
        static string Literal(string value) => "REPLACE(N'" + value.Replace("\r", "").Replace("'", "''") + "',CHAR(13),N'')";
        builder.Sql($"""
            DECLARE @body nvarchar(max)=REPLACE(OBJECT_DEFINITION(OBJECT_ID(N'dbo.[{trigger}]')),CHAR(13),N'');
            DECLARE @before nvarchar(max)={Literal(before)};
            IF @body IS NULL OR CHARINDEX(@before,@body)=0
                THROW 51980, 'RCV_OPTIONAL_APPROVAL_TRIGGER_DRIFT: expected receipt guard was not found; no guard was relaxed.', 1;
            SET @body=REPLACE(@body,@before,{Literal(after)});
            DECLARE @keyword int=CHARINDEX(N'TRIGGER',UPPER(@body));
            IF @keyword=0 THROW 51980, 'Receipt guard declaration could not be verified.', 1;
            SET @body=N'ALTER '+SUBSTRING(@body,@keyword,LEN(@body));
            EXEC sys.sp_executesql @body;
            """);
    }

    private const string OldRebind = """
        OR (
                           i.WorkflowDefinitionId <> d.WorkflowDefinitionId
                           AND NOT (
                               d.Status = 0
                               AND i.Status = 1
                               AND d.WorkflowInstanceId IS NULL
                               AND i.WorkflowInstanceId IS NULL
                               AND TRY_CONVERT(uniqueidentifier,
                                   SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_CASE_ID')) = i.Id
                               AND TRY_CONVERT(uniqueidentifier,
                                   SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_WORKFLOW_ID')) = i.WorkflowDefinitionId
                               AND EXISTS (
                                   SELECT 1
                                   FROM WorkflowDefinitions workflow
                                   JOIN WorkflowEntityTypes entityType
                                     ON entityType.Id = workflow.EntityTypeId
                                    AND entityType.TenantId = i.TenantId
                                    AND entityType.IsActive = 1
                                    AND entityType.IsDeleted = 0
                                   WHERE workflow.Id = i.WorkflowDefinitionId
                                     AND workflow.TenantId = i.TenantId
                                     AND workflow.IsActive = 1
                                     AND workflow.IsDeleted = 0
                                     AND workflow.LifecycleStatus = 1
                                     AND entityType.Code = N'PROCUREMENT_RECEIPT_INSPECTION'
                               )
                           )
                       )
        """;

    private const string NewRebind = """
        OR (
            (ISNULL(i.WorkflowDefinitionId,'00000000-0000-0000-0000-000000000000') <>
                     ISNULL(d.WorkflowDefinitionId,'00000000-0000-0000-0000-000000000000') OR i.ApprovalRequired <> d.ApprovalRequired)
            AND NOT (
                d.Status = 0 AND i.Status = 1
                AND d.WorkflowInstanceId IS NULL AND i.WorkflowInstanceId IS NULL
                AND ISNULL(TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_CASE_ID')),
                    '00000000-0000-0000-0000-000000000000') = i.Id
                AND (
                    (i.ApprovalRequired = 1
                     AND ISNULL(TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_WORKFLOW_ID')),
                         '00000000-0000-0000-0000-000000000000') = i.WorkflowDefinitionId
                     AND EXISTS (SELECT 1 FROM WorkflowDefinitions workflow
                         JOIN WorkflowEntityTypes entityType ON entityType.Id=workflow.EntityTypeId
                          AND entityType.TenantId=i.TenantId AND entityType.IsActive=1 AND entityType.IsDeleted=0
                         WHERE workflow.Id=i.WorkflowDefinitionId AND workflow.TenantId=i.TenantId
                          AND workflow.IsActive=1 AND workflow.IsDeleted=0 AND workflow.LifecycleStatus=1
                          AND (dbo.WorkflowApprovalEntityKey(entityType.Code)=N'PROCUREMENTRECEIPTINSPECTION'
                            OR dbo.WorkflowApprovalEntityKey(entityType.Name)=N'PROCUREMENTRECEIPTINSPECTION')))
                    OR (i.ApprovalRequired = 0 AND i.WorkflowDefinitionId IS NULL
                        AND SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_WORKFLOW_ID') IS NULL
                        AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'PROCUREMENT_RECEIPT_INSPECTION',i.Id)=0)
                )
            )
        )
        """;

    private const string OldAcceptance = """
        (TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_CASE_ID')) <> i.Id
                       OR wi.Id IS NULL OR wi.Status <> 2
                       OR i.DecidedByUserId IS NULL
                       OR i.DecidedByUserId = i.SubmittedByUserId
                       OR i.DecidedByUserId = i.CreatedByUserId))
        """;

    private const string NewAcceptance = """
        (ISNULL(TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_CASE_ID')),
                     '00000000-0000-0000-0000-000000000000') <> i.Id
            OR (i.ApprovalRequired = 1 AND
                (wi.Id IS NULL OR wi.Status <> 2 OR i.DecidedByUserId IS NULL
                 OR i.DecidedByUserId = i.SubmittedByUserId OR i.DecidedByUserId = i.CreatedByUserId))
            OR (i.ApprovalRequired = 0 AND
                (i.WorkflowDefinitionId IS NOT NULL OR i.WorkflowInstanceId IS NOT NULL
                 OR i.DecidedByUserId IS NOT NULL OR i.DecidedAtUtc IS NOT NULL
                 OR i.SubmittedByUserId IS NULL OR i.SubmittedAtUtc IS NULL
                 OR dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'PROCUREMENT_RECEIPT_INSPECTION',i.Id)<>0))))
        """;

    private const string OldReceiptAcceptance = """
        (c.Id IS NULL OR l.Id IS NULL OR wi.Id IS NULL OR wi.Status <> 2
                       OR c.Status <> 1 OR i.AcceptedQuantity <> l.AcceptedQuantity
                       OR i.RejectedQuantity <> l.RejectedQuantity))
        """;

    private const string NewReceiptAcceptance = """
        (c.Id IS NULL OR l.Id IS NULL
            OR c.Status <> 1 OR i.AcceptedQuantity <> l.AcceptedQuantity OR i.RejectedQuantity <> l.RejectedQuantity
            OR (c.ApprovalRequired = 1 AND (wi.Id IS NULL OR wi.Status <> 2
                OR wi.EntityId <> c.Id OR wi.WorkflowDefinitionId <> c.WorkflowDefinitionId))
            OR (c.ApprovalRequired = 0 AND (c.WorkflowDefinitionId IS NOT NULL OR c.WorkflowInstanceId IS NOT NULL
                OR c.DecidedByUserId IS NOT NULL OR c.DecidedAtUtc IS NOT NULL
                OR c.SubmittedByUserId IS NULL OR c.SubmittedAtUtc IS NULL
                OR dbo.WorkflowApprovalRequiredAtSubmission(c.TenantId,N'PROCUREMENT_RECEIPT_INSPECTION',c.Id)<>0))))
        """;

    private const string ModeGuard = """
        SET NOCOUNT ON;
        -- RCV_OPTIONAL_APPROVAL_MODE: preserve real history; absence is rechecked at submission/acceptance.
        IF EXISTS (
            SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
            WHERE i.ApprovalRequired=0 AND (
                i.WorkflowDefinitionId IS NOT NULL OR i.WorkflowInstanceId IS NOT NULL
                OR i.DecidedByUserId IS NOT NULL OR i.DecidedAtUtc IS NOT NULL
                OR i.Status IN (2,3)
                OR ((d.Id IS NULL OR i.Status=1 OR d.Status=1) AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'PROCUREMENT_RECEIPT_INSPECTION',i.Id)<>0)
                OR (i.Status=1 AND (
                    i.SubmittedByUserId IS NULL OR i.SubmittedAtUtc IS NULL
                    OR ISNULL(TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_CASE_ID')),
                        '00000000-0000-0000-0000-000000000000')<>i.Id))))
            THROW 51981, 'RCV_OPTIONAL_APPROVAL_MODE_INVALID: direct completion requires confirmed absent approval and retained submission identity.', 1;
        """;

    private const string CompletionIdentityGuard = """
        SET NOCOUNT ON;
        -- RCV_COMPLETION_IDENTITY: direct completion is not a human approval.
        IF EXISTS (
            SELECT 1 FROM inserted action
            JOIN ProcurementReceiptInspectionCases c ON c.Id=action.InspectionCaseId AND c.TenantId=action.TenantId
            WHERE (action.ActionType=3 AND c.ApprovalRequired=0)
               OR (action.ActionType=15 AND (c.ApprovalRequired<>0 OR c.SubmittedByUserId IS NULL
                   OR action.ActorUserId<>c.SubmittedByUserId
                   OR ISNULL(TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_CASE_ID')),
                       '00000000-0000-0000-0000-000000000000')<>c.Id)))
            THROW 51982, 'RCV_COMPLETION_IDENTITY_INVALID: direct completion must retain the actual submitter without human approval metadata.', 1;
        """;

}
