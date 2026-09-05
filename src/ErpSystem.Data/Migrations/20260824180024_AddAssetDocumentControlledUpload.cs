using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 16 slice 12b. Puts asset documents and photographs behind the <b>controlled upload
    /// gate</b> — five columns on <c>AssetAttachment</c> and six on <c>AssetImages</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this is really fixing.</b> Both create endpoints used to take <c>FileName</c> and
    /// <c>FilePath</c> as JSON: the caller named a path, the server wrote the string down, and no
    /// file was ever stored or scanned anywhere. An "attachment" was a string somebody typed, and
    /// the list rendered it beautifully. These columns are what an actual file needs — its size,
    /// who filed it, the scanned upload record, and the central-DMS record and version — and they
    /// are the same five every other HR attachment in this codebase already carries.
    /// </para>
    /// <para>
    /// ⚠ <b>The two table names disagree about pluralisation</b>: <c>AssetImages</c> is plural and
    /// <c>AssetAttachment</c> is singular. That is pre-existing and is exactly the shape §3.3 of the
    /// build plan warns about, so every statement below names its table explicitly rather than
    /// deriving one from the other.
    /// </para>
    /// <para>
    /// <b><c>UploadedById</c> is nullable, deliberately.</b> It is an Employee id taken from the
    /// token, and a row written before this gate existed genuinely does not know who filed it.
    /// Making it required would have meant inventing <c>Guid.Empty</c> for those rows — which is
    /// precisely the mistake four performance call sites made before <c>HrAttachmentUpload</c>
    /// existed, and every one of them died on a foreign-key violation the first time it ran.
    /// </para>
    /// <para>
    /// <b>Existing rows keep <c>FilePath</c> and gain nothing else</b>, which is the honest outcome:
    /// there is no file behind them to describe. <c>AssetAttachmentDto.IsStored</c> is computed from
    /// these columns, so the screens offer a download only where one can actually be served and say
    /// "No file stored" otherwise, rather than handing the user a button that answers 404.
    /// </para>
    /// <para>
    /// ⚠ <b>Every add is guarded and every drop is conditional</b>, so this is safe to re-run
    /// against a database at either state — including one built from the EF model rather than from
    /// the chain, which is how this repo's <c>rebuild-db</c> works. The scaffold was correct as
    /// generated: eleven column adds, nothing inferred and nothing renamed. (Worth confirming rather
    /// than assuming — EF inferred a wrong <c>RENAME</c> in this area at slice 3.)
    /// </para>
    /// </remarks>
    public partial class AddAssetDocumentControlledUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── AssetImages ───────────────────────────────────────────────────────────────
            //
            // `Caption` is not part of the gate; it goes in with it because a wall of thumbnails
            // nobody captioned is not a record of anything, and a photograph of damage taken at a
            // return is evidence in a money claim against a named employee.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'Caption' AND object_id = OBJECT_ID('dbo.AssetImages'))
    ALTER TABLE [dbo].[AssetImages] ADD [Caption] nvarchar(1000) NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'FileSizeBytes' AND object_id = OBJECT_ID('dbo.AssetImages'))
    ALTER TABLE [dbo].[AssetImages] ADD [FileSizeBytes] bigint NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'UploadedById' AND object_id = OBJECT_ID('dbo.AssetImages'))
    ALTER TABLE [dbo].[AssetImages] ADD [UploadedById] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'FileUploadRecordId' AND object_id = OBJECT_ID('dbo.AssetImages'))
    ALTER TABLE [dbo].[AssetImages] ADD [FileUploadRecordId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'DocumentRecordId' AND object_id = OBJECT_ID('dbo.AssetImages'))
    ALTER TABLE [dbo].[AssetImages] ADD [DocumentRecordId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'DocumentVersionId' AND object_id = OBJECT_ID('dbo.AssetImages'))
    ALTER TABLE [dbo].[AssetImages] ADD [DocumentVersionId] uniqueidentifier NULL;");

            // ── AssetAttachment (singular — see the remark above) ──────────────────────────
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'FileSizeBytes' AND object_id = OBJECT_ID('dbo.AssetAttachment'))
    ALTER TABLE [dbo].[AssetAttachment] ADD [FileSizeBytes] bigint NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'UploadedById' AND object_id = OBJECT_ID('dbo.AssetAttachment'))
    ALTER TABLE [dbo].[AssetAttachment] ADD [UploadedById] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'FileUploadRecordId' AND object_id = OBJECT_ID('dbo.AssetAttachment'))
    ALTER TABLE [dbo].[AssetAttachment] ADD [FileUploadRecordId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'DocumentRecordId' AND object_id = OBJECT_ID('dbo.AssetAttachment'))
    ALTER TABLE [dbo].[AssetAttachment] ADD [DocumentRecordId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'DocumentVersionId' AND object_id = OBJECT_ID('dbo.AssetAttachment'))
    ALTER TABLE [dbo].[AssetAttachment] ADD [DocumentVersionId] uniqueidentifier NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ This severs every asset document and photograph from the file behind it. The bytes
            // survive — they are in the controlled store and registered in the central DMS — but
            // nothing in HR will know which asset they belong to, and the rows left behind become
            // exactly what this slice replaced: a file name and a path that lead nowhere.
            foreach (var column in new[]
                     {
                         "DocumentVersionId", "DocumentRecordId", "FileUploadRecordId",
                         "UploadedById", "FileSizeBytes", "Caption",
                     })
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = '{column}' AND object_id = OBJECT_ID('dbo.AssetImages'))
    ALTER TABLE [dbo].[AssetImages] DROP COLUMN [{column}];");
            }

            foreach (var column in new[]
                     {
                         "DocumentVersionId", "DocumentRecordId", "FileUploadRecordId",
                         "UploadedById", "FileSizeBytes",
                     })
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = '{column}' AND object_id = OBJECT_ID('dbo.AssetAttachment'))
    ALTER TABLE [dbo].[AssetAttachment] DROP COLUMN [{column}];");
            }
        }
    }
}
