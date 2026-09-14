using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// <c>CompanyHrPolicySettings</c> gains the six figures the HR code had been carrying as
    /// constants because TDC has never answered them: FR-HR-177's written-query window, the
    /// employee's response window, FR-HR-178's investigation window, how long disciplinary
    /// reminders keep chasing, the days-per-year divisor behind a final settlement's daily rate,
    /// and whether approved leave counts as an expected day in the attendance rate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> Every one of these was a defensible assumption compiled into a service, so TDC's
    /// eventual answer cost a code change and a deploy. Each default below is <i>exactly</i> what
    /// the constant was, so adopting them changes no behaviour on any tenant — what changes is that
    /// the answer becomes an edit. Finish plan, lane 2a.
    /// </para>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced entirely, for three reasons — each of which only
    /// showed up when the previous one was fixed.</b>
    /// </para>
    /// <para>
    /// <b>1. It was not idempotent.</b> <c>rebuild-db</c> builds from the EF model rather than the
    /// migration chain, so a database rebuilt after the entity change already has all six columns
    /// and a bare <c>AddColumn</c> fails with "Column names in each table must be unique in each
    /// table". Each step is guarded on <c>COL_LENGTH</c>, so this is a no-op against a database
    /// already in the target shape and still does the work on one that is not. Same rewrite, same
    /// reason, as <c>20260831135847_AddAwardAttachmentDmsColumns</c> and its three siblings.
    /// </para>
    /// <para>
    /// <b>2. ⚠ Its defaults were zero, and zero is not a neutral value here.</b> The generated
    /// migration added every column with <c>defaultValue: 0</c> / <c>false</c> and then fixed only
    /// the SEEDED row by id. Any other tenant's settings row — and any row created by
    /// <c>CompanyHrPolicySettingsService</c> rather than by the seed — would have been left holding
    /// <c>WrittenQueryHours = 0</c> and <c>InvestigationDays = 0</c>, which makes every disciplinary
    /// case overdue the moment it is reported, and <c>SettlementDaysPerYear = 0</c>, which is a
    /// <b>divide by zero in the final-settlement daily rate</b>. The defaults below are the real
    /// figures, so SQL Server populates existing rows correctly on the ADD itself and no row anywhere
    /// depends on a follow-up UPDATE that only knows one id.
    /// </para>
    /// <para>
    /// <b>3. ⚠ Its <c>UpdateData</c> could not run at all.</b> The scaffolded seed fix failed with
    /// "There is no entity type mapped to the table 'CompanyHrPolicySettings' which is used in a
    /// data operation" — <c>UpdateData</c> resolves column types from the migration's TARGET MODEL,
    /// and the fast EF build (<c>TdcFastEfBuild</c>, the Debug default) removes every
    /// <c>*.Designer.cs</c> and the model snapshot, so no target model exists at runtime. It would
    /// have worked in Release and broken in Debug. <b>A data operation in a migration in this
    /// repository must be raw SQL.</b> The four sibling guarded migrations never met this because
    /// they add columns and nothing else.
    /// </para>
    /// <para>
    /// <b>Down drops the default constraint first.</b> SQL Server refuses <c>DROP COLUMN</c> while a
    /// default constraint references it, and the constraint's name is server-generated on a
    /// model-built database — so it is looked up in <c>sys.default_constraints</c> rather than
    /// guessed. The four sibling migrations never met this because their columns were nullable with
    /// no default at all.
    /// </para>
    /// </remarks>
    public partial class AddHrPolicyDeadlineSettings : Migration
    {
        private const string Table = "CompanyHrPolicySettings";

        /// <summary>
        /// Column, SQL type, and the default that MUST match the C# property initializer on
        /// <c>CompanyHrPolicySettings</c> and the <c>HasData</c> seed. Three places, one set of
        /// numbers: a tenant with no row, the seeded tenant, and a tenant whose row predates this
        /// migration all have to end up saying the same thing.
        /// </summary>
        private static readonly (string Column, string SqlType, string Default)[] Columns =
        {
            ("WrittenQueryHours",                   "int", "48"),
            ("QueryResponseWindowHours",            "int", "72"),
            ("InvestigationDays",                   "int", "28"),
            ("DisciplineBacklogHorizonDays",        "int", "90"),
            ("SettlementDaysPerYear",               "int", "365"),
            ("AttendanceRateIncludesApprovedLeave", "bit", "1"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (column, sqlType, @default) in Columns)
            {
                // Adding a NOT NULL column WITH a default populates every existing row with it, so
                // a tenant that already had a settings row keeps working with the same figures the
                // code used yesterday.
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{Table}]
        ADD [{column}] {sqlType} NOT NULL
        CONSTRAINT [DF_{Table}_{column}] DEFAULT ({@default});");
            }

            // ⚠ Not `UpdateData`. The scaffold generated one for the seeded row and it FAILED at
            // `database update` with "There is no entity type mapped to the table
            // 'CompanyHrPolicySettings' which is used in a data operation": UpdateData resolves its
            // column types from the migration's TARGET MODEL, and the fast EF build
            // (`TdcFastEfBuild`, the Debug default) strips every `*.Designer.cs` AND the model
            // snapshot, so no target model exists at runtime. The four sibling guarded migrations
            // never met this because they are pure SQL. **Any data operation in a migration in this
            // repository must be raw SQL, or it works in Release and breaks in Debug.**
            //
            // It is also redundant. Adding a NOT NULL column WITH a default populates every
            // existing row, so the seeded row already holds these values by the time this runs.
            // What is left is a repair for one case the defaults cannot reach: a database where
            // the columns were added by an EARLIER, broken version of this migration, which used
            // `defaultValue: 0`. Zero is not a legitimate value for any of them — the entity's
            // [Range] attributes start at 1 — so a zero can only be that damage, never a choice.
            foreach (var (column, _, @default) in Columns)
            {
                if (column == "AttendanceRateIncludesApprovedLeave") continue; // false is legitimate

                migrationBuilder.Sql($@"
UPDATE [dbo].[{Table}] SET [{column}] = {@default} WHERE [{column}] = 0;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (column, _, _) in Columns)
            {
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @constraint sysname;

    SELECT @constraint = dc.name
      FROM sys.default_constraints dc
      JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
     WHERE dc.parent_object_id = OBJECT_ID('dbo.{Table}')
       AND c.name = '{column}';

    IF @constraint IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{Table}] DROP CONSTRAINT [' + @constraint + ']');

    ALTER TABLE [dbo].[{Table}] DROP COLUMN [{column}];
END;");
            }
        }
    }
}
