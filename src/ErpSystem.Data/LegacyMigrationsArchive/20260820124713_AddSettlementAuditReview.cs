using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Internal Audit's review of a final settlement, before payment is released
    /// (area 9b slice 6, FR-HR-185).
    /// </summary>
    /// <remarks>
    /// <para>FR-HR-185 and Chapter 12 both put Internal Audit between a prepared settlement and a
    /// paid one. A review either passes the statement or returns it with findings; a return is not
    /// a refusal of the separation, only of the figures, so the statement becomes editable again
    /// and must be corrected and re-finalised.</para>
    ///
    /// <para><b><c>ReturnCount</c> exists because <c>FinalisedOn</c> is overwritten</b> on each
    /// re-finalisation. "This was queried three times before it was paid" is exactly what an auditor
    /// asks afterwards, and without the counter that history disappears.</para>
    ///
    /// <para>⚠ <b><c>ReviewOutcome</c> defaults to 1, not the scaffold's 0.</b> EF writes
    /// <c>defaultValue: 0</c> for any non-nullable int, but <c>SettlementReviewOutcome</c> starts at
    /// <c>NotReviewed = 1</c> — so 0 is not a member of the enum at all, and an existing row would
    /// carry a value that maps to nothing. The same correction <c>ProceduralAbsenceDays</c> needed:
    /// <b>read what a scaffold chose for a non-nullable column, and check enums that do not start at
    /// zero.</b></para>
    ///
    /// <para>⚠ Nobody holds <c>TDC_INTERNAL_AUDIT</c> on the live tenant (measured 2026-08-20). The
    /// control is built as specified and will hold every settlement unreviewed until the role is
    /// granted — correct, and raised with TDC as an operational prerequisite.</para>
    ///
    /// <para>Scaffolded by the user, rewritten here into guarded SQL, listed in
    /// <c>FastBuildMigrationMetadata.cs</c>.</para>
    /// </remarks>
    public partial class AddSettlementAuditReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default 1 = NotReviewed. See the remarks: 0 is not a member of this enum.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ReviewOutcome' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    ALTER TABLE [dbo].[SeparationSettlements] ADD [ReviewOutcome] int NOT NULL CONSTRAINT [DF_SeparationSettlements_ReviewOutcome] DEFAULT (1);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ReviewedById' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    ALTER TABLE [dbo].[SeparationSettlements] ADD [ReviewedById] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ReviewedOn' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    ALTER TABLE [dbo].[SeparationSettlements] ADD [ReviewedOn] datetime2 NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ReviewNotes' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    ALTER TABLE [dbo].[SeparationSettlements] ADD [ReviewNotes] nvarchar(2000) NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ReturnCount' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    ALTER TABLE [dbo].[SeparationSettlements] ADD [ReturnCount] int NOT NULL CONSTRAINT [DF_SeparationSettlements_ReturnCount] DEFAULT (0);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationSettlements_ReviewedById' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    CREATE INDEX [IX_SeparationSettlements_ReviewedById] ON [dbo].[SeparationSettlements] ([ReviewedById]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationSettlements_Employees_ReviewedById')
    ALTER TABLE [dbo].[SeparationSettlements] ADD CONSTRAINT [FK_SeparationSettlements_Employees_ReviewedById]
        FOREIGN KEY ([ReviewedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationSettlements_Employees_ReviewedById')
    ALTER TABLE [dbo].[SeparationSettlements] DROP CONSTRAINT [FK_SeparationSettlements_Employees_ReviewedById];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationSettlements_ReviewedById' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    DROP INDEX [IX_SeparationSettlements_ReviewedById] ON [dbo].[SeparationSettlements];");

            // Named default constraints must go before their columns.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_SeparationSettlements_ReviewOutcome')
    ALTER TABLE [dbo].[SeparationSettlements] DROP CONSTRAINT [DF_SeparationSettlements_ReviewOutcome];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_SeparationSettlements_ReturnCount')
    ALTER TABLE [dbo].[SeparationSettlements] DROP CONSTRAINT [DF_SeparationSettlements_ReturnCount];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ReturnCount' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    ALTER TABLE [dbo].[SeparationSettlements] DROP COLUMN [ReturnCount];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ReviewNotes' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    ALTER TABLE [dbo].[SeparationSettlements] DROP COLUMN [ReviewNotes];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ReviewedOn' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    ALTER TABLE [dbo].[SeparationSettlements] DROP COLUMN [ReviewedOn];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ReviewedById' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    ALTER TABLE [dbo].[SeparationSettlements] DROP COLUMN [ReviewedById];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ReviewOutcome' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    ALTER TABLE [dbo].[SeparationSettlements] DROP COLUMN [ReviewOutcome];");
        }
    }
}
