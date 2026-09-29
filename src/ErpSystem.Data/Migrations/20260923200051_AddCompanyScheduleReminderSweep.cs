using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane N-b2 — company-schedule reminders that send themselves: the hourly sweep's marker
    /// for the RSVP chase, and the tenant setting for when it goes.
    /// </summary>
    /// <remarks>
    /// <para><b><c>CompanyEvents.RsvpReminderSentDate</c></b> — when an event's unanswered invitations
    /// were chased, by the sweep or HR's button; null until then. The event reminder already had its
    /// marker (<c>ReminderSentDate</c>) — saved, and read by nothing until this lane.</para>
    ///
    /// <para><b><c>CompanyHrPolicySettings.CompanyEventRsvpChaseLeadDays</c></b> — NOT NULL with a real
    /// <c>DEFAULT (2)</c>, which fills EVERY tenant's row, not only the seeded one. ⚠ The scaffold had
    /// <c>defaultValue: 0</c> and then an <c>UpdateData</c> against one hard-coded seed-row id: right on a
    /// database holding exactly that row, and a chase ON the deadline day everywhere else — the pattern
    /// round 4 has now rewritten three times.</para>
    ///
    /// <para>⚠ Guarded SQL throughout, as on every HR migration: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has both columns.</para>
    /// </remarks>
    public partial class AddCompanyScheduleReminderSweep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CompanyEvents', 'RsvpReminderSentDate') IS NULL
    ALTER TABLE [dbo].[CompanyEvents] ADD [RsvpReminderSentDate] datetime2 NULL;
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'CompanyEventRsvpChaseLeadDays') IS NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] ADD [CompanyEventRsvpChaseLeadDays] int NOT NULL
        CONSTRAINT [DF_CompanyHrPolicySettings_CompanyEventRsvpChaseLeadDays] DEFAULT (2);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The default constraint first, found by column rather than trusted by name: a database built
            // from the model has no constraint at all, and one built by this migration has the named one.
            migrationBuilder.Sql(@"
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
FROM sys.default_constraints dc
WHERE dc.parent_object_id = OBJECT_ID('dbo.CompanyHrPolicySettings')
  AND COL_NAME(dc.parent_object_id, dc.parent_column_id) = 'CompanyEventRsvpChaseLeadDays';
IF LEN(@sql) > 0 EXEC sp_executesql @sql;");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'CompanyEventRsvpChaseLeadDays') IS NOT NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP COLUMN [CompanyEventRsvpChaseLeadDays];
IF COL_LENGTH('dbo.CompanyEvents', 'RsvpReminderSentDate') IS NOT NULL
    ALTER TABLE [dbo].[CompanyEvents] DROP COLUMN [RsvpReminderSentDate];");
        }
    }
}
