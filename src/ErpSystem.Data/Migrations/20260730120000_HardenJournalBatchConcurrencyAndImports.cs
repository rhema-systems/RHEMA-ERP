using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260730120000_HardenJournalBatchConcurrencyAndImports")]
public partial class HardenJournalBatchConcurrencyAndImports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_JournalBatches_TenantId_ReversalOfJournalBatchId",
            table: "JournalBatches");

        migrationBuilder.DropIndex(
            name: "IX_JournalBatchImportSessions_TenantId_PreviewToken",
            table: "JournalBatchImportSessions");

        migrationBuilder.AddColumn<bool>(
            name: "IsVoided",
            table: "JournalBatches",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "VoidedAt",
            table: "JournalBatches",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "VoidedByUserId",
            table: "JournalBatches",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "VoidReason",
            table: "JournalBatches",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "PostingClaimedAt",
            table: "JournalBatchItems",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "PostingClaimRunId",
            table: "JournalBatchItems",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.RenameColumn(
            name: "PreviewToken",
            table: "JournalBatchImportSessions",
            newName: "PreviewTokenHash");

        migrationBuilder.AddColumn<string>(
            name: "NormalizedPayloadHash",
            table: "JournalBatchImportSessions",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql(
            """
            UPDATE [JournalBatchImportSessions]
            SET [PreviewTokenHash] = CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varchar(100), [PreviewTokenHash])), 2),
                [NormalizedPayloadHash] = [FileHash];

            UPDATE [JournalBatches]
            SET [PostingStatus] =
                CASE
                    WHEN NOT EXISTS (
                        SELECT 1
                        FROM [JournalBatchItems] AS [i]
                        WHERE [i].[JournalBatchId] = [JournalBatches].[Id]
                          AND [i].[TenantId] = [JournalBatches].[TenantId]
                          AND [i].[IsDeleted] = 0
                          AND [i].[ReviewStatus] = N'Approved'
                          AND [i].[PostingStatus] <> N'Posted')
                    THEN N'Posted'
                    WHEN EXISTS (
                        SELECT 1
                        FROM [JournalBatchItems] AS [i]
                        WHERE [i].[JournalBatchId] = [JournalBatches].[Id]
                          AND [i].[TenantId] = [JournalBatches].[TenantId]
                          AND [i].[IsDeleted] = 0
                          AND [i].[ReviewStatus] = N'Approved'
                          AND [i].[PostingStatus] = N'Posted')
                    THEN N'PartiallyPosted'
                    ELSE N'Ready'
                END
            WHERE [PostingStatus] = N'PostingFailed';

            UPDATE [JournalBatches]
            SET [ReversalStatus] = N'ReversalPending'
            WHERE [ReversalStatus] = N'PartiallyReversed';
            """);

        migrationBuilder.AlterColumn<string>(
            name: "PreviewTokenHash",
            table: "JournalBatchImportSessions",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100);

        migrationBuilder.CreateIndex(
            name: "IX_JournalBatches_TenantId_ReversalOfJournalBatchId",
            table: "JournalBatches",
            columns: new[] { "TenantId", "ReversalOfJournalBatchId" },
            unique: true,
            filter: "[ReversalOfJournalBatchId] IS NOT NULL AND [IsDeleted] = 0 AND [IsVoided] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_JournalBatchImportSessions_TenantId_PreviewTokenHash",
            table: "JournalBatchImportSessions",
            columns: new[] { "TenantId", "PreviewTokenHash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_JournalBatchItems_TenantId_PostingClaimRunId",
            table: "JournalBatchItems",
            columns: new[] { "TenantId", "PostingClaimRunId" });

        migrationBuilder.AddForeignKey(
            name: "FK_JournalBatchItems_JournalBatchPostingRuns_PostingClaimRunId",
            table: "JournalBatchItems",
            column: "PostingClaimRunId",
            principalTable: "JournalBatchPostingRuns",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_JournalBatchItems_JournalBatchPostingRuns_PostingClaimRunId",
            table: "JournalBatchItems");

        migrationBuilder.DropIndex(
            name: "IX_JournalBatchItems_TenantId_PostingClaimRunId",
            table: "JournalBatchItems");

        migrationBuilder.DropIndex(
            name: "IX_JournalBatchImportSessions_TenantId_PreviewTokenHash",
            table: "JournalBatchImportSessions");

        migrationBuilder.DropIndex(
            name: "IX_JournalBatches_TenantId_ReversalOfJournalBatchId",
            table: "JournalBatches");

        migrationBuilder.Sql(
            """
            UPDATE [JournalBatchItems]
            SET [PostingStatus] = N'Ready'
            WHERE [PostingClaimRunId] IS NOT NULL
              AND [PostingStatus] = N'Posting';

            UPDATE [JournalBatches]
            SET [ApprovalStatus] = N'Rejected',
                [IsDeleted] = 1,
                [DeletedAt] = COALESCE([DeletedAt], SYSUTCDATETIME()),
                [DeletedBy] = COALESCE([DeletedBy], N'migration-down')
            WHERE [IsVoided] = 1
              AND [ReversalOfJournalBatchId] IS NOT NULL;
            """);

        migrationBuilder.DropColumn(
            name: "PostingClaimedAt",
            table: "JournalBatchItems");

        migrationBuilder.DropColumn(
            name: "PostingClaimRunId",
            table: "JournalBatchItems");

        migrationBuilder.DropColumn(
            name: "NormalizedPayloadHash",
            table: "JournalBatchImportSessions");

        migrationBuilder.AlterColumn<string>(
            name: "PreviewTokenHash",
            table: "JournalBatchImportSessions",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(64)",
            oldMaxLength: 64);

        migrationBuilder.RenameColumn(
            name: "PreviewTokenHash",
            table: "JournalBatchImportSessions",
            newName: "PreviewToken");

        migrationBuilder.DropColumn(
            name: "IsVoided",
            table: "JournalBatches");

        migrationBuilder.DropColumn(
            name: "VoidedAt",
            table: "JournalBatches");

        migrationBuilder.DropColumn(
            name: "VoidedByUserId",
            table: "JournalBatches");

        migrationBuilder.DropColumn(
            name: "VoidReason",
            table: "JournalBatches");

        migrationBuilder.CreateIndex(
            name: "IX_JournalBatches_TenantId_ReversalOfJournalBatchId",
            table: "JournalBatches",
            columns: new[] { "TenantId", "ReversalOfJournalBatchId" },
            unique: true,
            filter: "[ReversalOfJournalBatchId] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_JournalBatchImportSessions_TenantId_PreviewToken",
            table: "JournalBatchImportSessions",
            columns: new[] { "TenantId", "PreviewToken" },
            unique: true);
    }
}
