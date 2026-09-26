using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Lane E1b: withdrawal of a grade placement becomes a fact of its own, instead of being
    /// expressed by shortening the placement's date window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as for every migration in this
    /// round — <c>rebuild-db</c> builds from the EF model, so a rebuilt database already has these
    /// columns and a bare <c>AddColumn</c> stops the chain at its first statement.
    /// </para>
    ///
    /// <para><b>Why the columns exist.</b> One date interval was carrying two different questions:
    /// <i>when were these terms in force</i>, and <i>was this row withdrawn</i>. Withdrawal was
    /// expressed by pulling <c>EffectiveTo</c> back to yesterday — which cannot be done to a
    /// placement that has not started yet without ending it before it begins. So the old code
    /// clamped the end to the row's own start date, and a one-day window in the FUTURE is not a
    /// closed placement: it is a scheduled one. A promotion booked for 1 October and withdrawn on
    /// 15 September matched the as-of predicate again ON 1 October, and
    /// <c>EmolumentService</c> — and through it benefit enrolment — read that notch as the person's
    /// basic pay for the day.</para>
    ///
    /// <para><b>The repair, and its limit.</b> Measured on ErpSystemDB 2026-09-09: 10 live
    /// placements, 6 carrying <c>EffectiveTo = EffectiveDate</c> (the clamp's fingerprint), 4 of
    /// them dated today or later and therefore still able to fire, 0 negative windows. Statement 1
    /// withdraws exactly those that can still fire. Statement 2 withdraws any negative window —
    /// none here, but <c>AssignSalaryAsync</c> could produce one when two placements shared an
    /// effective date, so another environment may hold some.</para>
    ///
    /// <para>⚠ <b>The repair infers intent from shape, so it is written to be reversible.</b>
    /// <c>EffectiveTo = EffectiveDate</c> is the clamp's fingerprint, but a placement somebody
    /// created deliberately to run for a single day is indistinguishable from one after the fact.
    /// Scoping to <c>EffectiveDate &gt;= today</c> makes a false positive close to inconceivable —
    /// nobody places a person on a notch for exactly one future day — but "close to inconceivable"
    /// is an inference, not a measurement, on a database nobody has looked at. So each row records
    /// <b>its original end date inside the withdrawal reason</b>: if this ever does catch a genuine
    /// one-day placement, the value is not lost, and clearing <c>WithdrawnAt</c> and restoring the
    /// date named in the reason puts it back exactly.</para>
    ///
    /// <para>⚠ <b>Clamped rows dated in the PAST are deliberately left alone.</b> They have already
    /// had their one day and it is over, so nothing further can fire. Marking them withdrawn now
    /// would change what a historical as-of read says about that day, for no gain.</para>
    /// </remarks>
    public partial class AddSalaryAssignmentWithdrawal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── schema ────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.EmployeeSalaryAssignments', 'WithdrawnAt') IS NULL
    ALTER TABLE [dbo].[EmployeeSalaryAssignments] ADD [WithdrawnAt] datetime2 NULL;");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.EmployeeSalaryAssignments', 'WithdrawnReason') IS NULL
    ALTER TABLE [dbo].[EmployeeSalaryAssignments] ADD [WithdrawnReason] nvarchar(500) NULL;");

            // ── the repair ────────────────────────────────────────────────────
            //
            // ⚠ Both statements clear EffectiveTo AND quote its old value in the reason. SQL Server
            // evaluates every source expression against the pre-update row before applying any
            // assignment, so the CONVERT below reads the original date, not the NULL being written
            // beside it. That is what makes the inference reversible by hand.

            // 1 · a clamped placement that has not fired yet. EffectiveTo is cleared as well as
            //     marked: a placement withdrawn before it took effect was never in force, so under
            //     the new rule it carries no window at all.
            migrationBuilder.Sql(@"
UPDATE [dbo].[EmployeeSalaryAssignments]
   SET [WithdrawnAt] = SYSUTCDATETIME(),
       [WithdrawnReason] =
           'Withdrawn before it took effect. Repaired by migration AddSalaryAssignmentWithdrawal: '
         + 'it had been ended by a rule that could not express withdrawal, which left it able to '
         + 'take effect on its own start date. Its end date was '
         + CONVERT(varchar(10), [EffectiveTo], 23)
         + ' — if this placement was genuinely meant to run for that single day, clear WithdrawnAt '
         + 'and restore that end date.',
       [EffectiveTo] = NULL
 WHERE [IsDeleted] = 0
   AND [WithdrawnAt] IS NULL
   AND [EffectiveTo] IS NOT NULL
   AND CAST([EffectiveTo] AS date) = CAST([EffectiveDate] AS date)
   AND CAST([EffectiveDate] AS date) >= CAST(SYSUTCDATETIME() AS date);");

            // 2 · a window that ends before it begins. Only one path could produce these — two
            //     placements sharing an effective date, where the older was ended "the day before"
            //     the newer. None on this database; other environments may differ.
            migrationBuilder.Sql(@"
UPDATE [dbo].[EmployeeSalaryAssignments]
   SET [WithdrawnAt] = SYSUTCDATETIME(),
       [WithdrawnReason] =
           'Withdrawn before it took effect. Repaired by migration AddSalaryAssignmentWithdrawal: '
         + 'it had been superseded by a placement sharing its effective date, which ended it before '
         + 'it began. Its end date was '
         + CONVERT(varchar(10), [EffectiveTo], 23)
         + ' — if that was deliberate, clear WithdrawnAt and restore that end date.',
       [EffectiveTo] = NULL
 WHERE [IsDeleted] = 0
   AND [WithdrawnAt] IS NULL
   AND [EffectiveTo] IS NOT NULL
   AND CAST([EffectiveTo] AS date) < CAST([EffectiveDate] AS date);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ The repair is not undone. Dropping the columns discards the withdrawal marks, and
            // the end dates they replaced were wrong — restoring them would put back the very rows
            // that could fire. Down restores the schema, not the defect. The original dates are in
            // the reason text, which goes with the column; read them before running this if any of
            // the repaired rows are in question.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.EmployeeSalaryAssignments', 'WithdrawnReason') IS NOT NULL
    ALTER TABLE [dbo].[EmployeeSalaryAssignments] DROP COLUMN [WithdrawnReason];");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.EmployeeSalaryAssignments', 'WithdrawnAt') IS NOT NULL
    ALTER TABLE [dbo].[EmployeeSalaryAssignments] DROP COLUMN [WithdrawnAt];");
        }
    }
}
