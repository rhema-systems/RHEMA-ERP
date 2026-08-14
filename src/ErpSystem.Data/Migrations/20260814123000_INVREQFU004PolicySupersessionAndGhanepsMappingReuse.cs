using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Allows an independently approved supplier review to be explicitly superseded when its
/// immutable DEC-011 decision has been replaced, and versions manual GHANEPS exchange evidence
/// by the immutable mapping definition rather than by the containing profile revision.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260814123000_INVREQFU004PolicySupersessionAndGhanepsMappingReuse")]
public partial class INVREQFU004PolicySupersessionAndGhanepsMappingReuse : Migration
{
    private const string ExistingTransition = "(d.Status = 1 AND i.Status IN (2, 3))";
    private const string SupersessionTransition = "(d.Status = 1 AND i.Status IN (2, 3, 5))";
    private const string OldIndex =
        "IX_ProcurementGhanepsExchangeEvents_TenantId_SourceType_SourceId_EventFamily_Direction_MappingKey_EventReference";
    private const string NewIndex = "UX_ProcGhanepsEvent_Source_MappingHash";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(AlterDueDiligenceGateSql(
            ExistingTransition, SupersessionTransition));
        migrationBuilder.DropIndex(
            name: OldIndex,
            table: "ProcurementGhanepsExchangeEvents");
        migrationBuilder.CreateIndex(
            name: NewIndex,
            table: "ProcurementGhanepsExchangeEvents",
            columns:
            [
                "TenantId", "SourceType", "SourceId", "EventFamily", "Direction",
                "MappingKey", "EventReference", "MappingIntegrityHash"
            ],
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: NewIndex,
            table: "ProcurementGhanepsExchangeEvents");
        migrationBuilder.CreateIndex(
            name: OldIndex,
            table: "ProcurementGhanepsExchangeEvents",
            columns:
            [
                "TenantId", "SourceType", "SourceId", "EventFamily", "Direction",
                "MappingKey", "EventReference"
            ],
            unique: true);
        migrationBuilder.Sql(AlterDueDiligenceGateSql(
            SupersessionTransition, ExistingTransition));
    }

    private static string AlterDueDiligenceGateSql(string from, string to) => $$"""
        DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(
            OBJECT_ID(N'[dbo].[TR_ProcurementSupplierDueDiligenceReviews_Lifecycle]', N'TR'));

        IF @definition IS NULL
            THROW 51845, 'SUPPLIER_DUE_DILIGENCE_TRIGGER_MISSING: the governed lifecycle trigger is required.', 1;

        IF CHARINDEX(N'{{to.Replace("'", "''")}}', @definition) = 0
        BEGIN
            IF CHARINDEX(N'{{from.Replace("'", "''")}}', @definition) = 0
                THROW 51846, 'SUPPLIER_DUE_DILIGENCE_TRIGGER_DRIFT: the lifecycle transition gate is not recognized.', 1;

            SET @definition = REPLACE(
                @definition,
                N'{{from.Replace("'", "''")}}',
                N'{{to.Replace("'", "''")}}');
            DECLARE @triggerKeyword int = CHARINDEX(N'TRIGGER', UPPER(@definition));
            IF @triggerKeyword = 0
                THROW 51846, 'SUPPLIER_DUE_DILIGENCE_TRIGGER_DRIFT: the lifecycle trigger definition is invalid.', 1;
            SET @definition = N'CREATE OR ALTER ' +
                SUBSTRING(@definition, @triggerKeyword, LEN(@definition));
            EXEC sys.sp_executesql @definition;
        END;
        """;
}
