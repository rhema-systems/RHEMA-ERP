using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 9c slice 7 — the three employee-relations reminder clocks move out of code and into
    /// <c>CompanyHrPolicySettings</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why they are settings at all.</b> <c>GrievanceRungChaseDays</c> was a <c>const</c> in
    /// <c>DisciplineReminderService</c>. FR-HR-181 names the escalation route and sets <b>no time
    /// limit at any rung</b>, so five days is OUR assumption — raised with TDC in
    /// <c>docs/HR/programme/HR-OPEN-QUESTIONS-FOR-TDC.md</c> §2 and still unanswered. As a constant, their answer
    /// would have cost a code change and a deploy. It now costs a settings edit.
    /// </para>
    /// <para>
    /// <b>⚠ THE SCAFFOLD'S DEFAULTS WERE WRONG AND ARE CORRECTED HERE.</b> EF generated
    /// <c>defaultValue: 0</c> for all three columns and then a single <c>UpdateData</c> fixing only
    /// the seeded DEFAULT-tenant row. Every OTHER tenant's settings row would have kept <b>0</b> —
    /// and zero is not a harmless default here, it is the worst possible value: a chase threshold of
    /// 0 makes <c>ReportedAt &lt;= today.AddDays(0)</c> true for everything, so every unanswered
    /// rung and every new concern becomes overdue the instant it is created. The first sweep on such
    /// a tenant would fire on the entire back catalogue at once — the flood that
    /// <c>BacklogHorizonDays</c> exists to prevent, arriving through the front door.
    /// </para>
    /// <para>
    /// So the defaults below are the real values, and there is a corrective <c>UPDATE</c> for any row
    /// that already holds 0. Same shape as slice 1's <c>CaseType DEFAULT (1)</c>: <b>the default is
    /// load-bearing, not cosmetic.</b>
    /// </para>
    /// <para>
    /// <b>Why 3 days for a concern and 5 for a rung.</b> Every other clock in this engine belongs to
    /// somebody who can chase it themselves. A whistleblower cannot — walking into an office to ask
    /// about their report is precisely the act that would identify them. If HR does not look,
    /// nothing happens and nobody ever knows.
    /// </para>
    /// </remarks>
    public partial class AddEmployeeRelationsReminderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠ Real defaults, not 0. See the remarks — 0 means "everything is already overdue".
            foreach (var (column, defaultValue) in new[]
            {
                ("GrievanceRungChaseDays", 5),
                ("ConcernTriageChaseDays", 3),
                ("GrievanceAgreementChaseDays", 14),
            })
            {
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.CompanyHrPolicySettings', '{column}') IS NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings]
        ADD [{column}] int NOT NULL
        CONSTRAINT [DF_CompanyHrPolicySettings_{column}] DEFAULT ({defaultValue});");

                // Corrective, and idempotent: catches a row that already took the scaffold's 0 —
                // whether from an earlier run of this migration or a rebuild-db from the EF model
                // before this file was fixed. A tenant left on 0 would be chased on everything.
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.CompanyHrPolicySettings', '{column}') IS NOT NULL
    UPDATE [dbo].[CompanyHrPolicySettings] SET [{column}] = {defaultValue} WHERE [{column}] = 0;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping these does not stop the sweeps — it puts the thresholds back in code, where
            // TDC's answer to open question §2 would once again cost a deploy.
            foreach (var column in new[]
            {
                "GrievanceRungChaseDays", "ConcernTriageChaseDays", "GrievanceAgreementChaseDays",
            })
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_CompanyHrPolicySettings_{column}')
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP CONSTRAINT [DF_CompanyHrPolicySettings_{column}];

IF COL_LENGTH('dbo.CompanyHrPolicySettings', '{column}') IS NOT NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP COLUMN [{column}];");
            }
        }
    }
}
