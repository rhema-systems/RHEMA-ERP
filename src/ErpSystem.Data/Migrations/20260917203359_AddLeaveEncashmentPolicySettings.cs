using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Leave residue plan, slice G2: the configuration turn. Four things that were hardcoded, or
    /// unanswerable without one client's ruling, become settings a client can change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database already has these columns and a bare
    /// <c>AddColumn</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ And the scaffold's DEFAULTS were wrong, which matters far more than the guarding.</b>
    /// EF emitted <c>defaultValue: 0</c> for all six integer columns and repaired only the single
    /// seeded row with an <c>UpdateData</c>. On a database migrated rather than rebuilt, <b>every
    /// other tenant's settings row</b> would have landed with:
    /// </para>
    /// <list type="bullet">
    ///   <item><c>EncashmentWorkingDaysPerMonth = 0</c> — a divisor of zero on every encashment
    ///   rate. The service guards it, but a setting that must be guarded against itself is a
    ///   setting that was written wrong.</item>
    ///   <item><c>MandatoryLeaveChaseFromMonth = 0</c> — "chase from month zero", so mandatory-leave
    ///   reminders fire all year instead of from September.</item>
    ///   <item><c>LeaveStartingReminderDays = 0</c> — leave announced on the morning it begins,
    ///   which is the one day the reminder is of no use to anybody.</item>
    /// </list>
    /// <para>
    /// This is precisely the failure the seed block in <c>ApplicationDbContext.HR.cs</c> already
    /// records for <c>WrittenQueryHours</c>: a zero that is silently wrong rather than loudly
    /// broken. So every column below carries its <b>real</b> default, and the seeded row's
    /// <c>UpdateData</c> is kept only for the one value that genuinely differs from it.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ <c>AllowInServiceEncashment</c> defaults to 0 and the seeded row is set to 1, on
    /// purpose.</b> FR-HR-046 says leave is encashed "only on exit, no other route", so a new tenant
    /// should start there — but the seeded tenant is the one the demo runs on and its encashment
    /// screen has already been shown. Defaulting it off without that row update would make a
    /// demonstrated feature disappear.
    /// </para>
    /// </remarks>
    public partial class AddLeaveEncashmentPolicySettings : Migration
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
        private const string Encashments = "LeaveEncashments";

        /// <summary>The seeded tenant's settings row — the one the demo runs on.</summary>
        private static readonly Guid SeededSettingsId = new("b2c3d4e5-0000-0000-0000-000000000001");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // How an encashed amount was arrived at, in words. The final settlement has recorded its
            // basis since FR-HR-184; leave does now too, because the two use deliberately different
            // divisors and either can be re-configured after a payout has been made.
            migrationBuilder.Sql(AddColumn(Encashments, "RateBasis", "nvarchar(500) NULL"));

            // ⚠ Settles a requirements conflict. Off is FR-HR-046's reading and the safe direction:
            // a tenant that should not be encashing in service simply cannot, rather than doing it
            // by default and being corrected later.
            migrationBuilder.Sql(AddColumn(Settings, "AllowInServiceEncashment",
                "bit NOT NULL CONSTRAINT [DF_CompanyHrPolicySettings_AllowInServiceEncashment] DEFAULT 0"));

            // ⚠ 22, NOT the scaffold's 0. This is a divisor.
            migrationBuilder.Sql(AddColumn(Settings, "EncashmentWorkingDaysPerMonth",
                "int NOT NULL CONSTRAINT [DF_CompanyHrPolicySettings_EncashmentWorkingDaysPerMonth] DEFAULT 22"));

            // The reminder cadence. Each default is exactly the private const it replaces, so an
            // existing tenant is chased today on the same schedule as yesterday.
            migrationBuilder.Sql(AddColumn(Settings, "LeaveStartingReminderDays",
                "int NOT NULL CONSTRAINT [DF_CompanyHrPolicySettings_LeaveStartingReminderDays] DEFAULT 7"));
            migrationBuilder.Sql(AddColumn(Settings, "LeaveClosureGraceDays",
                "int NOT NULL CONSTRAINT [DF_CompanyHrPolicySettings_LeaveClosureGraceDays] DEFAULT 2"));
            migrationBuilder.Sql(AddColumn(Settings, "LeaveUndecidedChaseDays",
                "int NOT NULL CONSTRAINT [DF_CompanyHrPolicySettings_LeaveUndecidedChaseDays] DEFAULT 5"));
            migrationBuilder.Sql(AddColumn(Settings, "MandatoryLeaveChaseFromMonth",
                "int NOT NULL CONSTRAINT [DF_CompanyHrPolicySettings_MandatoryLeaveChaseFromMonth] DEFAULT 9"));
            migrationBuilder.Sql(AddColumn(Settings, "LeaveCarryOverExpiryReminderDays",
                "int NOT NULL CONSTRAINT [DF_CompanyHrPolicySettings_LeaveCarryOverExpiryReminderDays] DEFAULT 30"));

            // ⚠ The ONE value that differs from its column default, and the only reason this block
            // exists. Everything else the scaffold wanted to write here is already the default above,
            // so writing it again would only be a second place to keep the numbers in step.
            //
            // Guarded on the current value so it is idempotent: on a rebuilt database the model seed
            // has already set this to 1 and there is nothing to do.
            //
            // ⚠ It is NOT additionally guarded on `UpdatedAt IS NULL`, and the first version of this
            // migration was — which silently did nothing on the very database it was written for.
            // The seeded row had been edited through the settings page on 2026-09-14, so the row
            // looked "customised" and the update declined to touch it, leaving the demo tenant with
            // in-service encashment OFF: exactly the outcome this statement exists to prevent.
            //
            // The reasoning behind that guard does not hold. It was meant to avoid overturning a
            // deliberate choice — but AllowInServiceEncashment did not EXIST until the statement
            // above created it moments ago, so no choice about it can predate this migration. A
            // timestamp that records an edit to some other field says nothing about this one.
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Settings}', 'AllowInServiceEncashment') IS NOT NULL
    UPDATE [dbo].[{Settings}]
       SET [AllowInServiceEncashment] = 1
     WHERE [Id] = '{SeededSettingsId}'
       AND [AllowInServiceEncashment] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumn(Encashments, "RateBasis"));

            migrationBuilder.Sql(DropColumn(Settings, "AllowInServiceEncashment"));
            migrationBuilder.Sql(DropColumn(Settings, "EncashmentWorkingDaysPerMonth"));
            migrationBuilder.Sql(DropColumn(Settings, "LeaveStartingReminderDays"));
            migrationBuilder.Sql(DropColumn(Settings, "LeaveClosureGraceDays"));
            migrationBuilder.Sql(DropColumn(Settings, "LeaveUndecidedChaseDays"));
            migrationBuilder.Sql(DropColumn(Settings, "MandatoryLeaveChaseFromMonth"));
            migrationBuilder.Sql(DropColumn(Settings, "LeaveCarryOverExpiryReminderDays"));
        }
    }
}
