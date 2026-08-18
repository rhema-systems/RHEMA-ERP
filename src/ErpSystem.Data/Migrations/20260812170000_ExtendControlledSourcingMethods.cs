using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Routes QBS/QCBS through the existing sealed tender lifecycle and petty purchases through the
/// existing governed noncompetitive lifecycle without creating parallel control tables.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260812170000_ExtendControlledSourcingMethods")]
public sealed class ExtendControlledSourcingMethods : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE [dbo].[ProcurementTenderControls]
                DROP CONSTRAINT [CK_ProcurementTenderControls_State];
            ALTER TABLE [dbo].[ProcurementTenderControls]
                ADD CONSTRAINT [CK_ProcurementTenderControls_State]
                CHECK ([Method] IN (1, 2, 7, 8) AND [Status] BETWEEN 0 AND 9
                    AND [DocumentFee] >= 0
                    AND [OpeningScheduledAtUtc] >= [SubmissionDeadlineUtc]
                    AND LEN([IntegrityHash]) = 64
                    AND ISJSON([LifecycleSnapshotJson]) = 1);

            ALTER TABLE [dbo].[ProcurementExceptionalSourcingControls]
                DROP CONSTRAINT [CK_ProcurementExceptionalSourcingControls_Core];
            ALTER TABLE [dbo].[ProcurementExceptionalSourcingControls]
                ADD CONSTRAINT [CK_ProcurementExceptionalSourcingControls_Core]
                CHECK ([Method] IN (3,4,5) AND [Status] BETWEEN 0 AND 9
                    AND ISJSON([SupplierSnapshotJson]) = 1
                    AND ISJSON([EvidenceChecklistJson]) = 1
                    AND ISJSON([ApprovalActorsJson]) = 1
                    AND LEN([IntegrityHash]) = 64);
            """);

        migrationBuilder.Sql("""
            DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_ProcurementTenderControls_Lifecycle]'));
            IF @definition IS NULL OR CHARINDEX(N'sc.[SelectedMethod] NOT IN (1, 2)', @definition) = 0
                THROW 51109, 'The controlled-tender lifecycle trigger is missing or has an unexpected method guard.', 1;
            SET @definition = REPLACE(@definition,
                N'sc.[SelectedMethod] NOT IN (1, 2)',
                N'sc.[SelectedMethod] NOT IN (1, 2, 7, 8)');
            DECLARE @triggerOffset int = CHARINDEX(N'TRIGGER', UPPER(@definition));
            IF @triggerOffset = 0
                THROW 51109, 'The controlled-tender lifecycle trigger definition is invalid.', 1;
            SET @definition = N'CREATE OR ALTER ' + SUBSTRING(@definition, @triggerOffset, LEN(@definition));
            EXEC sys.sp_executesql @definition;
            """, suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [dbo].[ProcurementTenderControls] WHERE [Method] IN (7,8))
                THROW 51108, 'Cannot remove QBS/QCBS controls while governed records exist.', 1;
            IF EXISTS (SELECT 1 FROM [dbo].[ProcurementExceptionalSourcingControls] WHERE [Method] = 5)
                THROW 51128, 'Cannot remove petty-purchase controls while governed records exist.', 1;

            ALTER TABLE [dbo].[ProcurementTenderControls]
                DROP CONSTRAINT [CK_ProcurementTenderControls_State];
            ALTER TABLE [dbo].[ProcurementTenderControls]
                ADD CONSTRAINT [CK_ProcurementTenderControls_State]
                CHECK ([Method] IN (1, 2) AND [Status] BETWEEN 0 AND 9
                    AND [DocumentFee] >= 0
                    AND [OpeningScheduledAtUtc] >= [SubmissionDeadlineUtc]
                    AND LEN([IntegrityHash]) = 64
                    AND ISJSON([LifecycleSnapshotJson]) = 1);

            ALTER TABLE [dbo].[ProcurementExceptionalSourcingControls]
                DROP CONSTRAINT [CK_ProcurementExceptionalSourcingControls_Core];
            ALTER TABLE [dbo].[ProcurementExceptionalSourcingControls]
                ADD CONSTRAINT [CK_ProcurementExceptionalSourcingControls_Core]
                CHECK ([Method] IN (3,4) AND [Status] BETWEEN 0 AND 9
                    AND ISJSON([SupplierSnapshotJson]) = 1
                    AND ISJSON([EvidenceChecklistJson]) = 1
                    AND ISJSON([ApprovalActorsJson]) = 1
                    AND LEN([IntegrityHash]) = 64);
            """);

        migrationBuilder.Sql("""
            DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_ProcurementTenderControls_Lifecycle]'));
            IF @definition IS NULL OR CHARINDEX(N'sc.[SelectedMethod] NOT IN (1, 2, 7, 8)', @definition) = 0
                THROW 51107, 'The controlled-tender lifecycle trigger is missing or has an unexpected method guard.', 1;
            SET @definition = REPLACE(@definition,
                N'sc.[SelectedMethod] NOT IN (1, 2, 7, 8)',
                N'sc.[SelectedMethod] NOT IN (1, 2)');
            DECLARE @triggerOffset int = CHARINDEX(N'TRIGGER', UPPER(@definition));
            IF @triggerOffset = 0
                THROW 51107, 'The controlled-tender lifecycle trigger definition is invalid.', 1;
            SET @definition = N'CREATE OR ALTER ' + SUBSTRING(@definition, @triggerOffset, LEN(@definition));
            EXEC sys.sp_executesql @definition;
            """, suppressTransaction: true);
    }
}
