using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 5, lane A: every leave type has a kind — Annual, Maternity or Other — and the kind
    /// replaces the <c>MandatoryAnnualLeave</c> flag, which only ever meant "this is the annual leave".
    /// </summary>
    /// <remarks>
    /// <para><b>Three steps, in this order, each its own command:</b></para>
    /// <list type="number">
    ///   <item><c>LeaveTypes.Category</c> (int, NOT NULL, default 0 = Other) is added.</item>
    ///   <item><b>The data step</b>, only while the flag still exists — so only once. Per tenant, ONE
    ///   type becomes Annual: <c>ANN</c> first, then an active flagged type, then the oldest; a
    ///   soft-deleted type never. One, because a tenant may have at most one active Annual type, and
    ///   harness runs have left several types flagged. <c>MAT</c> becomes Maternity.</item>
    ///   <item><c>MandatoryAnnualLeave</c> is dropped, its default constraint first if it has one.</item>
    /// </list>
    ///
    /// <para>⚠ The scaffold dropped the flag BEFORE adding the kind. On a database without an
    /// <c>ANN</c> code that would have lost the only record of which type was the annual one.</para>
    ///
    /// <para>⚠ The flag is read through <c>sp_executesql</c>. SQL Server resolves a missing COLUMN when
    /// it compiles a batch (only missing tables are deferred), so a plain reference would fail the
    /// whole batch on a second run, after the column is gone, even inside an <c>IF</c> that skips it.</para>
    ///
    /// <para><b>Down</b> puts the flag back on the Annual type only. Types that carried the flag
    /// without being chosen (harness leftovers) come back unflagged; that is the one thing Down cannot
    /// restore.</para>
    ///
    /// <para>Proven on a scratch database before it was applied: Up twice, Down twice, Up again, over
    /// four tenant shapes — ANN beside an older flagged type; no ANN, flagged types active and not; a
    /// soft-deleted ANN; ANN by its code alone with the flag off.</para>
    /// </remarks>
    public partial class AddLeaveTypeCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.LeaveTypes', 'Category') IS NULL
    ALTER TABLE dbo.LeaveTypes ADD Category int NOT NULL DEFAULT 0;");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.LeaveTypes', 'MandatoryAnnualLeave') IS NOT NULL
    EXEC sp_executesql N'
        WITH ranked AS (
            SELECT Id,
                   ROW_NUMBER() OVER (
                       PARTITION BY TenantId
                       ORDER BY CASE WHEN Code = ''ANN'' THEN 0 ELSE 1 END,
                                CASE WHEN IsActive = 1 THEN 0 ELSE 1 END,
                                CreatedAt, Id) AS rn
            FROM dbo.LeaveTypes
            WHERE IsDeleted = 0 AND (MandatoryAnnualLeave = 1 OR Code = ''ANN''))
        UPDATE lt SET Category = 1
        FROM dbo.LeaveTypes lt
        JOIN ranked r ON r.Id = lt.Id
        WHERE r.rn = 1 AND lt.Category = 0;

        UPDATE dbo.LeaveTypes SET Category = 2
        WHERE Code = ''MAT'' AND IsDeleted = 0 AND Category = 0;';");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.LeaveTypes', 'MandatoryAnnualLeave') IS NOT NULL
BEGIN
    DECLARE @default sysname;
    SELECT @default = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.LeaveTypes') AND c.name = 'MandatoryAnnualLeave';
    IF @default IS NOT NULL
    BEGIN
        DECLARE @drop nvarchar(400) = N'ALTER TABLE dbo.LeaveTypes DROP CONSTRAINT ' + QUOTENAME(@default);
        EXEC sp_executesql @drop;
    END
    ALTER TABLE dbo.LeaveTypes DROP COLUMN MandatoryAnnualLeave;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.LeaveTypes', 'MandatoryAnnualLeave') IS NULL
    ALTER TABLE dbo.LeaveTypes ADD MandatoryAnnualLeave bit NOT NULL DEFAULT CAST(0 AS bit);");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.LeaveTypes', 'Category') IS NOT NULL
    EXEC sp_executesql N'
        UPDATE dbo.LeaveTypes SET MandatoryAnnualLeave = 1
        WHERE Category = 1 AND MandatoryAnnualLeave = 0;';");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.LeaveTypes', 'Category') IS NOT NULL
BEGIN
    DECLARE @default sysname;
    SELECT @default = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.LeaveTypes') AND c.name = 'Category';
    IF @default IS NOT NULL
    BEGIN
        DECLARE @drop nvarchar(400) = N'ALTER TABLE dbo.LeaveTypes DROP CONSTRAINT ' + QUOTENAME(@default);
        EXEC sp_executesql @drop;
    END
    ALTER TABLE dbo.LeaveTypes DROP COLUMN Category;
END");
        }
    }
}
