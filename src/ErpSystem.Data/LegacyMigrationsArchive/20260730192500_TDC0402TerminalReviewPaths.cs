using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ErpSystem.Data.ApplicationDbContext))]
[Migration("20260730192500_TDC0402TerminalReviewPaths")]
public partial class TDC0402TerminalReviewPaths : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @callOffDefinition nvarchar(max) =
                OBJECT_DEFINITION(
                    OBJECT_ID(N'[TR_ProcurementFrameworkCallOffs_Lifecycle]'));
            IF @callOffDefinition IS NULL
                THROW 51220, 'The framework call-off lifecycle trigger is unavailable.', 1;

            IF CHARINDEX(
                    N'prior.Id IS NULL OR i.Status NOT IN (4, 5)',
                    @callOffDefinition) = 0
            BEGIN
                IF CHARINDEX(
                        N'OR requisition.Status <> ''Approved''',
                        @callOffDefinition) = 0
                    THROW 51220, 'The framework call-off terminal guard could not be upgraded safely.', 1;

                SET @callOffDefinition = REPLACE(
                    @callOffDefinition,
                    N'OR requisition.Status <> ''Approved''',
                    N'OR ((prior.Id IS NULL OR i.Status NOT IN (4, 5))
                               AND requisition.Status <> ''Approved'')');

                DECLARE @callOffTriggerKeyword int =
                    CHARINDEX(N'TRIGGER', UPPER(@callOffDefinition));
                IF @callOffTriggerKeyword = 0
                    THROW 51220, 'The framework call-off trigger declaration could not be altered safely.', 1;
                SET @callOffDefinition = STUFF(
                    @callOffDefinition,
                    1,
                    @callOffTriggerKeyword - 1,
                    N'CREATE OR ALTER ');
                EXEC sys.sp_executesql @callOffDefinition;
            END;

            DECLARE @extensionDefinition nvarchar(max) =
                OBJECT_DEFINITION(
                    OBJECT_ID(N'[TR_ProcurementFrameworkAgreementExtensions_Lifecycle]'));
            IF @extensionDefinition IS NULL
                THROW 51221, 'The framework extension lifecycle trigger is unavailable.', 1;

            IF CHARINDEX(
                    N'prior.Id IS NULL OR i.Status = 1',
                    @extensionDefinition) = 0
            BEGIN
                DECLARE @agreementJoin nvarchar(200) =
                    N'LEFT JOIN [ProcurementFrameworkAgreements] agreement';
                DECLARE @agreementJoinPosition int =
                    CHARINDEX(@agreementJoin, @extensionDefinition);
                IF @agreementJoinPosition = 0 OR CHARINDEX(
                        N'WHERE agreement.Id IS NULL OR agreement.Status <> 2',
                        @extensionDefinition) = 0
                    THROW 51221, 'The framework extension terminal guard could not be upgraded safely.', 1;

                SET @extensionDefinition = STUFF(
                    @extensionDefinition,
                    @agreementJoinPosition,
                    0,
                    N'LEFT JOIN deleted prior ON prior.Id = i.Id
                        ');
                SET @extensionDefinition = REPLACE(
                    @extensionDefinition,
                    N'WHERE agreement.Id IS NULL OR agreement.Status <> 2',
                    N'WHERE agreement.Id IS NULL
                           OR ((prior.Id IS NULL OR i.Status = 1)
                               AND agreement.Status <> 2)');

                DECLARE @extensionTriggerKeyword int =
                    CHARINDEX(N'TRIGGER', UPPER(@extensionDefinition));
                IF @extensionTriggerKeyword = 0
                    THROW 51221, 'The framework extension trigger declaration could not be altered safely.', 1;
                SET @extensionDefinition = STUFF(
                    @extensionDefinition,
                    1,
                    @extensionTriggerKeyword - 1,
                    N'CREATE OR ALTER ');
                EXEC sys.sp_executesql @extensionDefinition;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Retain terminal rejection/cancellation paths so a downgrade cannot
        // strand already-submitted call-offs or extension requests.
    }
}
