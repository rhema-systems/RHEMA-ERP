using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907042000_AlignPettyPurchaseAwardReadinessAuthority")]
public sealed class AlignPettyPurchaseAwardReadinessAuthority : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(GuardSql);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Retained Petty award-readiness decisions require a reviewed forward-compatible rollback.");

    internal const string GuardSql = """
        DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_ProcurementAwardReadinessDecisions_Immutable'));
        DECLARE @anchor nvarchar(100) = N'OR (i.SourceType = 2 AND NOT EXISTS (';
        DECLARE @readyScope int = CHARINDEX(N'THROW 51406,', @definition);
        DECLARE @start int = CHARINDEX(@anchor, @definition, @readyScope);
        DECLARE @finish int = CHARINDEX(N'THROW 51407,', @definition, @start);
        IF @definition IS NULL OR @readyScope = 0 OR @start <= @readyScope OR @finish <= @start
          OR CHARINDEX(@anchor, @definition, @start + 1) > 0
          THROW 51420, 'Unexpected exceptional award-readiness guard; review the database definition before applying this migration.', 1;
        DECLARE @fragment nvarchar(max) = SUBSTRING(@definition, @start, @finish - @start);
        DECLARE @old nvarchar(100) = N'AND c.AuthorityRouteId = i.AuthorityRouteId';
        DECLARE @new nvarchar(500) = N'AND (c.AuthorityRouteId = i.AuthorityRouteId OR (c.Method = 5 AND c.AuthorityRouteId IS NULL AND i.AuthorityRouteId IS NULL))';
        IF CHARINDEX(@new, @fragment) > 0
        BEGIN
          IF CHARINDEX(@new, @fragment, CHARINDEX(@new, @fragment) + 1) > 0 OR CHARINDEX(@old, @fragment) > 0
            THROW 51420, 'The installed Petty authority guard is ambiguous.', 1;
          RETURN;
        END;
        IF CHARINDEX(@old, @fragment) = 0
          OR CHARINDEX(@old, @fragment, CHARINDEX(@old, @fragment) + 1) > 0
          THROW 51420, 'The expected exceptional authority comparison was not found exactly once.', 1;
        SET @fragment = REPLACE(@fragment, @old, @new);
        SET @definition = STUFF(@definition, @start, @finish - @start, @fragment);
        SET @definition = STUFF(@definition, 1, CHARINDEX(N'TRIGGER', UPPER(@definition)) - 1, N'ALTER ');
        EXEC sys.sp_executesql @definition;
        """;
}
