using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Entitlement plan, slices W2b and W2c: the two year-end questions the product had been
    /// answering silently become settings on the leave type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>YearEndBasis</c></b> — what carry-over <i>and forfeiture</i> count as a person's unused
    /// days. <c>Granted</c> (0) reads <c>LeaveBalance.AvailableDays</c>, which is built on the whole
    /// year's <c>EntitledDays</c>, so somebody who joined in October and accrued 3.5 days carries the
    /// full cap. <c>Earned</c> (1) reads the accrued figure instead. Both are ordinary employer
    /// policy, which is why this is a choice and not a correction.
    /// </para>
    ///
    /// <para>
    /// <b><c>ProRateFirstYearEntitlement</c></b> — whether a joiner's first-year entitlement is
    /// scaled to the part of the year they were present for. ⚠ Refused alongside an incremental
    /// accrual policy, which already does that job; see the entity for why combining them deducts for
    /// the same months three times.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database already has these columns and a bare
    /// <c>AddColumn</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ Unlike <c>20260917203359_AddLeaveEncashmentPolicySettings</c>, the scaffold's DEFAULTS
    /// here are correct and are kept.</b> That migration needed six of them repaired because EF
    /// emitted <c>0</c> for columns whose real defaults were 22, 7, 2, 5, 9 and 30 — a zero divisor
    /// and a reminder engine chasing from "month 0" on every tenant that migrated rather than
    /// rebuilt. Here <c>0</c> genuinely <i>is</i> <c>LeaveYearEndBasis.Granted</c> and <c>false</c>
    /// genuinely is "do not pro-rate", and both are the behaviour that predates the setting. So a
    /// tenant that migrates and changes nothing behaves exactly as it did yesterday, which is the
    /// whole point of those defaults.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ No <c>UpdateData</c> for the seeded tenant, deliberately.</b> G2 needed one because the
    /// demo tenant had to differ from a fresh one. Neither of these does: the demonstration database
    /// should carry today's behaviour until somebody chooses otherwise, exactly like everybody else.
    /// </para>
    /// </remarks>
    public partial class AddLeaveYearEndBasisAndFirstYearProration : Migration
    {
        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        /// <remarks>
        /// The default constraint is looked up rather than named, so this works whether the column
        /// was created by this migration (named constraint) or by the EF model on a rebuilt database
        /// (server-generated name). Guessing the name would leave the column undroppable.
        /// </remarks>
        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @df_{table}_{column} sysname;
    SELECT @df_{table}_{column} = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @df_{table}_{column} IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @df_{table}_{column} + ']');
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";

        private const string LeaveTypes = "LeaveTypes";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AddColumn(LeaveTypes, "YearEndBasis",
                "int NOT NULL CONSTRAINT [DF_LeaveTypes_YearEndBasis] DEFAULT 0"));

            migrationBuilder.Sql(AddColumn(LeaveTypes, "ProRateFirstYearEntitlement",
                "bit NOT NULL CONSTRAINT [DF_LeaveTypes_ProRateFirstYearEntitlement] DEFAULT 0"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumn(LeaveTypes, "ProRateFirstYearEntitlement"));
            migrationBuilder.Sql(DropColumn(LeaveTypes, "YearEndBasis"));
        }
    }
}
