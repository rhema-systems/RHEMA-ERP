using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 5, lane D3: coming back from leave. The employee reports it, and their manager or HR
    /// confirms it, which closes the leave.
    /// </summary>
    /// <remarks>
    /// <para><b>Five nullable columns on <c>LeaveRequests</c></b>:</para>
    /// <list type="bullet">
    ///   <item><c>ResumptionDate</c> (date): the first day back, as reported or as the confirmer set it;</item>
    ///   <item><c>ResumptionReportedDate</c> (datetime2) and <c>ResumptionReportedById</c>
    ///   (uniqueidentifier): when the employee reported it, and who;</item>
    ///   <item><c>ClosureConfirmedById</c> (uniqueidentifier): who confirmed the return;</item>
    ///   <item><c>OverstayDays</c> (int): working days away after the employee was due back.</item>
    /// </list>
    ///
    /// <para><b>No foreign keys</b> on the two actor columns, like every actor column on this table
    /// (an unpaired navigation to Employee mints a shadow column). <b>No data step</b>: leave closed
    /// before this has no report, no confirmer and no overstay, and inventing them would claim facts
    /// nobody recorded.</para>
    ///
    /// <para>⚠ Guarded SQL, as on every HR migration: <c>rebuild-db</c> and the UAT builder create
    /// the schema from the EF model, so a rebuilt database already has all five columns.</para>
    /// </remarks>
    public partial class AddLeaveResumption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.LeaveRequests', 'ResumptionDate') IS NULL
    ALTER TABLE [dbo].[LeaveRequests] ADD [ResumptionDate] date NULL;
IF COL_LENGTH('dbo.LeaveRequests', 'ResumptionReportedDate') IS NULL
    ALTER TABLE [dbo].[LeaveRequests] ADD [ResumptionReportedDate] datetime2 NULL;
IF COL_LENGTH('dbo.LeaveRequests', 'ResumptionReportedById') IS NULL
    ALTER TABLE [dbo].[LeaveRequests] ADD [ResumptionReportedById] uniqueidentifier NULL;
IF COL_LENGTH('dbo.LeaveRequests', 'ClosureConfirmedById') IS NULL
    ALTER TABLE [dbo].[LeaveRequests] ADD [ClosureConfirmedById] uniqueidentifier NULL;
IF COL_LENGTH('dbo.LeaveRequests', 'OverstayDays') IS NULL
    ALTER TABLE [dbo].[LeaveRequests] ADD [OverstayDays] int NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.LeaveRequests', 'OverstayDays') IS NOT NULL
    ALTER TABLE [dbo].[LeaveRequests] DROP COLUMN [OverstayDays];
IF COL_LENGTH('dbo.LeaveRequests', 'ClosureConfirmedById') IS NOT NULL
    ALTER TABLE [dbo].[LeaveRequests] DROP COLUMN [ClosureConfirmedById];
IF COL_LENGTH('dbo.LeaveRequests', 'ResumptionReportedById') IS NOT NULL
    ALTER TABLE [dbo].[LeaveRequests] DROP COLUMN [ResumptionReportedById];
IF COL_LENGTH('dbo.LeaveRequests', 'ResumptionReportedDate') IS NOT NULL
    ALTER TABLE [dbo].[LeaveRequests] DROP COLUMN [ResumptionReportedDate];
IF COL_LENGTH('dbo.LeaveRequests', 'ResumptionDate') IS NOT NULL
    ALTER TABLE [dbo].[LeaveRequests] DROP COLUMN [ResumptionDate];");
        }
    }
}
