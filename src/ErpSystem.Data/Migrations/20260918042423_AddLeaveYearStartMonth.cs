using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Entitlement plan C1, half two: the month a tenant's leave year begins.
    /// </summary>
    /// <remarks>
    /// <para>A leave year is <b>labelled by the calendar year it starts in</b> — with an April
    /// start, 15 March 2028 is in leave year 2027. That matches <c>HrFiscalYear</c>'s convention and
    /// is the only one under which <c>LeaveBalance.Year</c> can stay an <c>int</c>.</para>
    ///
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration
    /// since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has this column and a bare
    /// <c>AddColumn</c> stops the chain.</para>
    ///
    /// <para><b>⚠ AND THE SCAFFOLD'S DEFAULT WAS WRONG — for the third time in this module.</b> EF
    /// emitted <c>defaultValue: 0</c> and repaired only the seeded row with an <c>UpdateData</c>, so
    /// on a database that MIGRATED rather than rebuilt, every other tenant would have landed with
    /// <c>LeaveYearStartMonth = 0</c>. There is no month zero. The API's <c>[Range(1, 12)]</c> would
    /// then refuse every save of the settings screen until somebody noticed, and the screen would be
    /// showing a value it could not accept.</para>
    ///
    /// <para>This is exactly what <c>20260917203359_AddLeaveEncashmentPolicySettings</c> records for
    /// six of its columns, and what the seed block records for <c>WrittenQueryHours</c> before that.
    /// <b>The pattern is that EF's default for an <c>int</c> is zero and zero is almost never the
    /// real default.</b> The column below carries <b>1</b>, so a migrating tenant lands on January —
    /// the behaviour it already had — and the <c>UpdateData</c> is dropped as redundant rather than
    /// kept as a repair for a wound this no longer inflicts.</para>
    ///
    /// <para>⚠ <c>LeaveYear</c> treats any month outside 1–12 as January, so a row that somehow
    /// carries 0 still behaves correctly. That defence is deliberate and is <b>not</b> a reason to
    /// leave the default wrong: a value that must be guarded against itself is a value that was
    /// written wrong.</para>
    /// </remarks>
    public partial class AddLeaveYearStartMonth : Migration
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

        private const string Settings = "CompanyHrPolicySettings";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AddColumn(Settings, "LeaveYearStartMonth",
                "int NOT NULL CONSTRAINT [DF_CompanyHrPolicySettings_LeaveYearStartMonth] DEFAULT 1"));

            // ⚠ Repairs a row that a PREVIOUS run of the scaffolded version could have left at zero,
            // and rows created by the EF model on a rebuilt database before the default was fixed.
            // Month zero cannot be a deliberate choice — nobody can enter it through the API, which
            // ranges the field 1–12 — so there is no user intent here to overturn.
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Settings}', 'LeaveYearStartMonth') IS NOT NULL
    UPDATE [dbo].[{Settings}] SET [LeaveYearStartMonth] = 1
     WHERE [LeaveYearStartMonth] NOT BETWEEN 1 AND 12;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumn(Settings, "LeaveYearStartMonth"));
        }
    }
}
