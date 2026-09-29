using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 5, lane E5: cancelling a leave plan records when and why.
    /// </summary>
    /// <remarks>
    /// <para><b>Two nullable columns on <c>LeavePlans</c></b>, mirroring <c>LeaveRequests</c>:
    /// <c>CancellationDate</c> (datetime2) and <c>CancellationReason</c> (nvarchar(1000)). Who
    /// cancelled is <c>UpdatedBy</c>, as on the request.</para>
    ///
    /// <para>Cancelling used to take no reason and leave nothing but the status. The reason is now
    /// required when HR cancels an APPROVED plan — the one cancel that takes back something agreed —
    /// and optional otherwise.</para>
    ///
    /// <para><b>No data step.</b> Plans cancelled before this keep a NULL date and reason: the old
    /// cancel recorded neither, and inventing them (from <c>UpdatedAt</c>, say) would claim a
    /// precision nobody had.</para>
    ///
    /// <para>⚠ Guarded SQL, as on every HR migration: <c>rebuild-db</c> and the UAT builder create
    /// the schema from the EF model, so a rebuilt database already has both columns.</para>
    /// </remarks>
    public partial class AddLeavePlanCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.LeavePlans', 'CancellationDate') IS NULL
    ALTER TABLE [dbo].[LeavePlans] ADD [CancellationDate] datetime2 NULL;
IF COL_LENGTH('dbo.LeavePlans', 'CancellationReason') IS NULL
    ALTER TABLE [dbo].[LeavePlans] ADD [CancellationReason] nvarchar(1000) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.LeavePlans', 'CancellationReason') IS NOT NULL
    ALTER TABLE [dbo].[LeavePlans] DROP COLUMN [CancellationReason];
IF COL_LENGTH('dbo.LeavePlans', 'CancellationDate') IS NOT NULL
    ALTER TABLE [dbo].[LeavePlans] DROP COLUMN [CancellationDate];");
        }
    }
}
