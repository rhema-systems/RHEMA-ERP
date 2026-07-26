using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260714103000_AddSubledgerAdjustmentPurpose")]
    public partial class AddSubledgerAdjustmentPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[SubledgerAdjustmentJournals]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'[dbo].[SubledgerAdjustmentJournals]', N'Purpose') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[SubledgerAdjustmentJournals]
                        ADD [Purpose] nvarchar(30) NOT NULL
                            CONSTRAINT [DF_SubledgerAdjustmentJournals_Purpose] DEFAULT N'StandardAdjustment';
                END

                IF OBJECT_ID(N'[dbo].[SubledgerAdjustmentJournals]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM sys.indexes
                       WHERE name = N'IX_SubledgerAdjustmentJournals_TenantId_Module_Purpose_AdjustmentDate'
                         AND object_id = OBJECT_ID(N'[dbo].[SubledgerAdjustmentJournals]', N'U')
                   )
                BEGIN
                    CREATE INDEX [IX_SubledgerAdjustmentJournals_TenantId_Module_Purpose_AdjustmentDate]
                        ON [dbo].[SubledgerAdjustmentJournals] ([TenantId], [Module], [Purpose], [AdjustmentDate]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[SubledgerAdjustmentJournals]', N'U') IS NOT NULL
                   AND EXISTS (
                       SELECT 1
                       FROM sys.indexes
                       WHERE name = N'IX_SubledgerAdjustmentJournals_TenantId_Module_Purpose_AdjustmentDate'
                         AND object_id = OBJECT_ID(N'[dbo].[SubledgerAdjustmentJournals]', N'U')
                   )
                BEGIN
                    DROP INDEX [IX_SubledgerAdjustmentJournals_TenantId_Module_Purpose_AdjustmentDate]
                        ON [dbo].[SubledgerAdjustmentJournals];
                END

                IF OBJECT_ID(N'[dbo].[SubledgerAdjustmentJournals]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'[dbo].[SubledgerAdjustmentJournals]', N'Purpose') IS NOT NULL
                BEGIN
                    DECLARE @constraintName sysname;

                    SELECT @constraintName = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c
                        ON c.object_id = dc.parent_object_id
                       AND c.column_id = dc.parent_column_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[SubledgerAdjustmentJournals]', N'U')
                      AND c.name = N'Purpose';

                    IF @constraintName IS NOT NULL
                    BEGIN
                        DECLARE @sql nvarchar(max) =
                            N'ALTER TABLE [dbo].[SubledgerAdjustmentJournals] DROP CONSTRAINT [' + @constraintName + N']';
                        EXEC sp_executesql @sql;
                    END

                    ALTER TABLE [dbo].[SubledgerAdjustmentJournals] DROP COLUMN [Purpose];
                END
                """);
        }
    }
}
