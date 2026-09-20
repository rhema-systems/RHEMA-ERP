using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds a server-controlled approval decision without reclassifying historical records.
/// All existing stock, Finance, evidence, tenant and mutation guards remain in place.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912120000_InventoryOptionalApprovalSnapshots")]
public sealed class InventoryOptionalApprovalSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "PhysicalCounts", "StockAdjustments", "InventoryReturnVouchers" })
            migrationBuilder.AddColumn<bool>("ApprovalRequired", table, "bit", nullable: false, defaultValue: true);

        Patch(migrationBuilder, "TR_StockAdjustments_ControlledLifecycle", "SET NOCOUNT ON;", """
            SET NOCOUNT ON;
            -- Approval requirement may be decided only once, by the initial submit transition.
            IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                WHERE (d.Id IS NULL AND i.ApprovalRequired=0)
                   OR (d.Id IS NOT NULL AND i.ApprovalRequired<>d.ApprovalRequired AND NOT (
                        d.ApprovalRequired=1 AND i.ApprovalRequired=0 AND d.Status=N'Draft' AND i.Status=N'ReadyToPost'
                        AND d.WorkflowInstanceId IS NULL AND d.ApprovedById IS NULL AND d.ApprovedAt IS NULL
                        AND i.SubmittedById IS NOT NULL AND i.SubmittedAtUtc IS NOT NULL
                        AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'StockAdjustment',i.Id)=0))
                   OR (i.ApprovalRequired=0 AND (i.WorkflowInstanceId IS NOT NULL OR i.ApprovedById IS NOT NULL
                        OR i.ApprovedAt IS NOT NULL OR i.Status NOT IN (N'ReadyToPost',N'Posted',N'Reversed',N'Cancelled'))))
                THROW 51971, 'INV_APPROVAL_SNAPSHOT_INVALID: the server must retain the initial approval decision and must not invent an approver.', 1;
            """);
        Patch(migrationBuilder, "TR_StockAdjustments_ControlledLifecycle",
            "(d.Status = N'Draft' AND i.Status = N'PendingApproval'",
            """
            (d.Status = N'Draft' AND i.Status = N'ReadyToPost' AND i.ApprovalRequired=0
             AND i.SubmittedById IS NOT NULL AND i.SubmittedAtUtc IS NOT NULL)
             OR (d.Status = N'Draft' AND i.Status = N'PendingApproval'
            """);
        Patch(migrationBuilder, "TR_StockAdjustments_ControlledLifecycle",
            "(d.Status = N'Approved' AND i.Status = N'Posted'",
            "((d.Status = N'Approved' AND i.ApprovalRequired=1 OR d.Status=N'ReadyToPost' AND i.ApprovalRequired=0) AND i.Status = N'Posted'");
        Patch(migrationBuilder, "TR_StockAdjustments_ControlledLifecycle",
            "AND i.PostedById <> i.RequestedById", "AND (i.ApprovalRequired=0 OR i.PostedById <> i.RequestedById)");
        Patch(migrationBuilder, "TR_StockAdjustments_ControlledLifecycle",
            "N'Draft', N'PendingApproval', N'Approved', N'Rejected', N'Posted', N'Reversed', N'Cancelled'",
            "N'Draft', N'PendingApproval', N'Approved', N'Rejected', N'Posted', N'Reversed', N'Cancelled', N'ReadyToPost'");
        Patch(migrationBuilder, "TR_StockAdjustmentActions_AppendOnly",
            "i.ActionType = N'Submitted' AND a.Status <> N'PendingApproval'",
            "i.ActionType = N'Submitted' AND NOT (a.Status=N'PendingApproval' AND a.ApprovalRequired=1 OR a.Status=N'ReadyToPost' AND a.ApprovalRequired=0 AND i.ActorUserId=a.SubmittedById)");
        Patch(migrationBuilder, "TR_StockAdjustmentActions_AppendOnly",
            "OR i.ActionType NOT IN (N'Created'",
            """
            OR (i.ActionType=N'ApprovalNotRequired' AND (a.ApprovalRequired<>0 OR a.Status<>N'ReadyToPost' OR i.ActorUserId<>a.SubmittedById))
            OR i.ActionType NOT IN (N'ApprovalNotRequired', N'Created'
            """);

        migrationBuilder.Sql("""
            ALTER TABLE dbo.InventoryReturnVouchers DROP CONSTRAINT CK_InventoryReturnVouchers_Status;
            ALTER TABLE dbo.InventoryReturnVouchers ADD CONSTRAINT CK_InventoryReturnVouchers_Status CHECK (Status BETWEEN 1 AND 6);
            """);
        Patch(migrationBuilder, "TR_InventoryReturnVouchers_ControlledLifecycle", "SET NOCOUNT ON;", """
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                WHERE (d.Id IS NULL AND i.ApprovalRequired=0)
                   OR (d.Id IS NOT NULL AND i.ApprovalRequired<>d.ApprovalRequired AND NOT (
                        d.ApprovalRequired=1 AND i.ApprovalRequired=0 AND d.Status=1 AND i.Status=6
                        AND d.WorkflowInstanceId IS NULL
                        AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'InventoryReturnVoucher',i.Id)=0
                        AND EXISTS(SELECT 1 FROM InventoryReturnVoucherActions a WHERE a.InventoryReturnVoucherId=i.Id
                            AND a.TenantId=i.TenantId AND a.ActionType=1 AND a.ActorUserId=i.RequestedById)))
                   OR (i.ApprovalRequired=0 AND (i.WorkflowInstanceId IS NOT NULL OR i.ApprovedById IS NOT NULL
                        OR i.ApprovedAtUtc IS NOT NULL OR i.Status NOT IN (4,5,6))))
                THROW 51972, 'INV_RETURN_APPROVAL_SNAPSHOT_INVALID: retain the initial approval decision and real human approval history.', 1;
            """);
        Patch(migrationBuilder, "TR_InventoryReturnVouchers_ControlledLifecycle",
            "(d.Status = 1 AND i.Status = 1",
            "(d.Status=1 AND i.Status=6 AND i.ApprovalRequired=0) OR (d.Status = 1 AND i.Status = 1");
        Patch(migrationBuilder, "TR_InventoryReturnVouchers_ControlledLifecycle",
            "(d.Status = 2 AND i.Status = 4", "((d.Status=2 AND i.ApprovalRequired=1 OR d.Status=6 AND i.ApprovalRequired=0) AND i.Status = 4");
        Patch(migrationBuilder, "TR_InventoryReturnVouchers_ControlledLifecycle",
            "AND i.PostedById <> i.RequestedById", "AND (i.ApprovalRequired=0 OR i.PostedById <> i.RequestedById)");
        Patch(migrationBuilder, "TR_InventoryReturnVoucherActions_AppendOnly",
            "OR i.ActionType NOT BETWEEN 1 AND 5", """
            OR (i.ActionType=6 AND (v.Status<>6 OR v.ApprovalRequired<>0 OR i.ActorUserId<>v.RequestedById))
            OR i.ActionType NOT BETWEEN 1 AND 6
            """);
        Patch(migrationBuilder, "TR_InventoryReturnVoucherLines_AppendOnly",
            "existingVoucher.Status IN (1,2", "existingVoucher.Status IN (1,2,6");

        migrationBuilder.Sql("""
            ALTER TABLE dbo.PhysicalCountActions DROP CONSTRAINT CK_PhysicalCountActions_ActionType;
            ALTER TABLE dbo.PhysicalCountActions ADD CONSTRAINT CK_PhysicalCountActions_ActionType CHECK (ActionType BETWEEN 1 AND 16);
            """);
        Patch(migrationBuilder, "TR_PhysicalCountActions_AppendOnly",
            "i.ActionType NOT BETWEEN 1 AND 15", "i.ActionType NOT BETWEEN 1 AND 16");
        Patch(migrationBuilder, "TR_PhysicalCounts_ControlledLifecycle", "SET NOCOUNT ON;", """
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                WHERE (d.Id IS NULL AND i.ApprovalRequired=0)
                   OR (d.Id IS NOT NULL AND i.ApprovalRequired<>d.ApprovalRequired AND NOT (
                        d.ApprovalRequired=1 AND i.ApprovalRequired=0 AND d.Status=N'UnderReview' AND i.Status=N'ReadyToPost'
                        AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'StockAdjustment',COALESCE(i.StockAdjustmentId,i.Id))=0
                        AND EXISTS(SELECT 1 FROM PhysicalCountActions a WHERE a.PhysicalCountId=i.Id AND a.TenantId=i.TenantId
                            AND a.ActionType=16 AND a.ActorUserId=i.CountedById
                            AND a.Sequence=(SELECT MAX(lastAction.Sequence) FROM PhysicalCountActions lastAction
                                WHERE lastAction.PhysicalCountId=i.Id AND lastAction.TenantId=i.TenantId))))
                   OR (i.ApprovalRequired=0 AND (i.ApprovedById IS NOT NULL OR i.StoresApprovedById IS NOT NULL
                        OR i.FinanceApprovedById IS NOT NULL OR i.AuditAttestedById IS NOT NULL
                        OR i.ApprovedDate IS NOT NULL OR i.StoresApprovedAtUtc IS NOT NULL OR i.FinanceApprovedAtUtc IS NOT NULL
                        OR i.AuditAttestedAtUtc IS NOT NULL OR i.Status NOT IN (N'ReadyToPost',N'Posted',N'Cancelled'))))
                THROW 51973, 'INV_COUNT_APPROVAL_SNAPSHOT_INVALID: an initial no-workflow decision must be audited without fabricating human approvals.', 1;
            """);
        Patch(migrationBuilder, "TR_PhysicalCounts_ControlledLifecycle",
            "WHERE i.Status <> d.Status AND NOT (", """
            WHERE i.Status <> d.Status AND NOT (
                (d.Status=N'UnderReview' AND i.Status=N'ReadyToPost' AND i.ApprovalRequired=0) OR
            """);
        Patch(migrationBuilder, "TR_PhysicalCounts_ControlledLifecycle", "AND i.PostedById=i.FinanceApprovedById",
            "AND i.PostedById IS NOT NULL AND (i.ApprovalRequired=0 OR i.PostedById=i.FinanceApprovedById)");
        Patch(migrationBuilder, "TR_PhysicalCounts_ControlledLifecycle",
            "WHERE (i.Status IN (N'PendingFinanceApproval'", "WHERE (i.ApprovalRequired=1 AND i.Status IN (N'PendingFinanceApproval'");
        Patch(migrationBuilder, "TR_PhysicalCounts_ControlledLifecycle",
            "OR (i.Status IN (N'PendingAuditAttestation'", "OR (i.ApprovalRequired=1 AND i.Status IN (N'PendingAuditAttestation'");
        Patch(migrationBuilder, "TR_PhysicalCounts_ControlledLifecycle",
            "OR (i.Status IN (N'ReadyToPost',N'Posted')", "OR (i.ApprovalRequired=1 AND i.Status IN (N'ReadyToPost',N'Posted')");
        Patch(migrationBuilder, "TR_PhysicalCounts_ControlledLifecycle",
            "i.TotalVarianceQuantity <> 0", "i.VarianceItems > 0");
        Patch(migrationBuilder, "TR_PhysicalCounts_ControlledLifecycle",
            "a.TenantId=i.TenantId AND a.Status=N'Approved'", "a.TenantId=i.TenantId AND ((i.ApprovalRequired=1 AND a.Status=N'Approved') OR (i.ApprovalRequired=0 AND a.ApprovalRequired=0 AND a.Status=N'ReadyToPost'))");
        Patch(migrationBuilder, "TR_StockMovements_PhysicalCountFreeze",
            "AND i.ProcessedById=p.FinanceApprovedById", "AND ((p.ApprovalRequired=1 AND i.ProcessedById=p.FinanceApprovedById) OR (p.ApprovalRequired=0 AND a.ApprovalRequired=0 AND i.ProcessedById=a.PostedById))");
    }

    private static void Patch(MigrationBuilder migrationBuilder, string trigger, string before, string after)
    {
        static string Literal(string value) => value.Replace("'", "''", StringComparison.Ordinal);
        migrationBuilder.Sql($$"""
            DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.{{trigger}}',N'TR'));
            IF @definition IS NULL OR CHARINDEX(N'{{Literal(before)}}',@definition)=0
                THROW 51974, 'INV_OPTIONAL_APPROVAL_GUARD_DRIFT: {{trigger}} does not match the expected protected definition.', 1;
            SET @definition=REPLACE(@definition,N'{{Literal(before)}}',N'{{Literal(after)}}');
            SET @definition=STUFF(@definition,1,CHARINDEX(N'TRIGGER',UPPER(@definition))-1,N'CREATE OR ALTER ');
            EXEC sys.sp_executesql @definition;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        THROW 51975, 'Optional approval snapshots retain auditable no-approval transitions. Restore the pre-change backup and matching application for rollback.', 1;
        """);
}
