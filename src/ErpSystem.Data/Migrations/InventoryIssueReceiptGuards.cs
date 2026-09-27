using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ErpSystem.Data.Migrations;

internal static class InventoryIssueReceiptGuards
{
    public static void Install(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(ParentLifecyclePatch);
        migrationBuilder.Sql(ActionGuard);
        migrationBuilder.Sql(ReceiptLineGuard);
    }

    public static void Uninstall(MigrationBuilder migrationBuilder)
    {
        // Never discard actual receipt evidence to make an older application fit.
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.InventoryIssueVoucherReceiptLines)
               OR EXISTS (SELECT 1 FROM dbo.InventoryIssueVouchers WHERE ReceiptSequence<>0)
               OR EXISTS (SELECT 1 FROM dbo.InventoryIssueVoucherActions
                   WHERE ActionType=3 OR ReceiptIdempotencyKey IS NOT NULL OR ReceiptPayloadHash IS NOT NULL)
                THROW 51634, 'INV_RECEIPT_ROLLBACK_BLOCKED: preserve actual receipt history before reverting this schema.', 1;
            """);

        // Restore only these two immutable baseline definitions, not the other module guards.
        var baseline = new MigrationBuilder(migrationBuilder.ActiveProvider);
        ArchivedGovernanceBaselineSql.Apply(baseline);
        foreach (var name in new[] { "TR_InventoryIssueVouchers_ControlledLifecycle", "TR_InventoryIssueVoucherActions_AppendOnly" })
        {
            var prefix = $"CREATE OR ALTER TRIGGER [dbo].[{name}]";
            var original = baseline.Operations.OfType<SqlOperation>().Single(operation =>
                operation.Sql.TrimStart().StartsWith(prefix, StringComparison.Ordinal));
            migrationBuilder.Sql(original.Sql);
        }
    }

    public const string ParentLifecyclePatch = """
        DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_InventoryIssueVouchers_ControlledLifecycle', N'TR'));
        DECLARE @before nvarchar(max) = N'(d.Status = 1 AND i.Status = 2
                     AND i.FinancePostingEventId = d.FinancePostingEventId AND i.FinanceJournalEntryId = d.FinanceJournalEntryId
                     AND d.AcknowledgedById IS NULL AND i.AcknowledgedById = i.ReceiverUserId
                     AND d.AcknowledgedAtUtc IS NULL AND i.AcknowledgedAtUtc IS NOT NULL
                     AND d.ReceiverComment IS NULL AND LEN(LTRIM(RTRIM(i.ReceiverComment))) > 0
                     AND i.IntegrityHash <> d.IntegrityHash AND i.UpdatedAt IS NOT NULL
                     AND i.LastModifiedById = i.ReceiverUserId)';
        DECLARE @after nvarchar(max) = N'(d.Status = 1 AND i.Status IN (1,2)
                     AND i.ReceiptSequence = d.ReceiptSequence + 1
                     AND ((i.FinancePostingEventId IS NULL AND d.FinancePostingEventId IS NULL)
                       OR (i.FinancePostingEventId IS NOT NULL AND d.FinancePostingEventId IS NOT NULL AND i.FinancePostingEventId=d.FinancePostingEventId))
                     AND ((i.FinanceJournalEntryId IS NULL AND d.FinanceJournalEntryId IS NULL)
                       OR (i.FinanceJournalEntryId IS NOT NULL AND d.FinanceJournalEntryId IS NOT NULL AND i.FinanceJournalEntryId=d.FinanceJournalEntryId))
                     AND i.IntegrityHash <> d.IntegrityHash AND i.UpdatedAt IS NOT NULL
                     AND i.LastModifiedById IS NOT NULL AND i.LastModifiedById = i.ReceiverUserId
                     AND i.ReceiverComment IS NOT NULL AND LEN(LTRIM(RTRIM(ISNULL(i.ReceiverComment,N'''')))) > 0
                     AND ((i.Status = 1 AND i.AcknowledgedById IS NULL AND i.AcknowledgedAtUtc IS NULL)
                       OR (i.Status = 2 AND i.AcknowledgedById IS NOT NULL AND i.AcknowledgedById = i.ReceiverUserId AND i.AcknowledgedAtUtc IS NOT NULL))
                     AND EXISTS (SELECT 1 FROM dbo.InventoryIssueVoucherActions a
                         WHERE a.TenantId=i.TenantId AND a.InventoryIssueVoucherId=i.Id AND a.IsDeleted=0
                           AND a.Sequence=i.ReceiptSequence+1 AND a.StatusAfter=i.Status
                           AND a.ActionType=CASE WHEN i.Status=2 THEN 2 ELSE 3 END
                           AND a.ActorUserId=i.ReceiverUserId AND a.ReceiptIdempotencyKey IS NOT NULL
                           AND a.ReceiptPayloadHash IS NOT NULL AND LEN(a.ReceiptPayloadHash)=64
                           AND a.Comment IS NOT NULL AND i.ReceiverComment IS NOT NULL AND a.Comment=i.ReceiverComment
                           AND (i.Status=1 OR (i.AcknowledgedAtUtc IS NOT NULL AND a.OccurredAtUtc=i.AcknowledgedAtUtc))
                           AND EXISTS (SELECT 1 FROM dbo.InventoryIssueVoucherReceiptLines r
                               WHERE r.TenantId=i.TenantId AND r.InventoryIssueVoucherActionId=a.Id AND r.IsDeleted=0))
                     AND i.Status = CASE WHEN EXISTS (
                         SELECT 1 FROM dbo.InventoryIssueVoucherLines l
                         WHERE l.TenantId=i.TenantId AND l.InventoryIssueVoucherId=i.Id AND l.IsDeleted=0
                           AND l.Quantity > (SELECT COALESCE(SUM(r.ReceivedQuantity),0)
                               FROM dbo.InventoryIssueVoucherReceiptLines r
                               WHERE r.TenantId=l.TenantId AND r.InventoryIssueVoucherLineId=l.Id AND r.IsDeleted=0))
                         THEN 1 ELSE 2 END)';
        -- @after is inside WHERE NOT(...): every nullable comparison must resolve to
        -- TRUE/FALSE, never UNKNOWN, or an invalid acknowledgement could evade rejection.
        -- Locate the complete known branch and tolerate formatting, not changed predicates.
        SET @definition=REPLACE(@definition,CHAR(13),N'');
        SET @before=REPLACE(@before,CHAR(13),N'');
        DECLARE @startMarker nvarchar(max)=N'(d.Status = 1 AND i.Status = 2';
        DECLARE @endMarker nvarchar(max)=N'AND i.LastModifiedById = i.ReceiverUserId)';
        DECLARE @branchStart int=CHARINDEX(@startMarker,@definition);
        DECLARE @branchEnd int=CHARINDEX(@endMarker,@definition,@branchStart);
        DECLARE @branchLength int=@branchEnd+LEN(@endMarker)-@branchStart;
        DECLARE @actual nvarchar(max)=SUBSTRING(@definition,@branchStart,CASE WHEN @branchLength>0 THEN @branchLength ELSE 0 END);
        IF @definition IS NULL OR @branchStart<=0 OR @branchEnd<=@branchStart
           OR CHARINDEX(@startMarker,@definition,@branchStart+1)>0
           OR REPLACE(REPLACE(REPLACE(@actual,N' ',N''),CHAR(10),N''),CHAR(9),N'') COLLATE Latin1_General_100_BIN2
             <> REPLACE(REPLACE(REPLACE(@before,N' ',N''),CHAR(10),N''),CHAR(9),N'') COLLATE Latin1_General_100_BIN2
            THROW 51630, 'INV_RECEIPT_TRIGGER_DRIFT: expected the governed voucher acknowledgement branch.', 1;
        SET @definition=STUFF(@definition,@branchStart,@branchLength,@after);
        DECLARE @financeBefore nvarchar(max)=N'AND i.Status = d.Status AND ISNULL(i.AcknowledgedById';
        IF (LEN(@definition)-LEN(REPLACE(@definition,@financeBefore,N'')))/LEN(@financeBefore)<>1
            THROW 51630, 'INV_RECEIPT_TRIGGER_DRIFT: expected the governed Finance binding branch.', 1;
        SET @definition=REPLACE(@definition,@financeBefore,N'AND i.ReceiptSequence = d.ReceiptSequence AND i.Status = d.Status AND ISNULL(i.AcknowledgedById');
        IF CHARINDEX(N'r.Status NOT IN (3,4,5,6)',@definition)=0
            THROW 51630, 'INV_RECEIPT_TRIGGER_DRIFT: expected the requisition source-state guard.', 1;
        SET @definition=REPLACE(@definition,N'r.Status NOT IN (3,4,5,6)',N'(r.Status NOT IN (3,4,5,6)
            AND NOT (r.Status=7 AND EXISTS (SELECT 1 FROM deleted prior
                WHERE prior.Id=i.Id AND prior.TenantId=i.TenantId AND prior.Status=1
                  AND i.Status IN (1,2) AND i.ReceiptSequence=prior.ReceiptSequence+1)))');
        DECLARE @bodyStart nvarchar(max)=N'SET NOCOUNT ON;';
        IF (LEN(@definition)-LEN(REPLACE(@definition,@bodyStart,N'')))/LEN(@bodyStart)<>1
            THROW 51630, 'INV_RECEIPT_TRIGGER_DRIFT: expected one governed voucher trigger body.', 1;
        SET @definition=REPLACE(@definition,@bodyStart,N'SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM inserted fresh LEFT JOIN deleted prior ON prior.Id=fresh.Id
                WHERE prior.Id IS NULL AND fresh.ReceiptSequence<>0)
                THROW 51633, ''INV_RECEIPT_INITIAL_SEQUENCE_INVALID: a new issue voucher must start without receipt history.'', 1;');
        SET @definition=STUFF(@definition,1,CHARINDEX(N'TRIGGER',UPPER(@definition))-1,N'CREATE OR ALTER ');
        EXEC sys.sp_executesql @definition;
        """;

    public const string ActionGuard = """
        CREATE OR ALTER TRIGGER [dbo].[TR_InventoryIssueVoucherActions_AppendOnly]
        ON [dbo].[InventoryIssueVoucherActions]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted)
                THROW 51621, 'INV_ISSUE_ACTION_IMMUTABLE: voucher actions cannot be changed or deleted.', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN dbo.InventoryIssueVouchers v ON v.Id=i.InventoryIssueVoucherId AND v.TenantId=i.TenantId AND v.IsDeleted=0
                WHERE v.Id IS NULL OR i.IsDeleted=1
                   OR (i.ActionType=1 AND (i.Sequence<>1 OR i.ActorUserId<>v.IssuedById OR i.StatusAfter<>1 OR v.Status<>1
                       OR i.ReceiptIdempotencyKey IS NOT NULL OR i.ReceiptPayloadHash IS NOT NULL))
                   OR (i.ActionType IN (2,3) AND (v.Status<>1 OR i.ActorUserId<>v.ReceiverUserId
                       OR i.Sequence<>v.ReceiptSequence+2
                       OR i.StatusAfter<>CASE WHEN i.ActionType=2 THEN 2 ELSE 1 END
                       OR NULLIF(LTRIM(RTRIM(i.ReceiptIdempotencyKey)),N'') IS NULL
                       OR i.ReceiptPayloadHash IS NULL OR LEN(i.ReceiptPayloadHash)<>64)))
                THROW 51622, 'INV_ISSUE_ACTION_INVALID: action actor, sequence and status must match the controlled voucher lifecycle.', 1;
        END
        """;

    public const string ReceiptLineGuard = """
        CREATE OR ALTER TRIGGER [dbo].[TR_InventoryIssueVoucherReceiptLines_AppendOnly]
        ON [dbo].[InventoryIssueVoucherReceiptLines]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted)
                THROW 51631, 'INV_RECEIPT_IMMUTABLE: actual receipt evidence cannot be changed or deleted.', 1;
            -- The service transaction atomically owns action, receipt rows and parent transition.
            -- SQL guards validate each statement; reads/replays reject unfinalized evidence.
            -- Lock the immutable issue evidence while checking cumulative quantities.
            DECLARE @locked int;
            SELECT @locked=COUNT(*) FROM dbo.InventoryIssueVoucherLines l WITH (UPDLOCK,HOLDLOCK)
            WHERE EXISTS (SELECT 1 FROM inserted i WHERE i.InventoryIssueVoucherLineId=l.Id);
            IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN dbo.InventoryIssueVoucherLines l ON l.Id=i.InventoryIssueVoucherLineId AND l.TenantId=i.TenantId AND l.IsDeleted=0
                LEFT JOIN dbo.InventoryIssueVoucherActions a ON a.Id=i.InventoryIssueVoucherActionId AND a.TenantId=i.TenantId AND a.IsDeleted=0
                LEFT JOIN dbo.InventoryIssueVouchers v ON v.Id=l.InventoryIssueVoucherId AND v.TenantId=i.TenantId AND v.IsDeleted=0
                WHERE l.Id IS NULL OR a.Id IS NULL OR v.Id IS NULL OR i.IsDeleted=1
                   OR a.InventoryIssueVoucherId<>v.Id OR a.ActorUserId<>v.ReceiverUserId
                   OR a.ActionType NOT IN (2,3) OR a.ReceiptIdempotencyKey IS NULL
                   OR v.Status<>1 OR a.Sequence<>v.ReceiptSequence+2
                   OR (NULLIF(l.SerialNumber,N'') IS NOT NULL AND i.ReceivedQuantity<>FLOOR(i.ReceivedQuantity))
                   OR l.Quantity < (SELECT COALESCE(SUM(r.ReceivedQuantity),0)
                       FROM dbo.InventoryIssueVoucherReceiptLines r
                       WHERE r.InventoryIssueVoucherLineId=l.Id AND r.TenantId=i.TenantId AND r.IsDeleted=0))
                THROW 51632, 'INV_RECEIPT_SOURCE_OR_QUANTITY_INVALID: receipt must match the designated receiver and original issue and cannot exceed issued quantity.', 1;
        END
        """;
}
