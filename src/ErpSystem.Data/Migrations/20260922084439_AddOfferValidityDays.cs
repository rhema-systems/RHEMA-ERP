using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, decision D-10: how long an offer stays open when HR sets no expiry.
    /// </summary>
    /// <remarks>
    /// <para><b>Why.</b> An offer previously arrived with <i>no</i> expiry unless somebody
    /// remembered to type one, and an offer with no expiry never lapses — it sits Issued
    /// indefinitely while the candidate takes another job and the vacancy stays notionally filled.
    /// The offer defaults endpoint now seeds <c>ExpiryDate</c> from this setting. It is a default,
    /// not a cap: HR may set any date they like.</para>
    ///
    /// <para><b>⚠ The scaffold wrote <c>defaultValue: 0</c>, and it had to be replaced.</b> That is
    /// the trap this module has now hit five times — EF's scaffolded default for a value type is
    /// zero, and zero is almost never the real default. Here it is worse than meaningless: zero
    /// days means <i>"this offer expired the day it was raised"</i>. The scaffold then patched it
    /// back to 14 with an <c>UpdateData</c> against <b>one hard-coded seeded row id</b>, so the
    /// defect was invisible on a database that has exactly that row and live on any other — a
    /// second tenant, or a settings row created by anything but the seeder, would have been left at
    /// zero. The entity carries <c>[Range(1, 3650)]</c>, so such a row would then have failed
    /// validation the next time anyone saved the settings screen, with nothing to explain why.</para>
    ///
    /// <para>So the column is added with a real <c>DEFAULT 14</c> — which is what gives <i>existing</i>
    /// rows a sane value — and a repair statement fixes any row already sitting at zero, on every
    /// tenant rather than one named id. Both are idempotent.</para>
    ///
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration
    /// since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has this column and a
    /// bare <c>AddColumn</c> stops the chain for everyone.</para>
    /// </remarks>
    public partial class AddOfferValidityDays : Migration
    {
        private const string Settings = "CompanyHrPolicySettings";
        private const string Column = "OfferValidityDays";
        private const int DefaultDays = 14;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Settings}', '{Column}') IS NULL
    ALTER TABLE [dbo].[{Settings}] ADD [{Column}] int NOT NULL
        CONSTRAINT [DF_{Settings}_{Column}] DEFAULT ({DefaultDays});");

            // Repairs a row that already holds zero — either because an earlier build of this
            // migration added the column with the scaffolded DEFAULT 0, or because a rebuilt
            // database created it from a model revision that had no initialiser. Keyed on the
            // VALUE, not on a seeded row id, so it covers every tenant.
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Settings}', '{Column}') IS NOT NULL
    UPDATE [dbo].[{Settings}] SET [{Column}] = {DefaultDays} WHERE [{Column}] <= 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Settings}', '{Column}') IS NOT NULL
BEGIN
    DECLARE @df sysname;
    SELECT @df = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{Settings}') AND c.name = '{Column}';
    IF @df IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{Settings}] DROP CONSTRAINT [' + @df + ']');
    ALTER TABLE [dbo].[{Settings}] DROP COLUMN [{Column}];
END");
        }
    }
}
