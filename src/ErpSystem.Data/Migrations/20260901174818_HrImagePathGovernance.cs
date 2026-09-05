using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Lane 3a-ii — the last two caller-supplied image paths, replaced by gated uploads.
    /// </summary>
    /// <remarks>
    /// <para><b>Two unrelated tables, one migration, because they are one piece of work:</b> closing
    /// the remaining places where a file location arrived on a request body instead of a file
    /// arriving through the controlled upload gate.</para>
    ///
    /// <para><c>ExternalAssociates.PhotoFileUploadRecordId</c> is a single nullable column — the
    /// avatar precedent set by <c>JobCandidate.ProfilePhotoFileUploadRecordId</c>, deliberately NOT
    /// registered in the central DMS, because a contact's photograph carries no retention value and
    /// one document record per avatar is repository noise.</para>
    ///
    /// <para><c>CompanySealAssets</c> is the opposite call, and for the opposite reason. A seal or a
    /// signature is an instrument of authority: whoever holds it can make a document look authentic.
    /// It is versioned rather than overwritten, so that after a compromise the question <i>which
    /// documents carry the seal that leaked?</i> still has an answer. Currency is derived from
    /// <c>RetiredOn IS NULL</c>, following <c>EmployeeSalaryAssignment</c>'s close-then-insert idiom
    /// rather than storing a flag that drifts from the window it describes.</para>
    ///
    /// <para>⚠ <b>No unique index on "one open row per tenant per kind."</b> The delete here is soft,
    /// and a unique index that counts soft-deleted rows is the defect this module has met nine
    /// times. The service closes the open row before inserting, and the suite asserts
    /// retire-then-re-add.</para>
    ///
    /// <para>Written as guarded SQL rather than the scaffolded builder calls, per the house
    /// convention: this must be safe to apply to a database already rebuilt from the EF model.</para>
    /// </remarks>
    public partial class HrImagePathGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[dbo].[ExternalAssociates]', N'PhotoFileUploadRecordId') IS NULL
    ALTER TABLE [dbo].[ExternalAssociates] ADD [PhotoFileUploadRecordId] uniqueidentifier NULL;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[CompanySealAssets]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CompanySealAssets](
        [Id] uniqueidentifier NOT NULL,
        [Kind] int NOT NULL,
        [FileUploadRecordId] uniqueidentifier NOT NULL,
        [DocumentRecordId] uniqueidentifier NULL,
        [DocumentVersionId] uniqueidentifier NULL,
        [FileName] nvarchar(255) NULL,
        [MimeType] nvarchar(150) NULL,
        [FileSizeBytes] bigint NULL,
        [EffectiveFrom] datetime2 NOT NULL,
        [RetiredOn] datetime2 NULL,
        [RetiredReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_CompanySealAssets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CompanySealAssets_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants]([Id]) ON DELETE NO ACTION
    );
END;

-- Every generated letter resolves the current seal, and the lookup is always
-- (tenant, kind, still open). This is the index that keeps letter generation cheap.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CompanySealAsset_Tenant_Kind_RetiredOn' AND object_id = OBJECT_ID(N'[dbo].[CompanySealAssets]'))
    CREATE INDEX [IX_CompanySealAsset_Tenant_Kind_RetiredOn] ON [dbo].[CompanySealAssets]([TenantId], [Kind], [RetiredOn]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[CompanySealAssets]', N'U') IS NOT NULL DROP TABLE [dbo].[CompanySealAssets];

IF COL_LENGTH(N'[dbo].[ExternalAssociates]', N'PhotoFileUploadRecordId') IS NOT NULL
    ALTER TABLE [dbo].[ExternalAssociates] DROP COLUMN [PhotoFileUploadRecordId];
");
        }
    }
}
