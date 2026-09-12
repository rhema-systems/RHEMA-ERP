using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// The notice decision becomes its own act, with its own actor and date (area 9b slice 14).
    /// </summary>
    /// <remarks>
    /// <para><b>Why this exists.</b> The FR-HR-092 approval moved onto the generic workflow engine,
    /// whose approve action carries a comment and nothing else. This area's approval also settled
    /// the <i>notice</i> — waive the balance, or pay it instead of working it — and that is money.
    /// It does not belong in a comment, so it moved out into its own endpoint.</para>
    ///
    /// <para><b>Why a DATE and not just the two bools that were already there.</b>
    /// <c>IsNoticeWaived == false AND IsNoticePaidInLieu == false</c> is two different facts wearing
    /// the same clothes: "we looked, and neither applies" and "nobody has looked". FR-HR-184 turns
    /// on the difference. A settlement prepared against an unserved notice with no decision recorded
    /// does not produce a wrong figure — it produces <b>no notice-pay line at all</b>, and a missing
    /// line on a final statement is invisible in a way a wrong number is not.</para>
    ///
    /// <para>So <c>NoticeDecisionOn</c> is the stamp that makes "neither" sayable, and
    /// <c>PrepareSettlementAsync</c> refuses while it is null and notice was left unserved.
    /// <c>NoticeDecidedById</c> names whoever took it, under the same authority that may sign the
    /// separation — the MD for most exits, HR only for a procedural one.</para>
    ///
    /// <para>Both columns are nullable, which is also the back-fill answer: every separation that
    /// already exists is correctly described as "no decision recorded". Where such a record has no
    /// notice shortfall the gate never fires; where it has one, the gate fires and somebody says
    /// what happens — which is the right outcome for a record nobody ever decided.</para>
    ///
    /// <para>Scaffolded by the user, rewritten here into guarded SQL, listed in
    /// <c>FastBuildMigrationMetadata.cs</c>.</para>
    /// </remarks>
    public partial class AddSeparationNoticeDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'NoticeDecisionOn' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] ADD [NoticeDecisionOn] datetime2 NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'NoticeDecidedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] ADD [NoticeDecidedById] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparations_NoticeDecidedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    CREATE INDEX [IX_EmployeeSeparations_NoticeDecidedById] ON [dbo].[EmployeeSeparations] ([NoticeDecidedById]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeSeparations_Employees_NoticeDecidedById')
    ALTER TABLE [dbo].[EmployeeSeparations] ADD CONSTRAINT [FK_EmployeeSeparations_Employees_NoticeDecidedById]
        FOREIGN KEY ([NoticeDecidedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeSeparations_Employees_NoticeDecidedById')
    ALTER TABLE [dbo].[EmployeeSeparations] DROP CONSTRAINT [FK_EmployeeSeparations_Employees_NoticeDecidedById];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparations_NoticeDecidedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    DROP INDEX [IX_EmployeeSeparations_NoticeDecidedById] ON [dbo].[EmployeeSeparations];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'NoticeDecidedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] DROP COLUMN [NoticeDecidedById];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'NoticeDecisionOn' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] DROP COLUMN [NoticeDecisionOn];");
        }
    }
}
