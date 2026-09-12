using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 16 slice 5. Records the serving of the AST-5 responsibility-and-terms document:
    /// <c>TermsDocumentSentAt</c>, <c>TermsDocumentSentTo</c> and <c>TermsDocumentSentById</c> on
    /// <c>AssetAssignments</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three columns rather than a flag</b>, because "was it sent?" is really three questions and
    /// a boolean answers none of them well: <i>when</i>, <i>to which address</i> — an employee's
    /// recorded email changes, and the copy went to whatever it was that day — and <i>by whom</i>.
    /// Only the email route writes them. Downloading the document for print deliberately records
    /// nothing: printing a copy is not serving it on somebody, and stamping "sent" there would let an
    /// unsent form look served.
    /// </para>
    /// <para>
    /// The scaffold was correct as generated — three column adds, an index and one foreign key, with
    /// nothing inferred. Rewritten as guarded SQL only to match the surrounding HR migrations, so it
    /// is safe to re-run against a database at either state, including one built from the EF model.
    /// </para>
    /// </remarks>
    public partial class AddAssetAssignmentTermsDocumentSend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'TermsDocumentSentAt' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD [TermsDocumentSentAt] datetime2 NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'TermsDocumentSentTo' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD [TermsDocumentSentTo] nvarchar(256) NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'TermsDocumentSentById' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD [TermsDocumentSentById] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetAssignments_TermsDocumentSentById' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    CREATE INDEX [IX_AssetAssignments_TermsDocumentSentById] ON [dbo].[AssetAssignments] ([TermsDocumentSentById]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AssetAssignments_Employees_TermsDocumentSentById' AND parent_object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD CONSTRAINT [FK_AssetAssignments_Employees_TermsDocumentSentById]
        FOREIGN KEY ([TermsDocumentSentById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AssetAssignments_Employees_TermsDocumentSentById' AND parent_object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] DROP CONSTRAINT [FK_AssetAssignments_Employees_TermsDocumentSentById];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetAssignments_TermsDocumentSentById' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    DROP INDEX [IX_AssetAssignments_TermsDocumentSentById] ON [dbo].[AssetAssignments];");

            // The record of who was served which document, and when, is lost — nothing else carries
            // it. The documents themselves are unaffected: they are rendered on demand from the
            // assignment and never stored.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'TermsDocumentSentById' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] DROP COLUMN [TermsDocumentSentById];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'TermsDocumentSentTo' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] DROP COLUMN [TermsDocumentSentTo];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'TermsDocumentSentAt' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] DROP COLUMN [TermsDocumentSentAt];");
        }
    }
}
