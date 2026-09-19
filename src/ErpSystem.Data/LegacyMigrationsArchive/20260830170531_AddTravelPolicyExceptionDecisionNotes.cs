using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// <c>StaffTravelPolicyExceptions</c> gains <c>DecisionNotes</c> — why an exception to a travel
    /// policy rule was granted or refused.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> The requester's side was always recorded (<c>ExceptionReason</c>); the decider's
    /// had nowhere to go. Granting an exception authorises spend above a cap — an authority HR
    /// deliberately does not hold — so "why" is precisely what an auditor asks and the row could
    /// not answer it.
    /// </para>
    /// <para>
    /// <b>⚠ The client had been sending it all along.</b> <c>decidePolicyException</c> posted a
    /// <c>notes</c> field for as long as it has existed and
    /// <c>DecideStaffTravelPolicyExceptionDto</c> had no such property, so the model binder
    /// discarded every explanation while the screen reported success. A matched route says nothing
    /// about the body — the path resolves either way, and only reading the DTO or running the call
    /// finds it. The field is <c>decisionNotes</c> on both sides now.
    /// </para>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced because it was not idempotent.</b> <c>rebuild-db</c>
    /// builds the schema from the EF model rather than from the migration chain, so a database
    /// rebuilt after the entity change already has this column and a bare <c>AddColumn</c> fails
    /// with "Column names in each table must be unique in each table". Guarded on
    /// <c>COL_LENGTH</c>, the migration is a no-op against a database already in the target shape
    /// and still does the work on one that is not. Same rewrite, same reason, as
    /// <c>20260829215518_AddSuccessionAndNhisDocumentDmsColumns</c>. The generated
    /// <c>.Designer.cs</c> target model is retained unchanged.
    /// </para>
    /// </remarks>
    public partial class AddTravelPolicyExceptionDecisionNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.StaffTravelPolicyExceptions', 'DecisionNotes') IS NULL
BEGIN
    ALTER TABLE [dbo].[StaffTravelPolicyExceptions] ADD [DecisionNotes] nvarchar(2000) NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.StaffTravelPolicyExceptions', 'DecisionNotes') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[StaffTravelPolicyExceptions] DROP COLUMN [DecisionNotes];
END
");
        }
    }
}
