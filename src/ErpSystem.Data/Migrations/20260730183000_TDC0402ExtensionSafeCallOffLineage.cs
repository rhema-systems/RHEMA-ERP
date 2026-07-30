using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260730183000_TDC0402ExtensionSafeCallOffLineage")]
public partial class TDC0402ExtensionSafeCallOffLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @definition nvarchar(max) =
                OBJECT_DEFINITION(
                    OBJECT_ID(N'[TR_ProcurementFrameworkCallOffs_Lifecycle]'));
            IF @definition IS NULL
                THROW 51219, 'The framework source-consistency trigger is unavailable.', 1;

            IF CHARINDEX(
                    N'AND i.AgreementEffectiveEndUtc',
                    @definition) = 0
            BEGIN
                DECLARE @guardStart int =
                    CHARINDEX(
                        N'OR i.AgreementEffectiveEndUtc',
                        @definition);
                DECLARE @guardEnd int =
                    CHARINDEX(
                        N'OR i.AuthorityKind',
                        @definition,
                        @guardStart + 1);
                IF @guardStart = 0 OR @guardEnd = 0 OR @guardEnd <= @guardStart
                    THROW 51219, 'The framework end-date lineage guard could not be upgraded safely.', 1;

                DECLARE @replacement nvarchar(max) =
                    N'OR (prior.Id IS NULL
                                AND i.AgreementEffectiveEndUtc
                                     <> CASE
                                         WHEN extension.ApprovedEndUtc > agreement.EffectiveToUtc
                                             THEN extension.ApprovedEndUtc
                                         ELSE agreement.EffectiveToUtc
                                        END)';
                SET @definition = STUFF(
                    @definition,
                    @guardStart,
                    @guardEnd - @guardStart,
                    @replacement);

                DECLARE @triggerKeywordPosition int =
                    CHARINDEX(N'TRIGGER', UPPER(@definition));
                IF @triggerKeywordPosition = 0
                    THROW 51219, 'The framework source-consistency trigger declaration could not be altered safely.', 1;
                SET @definition =
                    STUFF(
                        @definition,
                        1,
                        @triggerKeywordPosition - 1,
                        N'CREATE OR ALTER ');
                EXEC sys.sp_executesql @definition;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Retain extension-safe creation-time lineage so a downgrade cannot
        // strand existing call-offs after an approved agreement extension.
    }
}
