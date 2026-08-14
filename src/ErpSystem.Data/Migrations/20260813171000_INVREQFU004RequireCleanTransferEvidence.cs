using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Keeps the central DMS as the sole evidence owner while ensuring transfer discrepancy
/// evidence cannot be linked until the repository upload has passed malware scanning.
/// </summary>
public partial class INVREQFU004RequireCleanTransferEvidence : Migration
{
    private const string ExistingGate =
        "OR dv.FileUploadRecordId <> i.FileUploadRecordId OR dv.Status <> N'Published' OR dv.PublishedAt IS NULL";
    private const string CleanScanGate =
        "OR dv.FileUploadRecordId <> i.FileUploadRecordId OR f.VirusScanStatus <> 2 OR dv.Status <> N'Published' OR dv.PublishedAt IS NULL";

    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(AlterGateSql(ExistingGate, CleanScanGate));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(AlterGateSql(CleanScanGate, ExistingGate));

    private static string AlterGateSql(string from, string to) => $$"""
        DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(
            OBJECT_ID(N'[dbo].[TR_InventoryTransferDiscrepancyEvidence_AppendOnly]', N'TR'));

        IF @definition IS NULL
            THROW 51843, 'INV_TRANSFER_EVIDENCE_TRIGGER_MISSING: the governed transfer-evidence trigger is required.', 1;

        IF CHARINDEX(N'{{to.Replace("'", "''")}}', @definition) = 0
        BEGIN
            IF CHARINDEX(N'{{from.Replace("'", "''")}}', @definition) = 0
                THROW 51844, 'INV_TRANSFER_EVIDENCE_TRIGGER_DRIFT: the transfer-evidence scan gate does not match the governed definition.', 1;

            SET @definition = REPLACE(@definition, N'{{from.Replace("'", "''")}}', N'{{to.Replace("'", "''")}}');
            DECLARE @triggerKeyword int = CHARINDEX(N'TRIGGER', UPPER(@definition));
            IF @triggerKeyword = 0
                THROW 51844, 'INV_TRANSFER_EVIDENCE_TRIGGER_DRIFT: the transfer-evidence trigger definition is invalid.', 1;
            SET @definition = N'CREATE OR ALTER ' + SUBSTRING(@definition, @triggerKeyword, LEN(@definition));
            EXEC sys.sp_executesql @definition;
        END;
        """;
}
