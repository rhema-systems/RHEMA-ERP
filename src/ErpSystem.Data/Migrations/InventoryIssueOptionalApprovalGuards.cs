using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

internal static class InventoryIssueOptionalApprovalGuards
{
    // Preserve the existing Finance-binding and actual-receipt controls; make
    // retained approver comparisons null-safe and add the direct-lifecycle gate.
    public static void Install(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(Patch(false));
    public static void Uninstall(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(Patch(true));

    internal static string Patch(bool reverse)
    {
        var replacements = new (string Before, string After)[]
        {
            ("i.ApprovedById <> d.ApprovedById",
                "ISNULL(i.ApprovedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ApprovedById, '00000000-0000-0000-0000-000000000000')"),
            ("i.ApprovedById = d.ApprovedById",
                "ISNULL(i.ApprovedById, '00000000-0000-0000-0000-000000000000') = ISNULL(d.ApprovedById, '00000000-0000-0000-0000-000000000000')"),
            ("r.ApprovedById <> i.ApprovedById",
                "ISNULL(r.ApprovedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(i.ApprovedById, '00000000-0000-0000-0000-000000000000')"),
            ("VALUES (i.RequestedById),(i.ApprovedById),(i.IssuedById),(i.ReceiverUserId)",
                "VALUES (i.RequestedById),(COALESCE(i.ApprovedById, i.RequestedById)),(i.IssuedById),(i.ReceiverUserId)"),
            ("SET NOCOUNT ON;", "SET NOCOUNT ON;\n" + OptionalApprovalInsertGuard)
        };
        static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
        var sql = "DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_InventoryIssueVouchers_ControlledLifecycle',N'TR'));\n" +
            "IF @definition IS NULL THROW 52070, 'INV_ISSUE_APPROVAL_TRIGGER_MISSING: expected the existing issue lifecycle guard.', 1;\n" +
            "SET @definition=REPLACE(@definition,CHAR(13),N'');\n";
        if (reverse)
            sql += "IF EXISTS (SELECT 1 FROM dbo.InventoryIssueVouchers WHERE ApprovedById IS NULL) THROW 52071, 'INV_ISSUE_APPROVAL_ROLLBACK_BLOCKED: preserve direct-lifecycle issue evidence.', 1;\n";
        foreach (var (before, after) in reverse ? replacements.Reverse() : replacements)
        {
            var source = reverse ? after : before;
            var destination = reverse ? before : after;
            sql += $"IF (LEN(@definition)-LEN(REPLACE(@definition,{Literal(source)},N'')))/LEN({Literal(source)})<>1 THROW 52072, 'INV_ISSUE_APPROVAL_TRIGGER_DRIFT: expected one known lifecycle predicate.', 1;\n";
            sql += $"SET @definition=REPLACE(@definition,{Literal(source)},{Literal(destination)});\n";
        }
        return sql + "SET @definition=STUFF(@definition,1,CHARINDEX(N'TRIGGER',UPPER(@definition))-1,N'CREATE OR ALTER ');\nEXEC sys.sp_executesql @definition;";
    }

    internal const string OptionalApprovalInsertGuard = """
        -- INV_ISSUE_OPTIONAL_APPROVAL_START
        IF EXISTS (
            SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
            WHERE d.Id IS NULL AND i.ApprovedById IS NULL AND (
                COALESCE(JSON_VALUE(CASE WHEN ISJSON(i.SourceSnapshotJson)=1 THEN i.SourceSnapshotJson ELSE N'{}' END,
                    '$.ApprovalRequired'),N'')<>N'false'
                OR EXISTS (
                    SELECT 1 FROM dbo.WorkflowInstances w
                    WHERE w.TenantId=i.TenantId AND w.EntityId=i.InventoryRequisitionId
                        AND w.IsDeleted=0 AND w.Status IN (0,1,5,6))
                OR EXISTS (
                    SELECT 1 FROM dbo.WorkflowDefinitions w
                    JOIN dbo.WorkflowEntityTypes t ON t.Id=w.EntityTypeId AND t.TenantId=w.TenantId
                    WHERE w.TenantId=i.TenantId AND w.IsDeleted=0 AND w.IsActive=1 AND w.LifecycleStatus=1
                        AND t.IsDeleted=0 AND t.IsActive=1
                        AND (UPPER(REPLACE(REPLACE(REPLACE(t.Code,N'_',N''),N' ',N''),N'-',N''))=N'INVENTORYREQUISITION'
                            OR UPPER(REPLACE(REPLACE(REPLACE(t.Name,N'_',N''),N' ',N''),N'-',N''))=N'INVENTORYREQUISITION'))))
            THROW 52073, 'INV_ISSUE_APPROVAL_REQUIRED: complete the configured workflow before issuing stock.', 1;
        -- INV_ISSUE_OPTIONAL_APPROVAL_END
        """;
}
