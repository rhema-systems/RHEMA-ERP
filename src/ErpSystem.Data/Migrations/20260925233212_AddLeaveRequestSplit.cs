using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 5, lane H (decision A5): casual leave beyond its limit. A request for more days than the
    /// type has left can ask for the extra days to be charged to annual leave, and at the final
    /// approval it becomes two linked requests.
    /// </summary>
    /// <remarks>
    /// <para><b>Three columns:</b></para>
    /// <list type="bullet">
    ///   <item><c>LeaveTypes.AllowOffsetAgainstAnnual</c> (bit, NOT NULL, default 0): the type may
    ///   send its excess to annual leave. HR switches it on per type; only Other kinds may carry it.</item>
    ///   <item><c>LeaveRequests.ChargeExcessToAnnual</c> (bit, NOT NULL, default 0): the employee (or
    ///   HR at the desk) asked for the days beyond the limit to be charged to annual leave.</item>
    ///   <item><c>LeaveRequests.SplitFromRequestId</c> (uniqueidentifier, nullable): set on the annual
    ///   part a split creates, pointing at the request it was split from. Indexed, and restricted to
    ///   <c>LeaveRequests</c> (no cascade: leave requests are soft-deleted).</item>
    /// </list>
    ///
    /// <para><b>No data step.</b> Nothing has been split before this, so every existing type is off,
    /// every existing request asked for nothing, and no request points at another. The defaults say
    /// exactly that.</para>
    ///
    /// <para>⚠ Guarded SQL, as on every HR migration: <c>rebuild-db</c> and the UAT builder create
    /// the schema from the EF model, so a rebuilt database already has all three columns, the index
    /// and the foreign key. The index and the key are in their own batch, because SQL Server compiles
    /// a batch before running it and could not name a column the same batch adds.</para>
    ///
    /// <para>Proven on a scratch database before it was applied: Up twice, Down twice, Up again, over
    /// existing rows in both tables.</para>
    /// </remarks>
    public partial class AddLeaveRequestSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.LeaveTypes', 'AllowOffsetAgainstAnnual') IS NULL
    ALTER TABLE [dbo].[LeaveTypes] ADD [AllowOffsetAgainstAnnual] bit NOT NULL DEFAULT CAST(0 AS bit);
IF COL_LENGTH('dbo.LeaveRequests', 'ChargeExcessToAnnual') IS NULL
    ALTER TABLE [dbo].[LeaveRequests] ADD [ChargeExcessToAnnual] bit NOT NULL DEFAULT CAST(0 AS bit);
IF COL_LENGTH('dbo.LeaveRequests', 'SplitFromRequestId') IS NULL
    ALTER TABLE [dbo].[LeaveRequests] ADD [SplitFromRequestId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_LeaveRequests_SplitFromRequestId'
                 AND object_id = OBJECT_ID('dbo.LeaveRequests'))
    CREATE INDEX [IX_LeaveRequests_SplitFromRequestId]
        ON [dbo].[LeaveRequests] ([SplitFromRequestId]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE name = 'FK_LeaveRequests_LeaveRequests_SplitFromRequestId')
    ALTER TABLE [dbo].[LeaveRequests]
        ADD CONSTRAINT [FK_LeaveRequests_LeaveRequests_SplitFromRequestId]
        FOREIGN KEY ([SplitFromRequestId]) REFERENCES [dbo].[LeaveRequests] ([Id]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ The link goes with the column: an annual part created by a split stays as an ordinary
            // annual request, no longer saying where it came from.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys
           WHERE name = 'FK_LeaveRequests_LeaveRequests_SplitFromRequestId')
    ALTER TABLE [dbo].[LeaveRequests]
        DROP CONSTRAINT [FK_LeaveRequests_LeaveRequests_SplitFromRequestId];

IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_LeaveRequests_SplitFromRequestId'
             AND object_id = OBJECT_ID('dbo.LeaveRequests'))
    DROP INDEX [IX_LeaveRequests_SplitFromRequestId] ON [dbo].[LeaveRequests];");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.LeaveRequests', 'SplitFromRequestId') IS NOT NULL
    ALTER TABLE [dbo].[LeaveRequests] DROP COLUMN [SplitFromRequestId];");

            migrationBuilder.Sql(DropBitColumnSql("LeaveRequests", "ChargeExcessToAnnual"));
            migrationBuilder.Sql(DropBitColumnSql("LeaveTypes", "AllowOffsetAgainstAnnual"));
        }

        /// <summary>
        /// Drops a column added with an unnamed default: the default constraint first (SQL Server
        /// named it), then the column.
        /// </summary>
        private static string DropBitColumnSql(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @default sysname;
    SELECT @default = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @default IS NOT NULL
    BEGIN
        DECLARE @drop nvarchar(400) = N'ALTER TABLE [dbo].[{table}] DROP CONSTRAINT ' + QUOTENAME(@default);
        EXEC sp_executesql @drop;
    END
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";
    }
}
