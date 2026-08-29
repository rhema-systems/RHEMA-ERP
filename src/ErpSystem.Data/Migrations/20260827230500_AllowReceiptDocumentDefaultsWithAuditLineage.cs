using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Keeps the receipt-document register tenant-safe and immutable while allowing
/// the application to resolve operational DEC-013 defaults for legacy tenant
/// profiles whose stored decision payload is empty or not yet published.
/// </summary>
public partial class AllowReceiptDocumentDefaultsWithAuditLineage : Migration
{
    private const string LegacyPublicationPredicate =
        "p.LifecycleStatus <> 1 OR c.Status <> 2 OR c.ApprovalStatus <> 1 OR " +
        "c.EvidenceStatus = 0 OR c.ValueJson <> i.DecisionSnapshotJson";

    protected override void Up(MigrationBuilder migrationBuilder) =>
        ReplacePublicationPredicate(migrationBuilder, LegacyPublicationPredicate, "1 = 0");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        ReplacePublicationPredicate(migrationBuilder, "1 = 0", LegacyPublicationPredicate);

    private static void ReplacePublicationPredicate(
        MigrationBuilder migrationBuilder,
        string fromPredicate,
        string toPredicate)
    {
        migrationBuilder.Sql($$"""
            DECLARE @definition nvarchar(max) =
                OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_ProcurementReceiptDocuments_TDC0509Protected]', N'TR'));
            IF @definition IS NULL
                THROW 51990, 'The protected receipt-document trigger is required before DEC-013 default alignment.', 1;

            DECLARE @fromPredicate nvarchar(max) = N'{{Escape(fromPredicate)}}';
            DECLARE @toPredicate nvarchar(max) = N'{{Escape(toPredicate)}}';
            DECLARE @matchCount int =
                (LEN(@definition) - LEN(REPLACE(@definition, @fromPredicate, N''))) /
                NULLIF(LEN(@fromPredicate), 0);
            IF @matchCount <> 1
                THROW 51991, 'The protected receipt-document trigger has drifted from the verified baseline.', 1;

            SET @definition = REPLACE(@definition, @fromPredicate, @toPredicate);
            DECLARE @triggerKeywordPosition int = CHARINDEX(N'TRIGGER', UPPER(@definition));
            IF @triggerKeywordPosition = 0
                THROW 51992, 'The protected receipt-document trigger declaration could not be altered safely.', 1;
            SET @definition = N'ALTER ' + SUBSTRING(
                @definition,
                @triggerKeywordPosition,
                LEN(@definition));
            EXEC sys.sp_executesql @definition;
            """);
    }

    private static string Escape(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
