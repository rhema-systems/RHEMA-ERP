using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912234000_InventoryDisposalOptionalApprovalAndDrafts")]
public sealed class InventoryDisposalOptionalApprovalAndDrafts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("ApprovalRequired", "InventoryDisposalCases", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.DropCheckConstraint("CK_InventoryDisposalCases_Status", "InventoryDisposalCases");
        migrationBuilder.AddCheckConstraint("CK_InventoryDisposalCases_Status", "InventoryDisposalCases", "[Status] BETWEEN 1 AND 11");
        migrationBuilder.Sql(CaseGuard);
        migrationBuilder.Sql(LineGuard);
        PatchAdjustmentApprovalGuard(migrationBuilder);
    }

    // There is no safe automatic downgrade after direct execution or draft editing:
    // doing so would misrepresent retained no-approval and superseded-line history.
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("THROW 51997, 'Inventory disposal history requires a reviewed forward migration; automatic downgrade is not supported.', 1;");

    public const string LineGuard = """
CREATE OR ALTER TRIGGER [dbo].[TR_InventoryDisposalLines_Immutable]
ON [dbo].[InventoryDisposalLines]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
        THROW 51104, 'INV_DISPOSAL_LINE_DELETE_PROHIBITED', 1;
    IF EXISTS (SELECT 1 FROM inserted i
        LEFT JOIN dbo.InventoryDisposalCases c ON c.Id=i.InventoryDisposalCaseId AND c.TenantId=i.TenantId
        LEFT JOIN dbo.InventoryItems p ON p.Id=i.InventoryItemId AND p.TenantId=i.TenantId
        LEFT JOIN dbo.WarehouseLocations l ON l.Id=i.LocationId AND l.TenantId=i.TenantId
            AND ((l.IsConsignmentBin=1 AND l.ConsignmentWarehouseId=c.WarehouseId)
                OR (l.IsConsignmentBin=0 AND l.WarehouseId=c.WarehouseId))
        WHERE c.Id IS NULL OR c.Status<>1 OR c.IsDeleted=1 OR p.Id IS NULL OR l.Id IS NULL)
        THROW 51105, 'INV_DISPOSAL_LINE_TENANT_LOCATION_OR_DRAFT_INVALID', 1;
    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
        WHERE i.TenantId<>d.TenantId OR i.InventoryDisposalCaseId<>d.InventoryDisposalCaseId
            OR i.InventoryItemId<>d.InventoryItemId OR i.LocationId<>d.LocationId
            OR i.Quantity<>d.Quantity OR i.UnitCost<>d.UnitCost OR i.TotalValue<>d.TotalValue
            OR ISNULL(i.LotNumber,'')<>ISNULL(d.LotNumber,'')
            OR ISNULL(i.BatchNumber,'')<>ISNULL(d.BatchNumber,'')
            OR ISNULL(i.SerialNumber,'')<>ISNULL(d.SerialNumber,'')
            OR d.IsDeleted=1 OR i.IsDeleted<>1)
        THROW 51104, 'INV_DISPOSAL_LINE_IMMUTABLE', 1;
END;
""";

    public const string CaseGuard = """
CREATE OR ALTER TRIGGER [dbo].[TR_InventoryDisposalCases_Guard]
ON [dbo].[InventoryDisposalCases]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
        THROW 51109, 'INV_DISPOSAL_CASE_DELETE_PROHIBITED', 1;
    IF EXISTS (SELECT 1 FROM inserted i
        LEFT JOIN dbo.Warehouses w ON w.Id=i.WarehouseId AND w.TenantId=i.TenantId
        LEFT JOIN dbo.Users u ON u.Id=i.RequestedById AND u.TenantId=i.TenantId
        LEFT JOIN dbo.StockAdjustments a ON a.Id=i.StockAdjustmentId AND a.TenantId=i.TenantId
        WHERE w.Id IS NULL OR u.Id IS NULL OR (i.StockAdjustmentId IS NOT NULL AND a.Id IS NULL))
        THROW 51110, 'INV_DISPOSAL_CASE_TENANT_LINEAGE_INVALID', 1;
    IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
        WHERE d.Id IS NULL AND (i.Status<>1 OR i.ApprovalRequired<>1 OR i.AuditVerifiedById IS NOT NULL
            OR i.WorkflowInstanceId IS NOT NULL OR i.ApprovedById IS NOT NULL OR i.StockAdjustmentId IS NOT NULL OR i.CompletedById IS NOT NULL))
        THROW 51111, 'INV_DISPOSAL_CASE_INITIAL_STATE_INVALID', 1;
    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
        WHERE i.TenantId<>d.TenantId OR i.DisposalNumber<>d.DisposalNumber OR i.WarehouseId<>d.WarehouseId
            OR i.RequestedById<>d.RequestedById OR i.RequestedAtUtc<>d.RequestedAtUtc
            OR i.IdempotencyKey<>d.IdempotencyKey OR i.PayloadHash<>d.PayloadHash OR i.CorrelationId<>d.CorrelationId OR i.IsDeleted<>d.IsDeleted
            OR (NOT(d.Status=1 AND i.Status=1) AND (i.Method<>d.Method OR i.Reason<>d.Reason
                OR i.IdentificationDetails<>d.IdentificationDetails OR i.TotalQuantity<>d.TotalQuantity OR i.TotalValue<>d.TotalValue))
            OR (i.ApprovalRequired<>d.ApprovalRequired AND NOT(d.Status IN (1,2,3,4) AND i.Status=11
                AND d.ApprovalRequired=1 AND i.ApprovalRequired=0
                AND d.WorkflowInstanceId IS NULL AND d.ApprovedById IS NULL
                AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'InventoryDisposal',i.Id)=0)))
        THROW 51112, 'INV_DISPOSAL_CASE_CORE_IMMUTABLE', 1;
    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
        WHERE i.Status<>d.Status AND NOT (
               (d.Status=1 AND i.Status IN (2,5,9,10,11))
            OR (d.Status=2 AND i.Status IN (3,5,10,11))
            OR (d.Status=3 AND i.Status IN (4,5,9,10,11))
            OR (d.Status=4 AND i.Status IN (5,10,11))
            OR (d.Status=5 AND i.Status IN (6,9,10))
            OR (d.Status IN (6,11) AND i.Status IN (7,10))
            OR (d.Status=7 AND i.Status=8)))
        THROW 51113, 'INV_DISPOSAL_CASE_TRANSITION_INVALID', 1;
    IF EXISTS (SELECT 1 FROM inserted WHERE Status=5 AND (WorkflowInstanceId IS NULL OR ApprovalRequired<>1))
        THROW 51114, 'INV_DISPOSAL_WORKFLOW_LINEAGE_REQUIRED', 1;
    IF EXISTS (SELECT 1 FROM inserted WHERE
        (ApprovalRequired=1 AND Status IN (6,7,8) AND (WorkflowInstanceId IS NULL OR ApprovedById IS NULL OR ApprovedAtUtc IS NULL))
        OR (Status=11 AND ApprovalRequired<>0)
        OR (ApprovalRequired=0 AND (Status NOT IN (7,8,10,11) OR WorkflowInstanceId IS NOT NULL OR ApprovedById IS NOT NULL OR ApprovedAtUtc IS NOT NULL)))
        THROW 51115, 'INV_DISPOSAL_APPROVAL_LINEAGE_REQUIRED', 1;
    IF EXISTS (SELECT 1 FROM inserted WHERE Status IN (7,8) AND (StockAdjustmentId IS NULL OR ExecutionReference IS NULL))
        THROW 51116, 'INV_DISPOSAL_ADJUSTMENT_LINEAGE_REQUIRED', 1;
    IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN dbo.StockAdjustments a ON a.Id=i.StockAdjustmentId AND a.TenantId=i.TenantId
        WHERE i.Status=8 AND (i.CompletedById IS NULL OR i.CompletedAtUtc IS NULL OR a.Status<>'Posted'
            OR (i.Method IN (1,2) AND (i.ProceedsAmount<=0 OR i.ProceedsAccountId IS NULL OR i.BuyerOrRecipient IS NULL
                OR i.ProceedsPostingEventId IS NULL OR i.ProceedsJournalEntryId IS NULL))
            OR (i.Method NOT IN (1,2) AND i.ProceedsAmount<>0)))
        THROW 51117, 'INV_DISPOSAL_COMPLETION_LINEAGE_INVALID', 1;
END;
""";

    private static void PatchAdjustmentApprovalGuard(MigrationBuilder migrationBuilder)
    {
        static string Literal(string value) => value.Replace("'", "''", StringComparison.Ordinal);
        const string before = "dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'StockAdjustment',i.Id)=0";
        migrationBuilder.Sql($$"""
DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_StockAdjustments_ControlledLifecycle',N'TR'));
DECLARE @before nvarchar(max)=N'{{Literal(before)}}';
DECLARE @after nvarchar(max)=N'{{Literal(AdjustmentApprovalDelegation)}}';
SET @definition=REPLACE(@definition,CHAR(13),N'');
SET @before=REPLACE(@before,CHAR(13),N'');
SET @after=REPLACE(@after,CHAR(13),N'');
IF @definition IS NULL OR (LEN(@definition)-LEN(REPLACE(@definition,@before,N'')))/NULLIF(LEN(@before),0)<>1
    THROW 51998, 'INV_DISPOSAL_ADJUSTMENT_GUARD_DRIFT: expected one protected stock-adjustment policy predicate.', 1;
SET @definition=REPLACE(@definition,@before,@after);
SET @definition=STUFF(@definition,1,CHARINDEX(N'TRIGGER',UPPER(@definition))-1,N'CREATE OR ALTER ');
EXEC sys.sp_executesql @definition;
""");
    }

    // A ready disposal owns the durable decision. Its generated adjustment must
    // match that source exactly, not merely carry a user-entered reference/prefix.
    // Standalone adjustments and retained in-flight routes keep their own policy.
    public const string AdjustmentApprovalDelegation = """
(dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'StockAdjustment',i.Id)=0
 OR EXISTS (
    SELECT 1 FROM dbo.InventoryDisposalCases c
    WHERE c.TenantId=i.TenantId AND c.IsDeleted=0 AND c.StockAdjustmentId=i.Id
      AND c.ApprovalRequired=0 AND c.Status IN (7,11)
      AND c.WorkflowInstanceId IS NULL AND c.ApprovedById IS NULL AND c.ApprovedAtUtc IS NULL
      AND c.WarehouseId=i.WarehouseId AND c.DisposalNumber=i.Reference
      AND i.IdempotencyKey=N'disposal:'+LOWER(REPLACE(CONVERT(nvarchar(36),c.Id),N'-',N''))+N':adjustment'
      AND i.ReasonCode=CASE WHEN c.Method=4 THEN N'DONATION' ELSE N'WRITE_OFF' END
      AND NOT EXISTS (
          SELECT 1 FROM dbo.WorkflowInstances w
          JOIN dbo.WorkflowEntityTypes e ON e.Id=w.EntityTypeId AND e.TenantId=w.TenantId
          WHERE w.TenantId=i.TenantId AND w.IsDeleted=0 AND w.Status IN (0,1,5,6)
            AND ((w.EntityId=i.Id AND (dbo.WorkflowApprovalEntityKey(e.Code)=N'STOCKADJUSTMENT'
                                      OR dbo.WorkflowApprovalEntityKey(e.Name)=N'STOCKADJUSTMENT'))
              OR (w.EntityId=c.Id AND (dbo.WorkflowApprovalEntityKey(e.Code)=N'INVENTORYDISPOSAL'
                                      OR dbo.WorkflowApprovalEntityKey(e.Name)=N'INVENTORYDISPOSAL'))))
      AND EXISTS (SELECT 1 FROM dbo.InventoryDisposalLines l
          WHERE l.TenantId=c.TenantId AND l.InventoryDisposalCaseId=c.Id AND l.IsDeleted=0)
      AND NOT EXISTS (
          SELECT 1 FROM dbo.StockAdjustmentItems a
          WHERE a.TenantId=i.TenantId AND a.AdjustmentId=i.Id AND a.IsDeleted=0
            AND 1<>(SELECT COUNT(*) FROM dbo.InventoryDisposalLines l
                WHERE l.TenantId=c.TenantId AND l.InventoryDisposalCaseId=c.Id AND l.IsDeleted=0
                  AND l.InventoryItemId=a.InventoryItemId AND l.LocationId=a.LocationId
                  AND l.Quantity=-a.AdjustmentQuantity
                  AND ISNULL(l.LotNumber,N'')=ISNULL(a.LotNumber,N'')
                  AND ISNULL(l.BatchNumber,N'')=ISNULL(a.BatchNumber,N'')
                  AND ISNULL(l.SerialNumber,N'')=ISNULL(a.SerialNumber,N'')))
      AND NOT EXISTS (
          SELECT 1 FROM dbo.InventoryDisposalLines l
          WHERE l.TenantId=c.TenantId AND l.InventoryDisposalCaseId=c.Id AND l.IsDeleted=0
            AND 1<>(SELECT COUNT(*) FROM dbo.StockAdjustmentItems a
                WHERE a.TenantId=i.TenantId AND a.AdjustmentId=i.Id AND a.IsDeleted=0
                  AND a.InventoryItemId=l.InventoryItemId AND a.LocationId=l.LocationId
                  AND a.AdjustmentQuantity=-l.Quantity
                  AND ISNULL(a.LotNumber,N'')=ISNULL(l.LotNumber,N'')
                  AND ISNULL(a.BatchNumber,N'')=ISNULL(l.BatchNumber,N'')
                  AND ISNULL(a.SerialNumber,N'')=ISNULL(l.SerialNumber,N'')))))
""";
}
