using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Links HR document rows to the shared controlled-upload boundary and the central DMS,
    /// and adds the two intake tables supporting public CV uploads and the legacy-file migration.
    /// </summary>
    /// <remarks>
    /// <para>HR uploads previously wrote straight to the public web root with no malware scan and
    /// no upload record. Routing them through <c>IControlledFileUploadService</c> and
    /// <c>ICentralDocumentRepositoryFileService</c> requires each owning row to remember which
    /// controlled upload and which DMS record/version backs it — that is all these columns are.</para>
    ///
    /// <para>Every added column is nullable and every existing path column is left untouched, so
    /// pre-migration rows keep resolving: the download helper prefers the DMS ids, falls back to a
    /// bare upload record, and only then reads the legacy string. Backfill is deliberately NOT done
    /// here — it has to stream each file through the virus scanner, which is the job of the separate
    /// HR legacy-file migration utility, not of a schema migration.</para>
    ///
    /// <para>No foreign keys to <c>FileUploadRecords</c> or the DMS tables. Controlled uploads are
    /// removed by soft-delete, <c>FileUploadRecords</c> carries a delete-guard trigger, and this
    /// database is rebuilt from the EF model rather than the migration chain — a dozen extra
    /// Restrict relationships would be a dozen new ways for that rebuild to fail, buying integrity
    /// the soft-delete contract already provides. This mirrors how
    /// <c>FileUploadRecord.UploadedByUserId</c> is treated.</para>
    ///
    /// <para><b>On the six dropped <c>IX_&lt;table&gt;_TenantId</c> indexes:</b> these are scaffolded,
    /// not hand-chosen. Each new composite index leads with <c>TenantId</c>, so EF's foreign-key
    /// index convention stops emitting the redundant single-column index and the model no longer
    /// contains it. SQL Server still seeks on the leading column, so tenant-scoped query plans are
    /// unaffected. Keeping them would leave the model and the database permanently out of step and
    /// make every future <c>migrations add</c> re-propose the drops — the same reasoning recorded on
    /// <c>20260731143111_MergeProcurementPhase4AndEstateReadiness</c>.</para>
    ///
    /// <para>The scaffolded body has been rewritten as guarded SQL, matching the defensive style of
    /// the merge migrations, so this is safe on databases built by the migration chain, on databases
    /// built by <c>rebuild-db</c>/EnsureCreated where the model already contains all of this, and on
    /// a re-run. The generated <c>.Designer.cs</c> target model is retained unchanged.</para>
    /// </remarks>
    public partial class HrControlledDocumentLinks : Migration
    {
        /// <summary>Tables gaining the standard upload + DMS link triple.</summary>
        private static readonly string[] DocumentLinkTables =
        [
            "JobCandidateDocuments",
            "LeaveRequestAttachments",
            "AppraisalAttachments",
            "StaffDisciplineDocuments",
            "StaffMovementAttachments",
            "EmployeeMedicalExamDocuments"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in DocumentLinkTables)
            {
                AddGuidColumnIfMissing(migrationBuilder, table, "FileUploadRecordId");
                AddGuidColumnIfMissing(migrationBuilder, table, "DocumentRecordId");
                AddGuidColumnIfMissing(migrationBuilder, table, "DocumentVersionId");

                CreateIndexIfMissing(
                    migrationBuilder, table,
                    $"IX_{table}_TenantId_FileUploadRecordId",
                    "[TenantId], [FileUploadRecordId]");

                // Superseded by the composite above, whose leading column is TenantId.
                DropIndexIfExists(migrationBuilder, table, $"IX_{table}_TenantId");
            }

            AddGuidColumnIfMissing(migrationBuilder, "JobCandidates", "CvFileUploadRecordId");
            AddGuidColumnIfMissing(migrationBuilder, "JobCandidates", "CvDocumentRecordId");
            AddGuidColumnIfMissing(migrationBuilder, "JobCandidates", "CvDocumentVersionId");
            AddGuidColumnIfMissing(migrationBuilder, "JobCandidates", "ProfilePhotoFileUploadRecordId");
            CreateIndexIfMissing(
                migrationBuilder, "JobCandidates",
                "IX_JobCandidates_TenantId_CvFileUploadRecordId",
                "[TenantId], [CvFileUploadRecordId]");
            CreateIndexIfMissing(
                migrationBuilder, "JobCandidates",
                "IX_JobCandidates_TenantId_ProfilePhotoFileUploadRecordId",
                "[TenantId], [ProfilePhotoFileUploadRecordId]");

            AddGuidColumnIfMissing(migrationBuilder, "JobOffers", "OfferLetterFileUploadRecordId");
            AddGuidColumnIfMissing(migrationBuilder, "JobOffers", "OfferLetterDocumentRecordId");
            AddGuidColumnIfMissing(migrationBuilder, "JobOffers", "OfferLetterDocumentVersionId");
            AddGuidColumnIfMissing(migrationBuilder, "JobOffers", "SignedOfferLetterFileUploadRecordId");
            AddGuidColumnIfMissing(migrationBuilder, "JobOffers", "SignedOfferLetterDocumentRecordId");
            AddGuidColumnIfMissing(migrationBuilder, "JobOffers", "SignedOfferLetterDocumentVersionId");
            CreateIndexIfMissing(
                migrationBuilder, "JobOffers",
                "IX_JobOffers_TenantId_OfferLetterFileUploadRecordId",
                "[TenantId], [OfferLetterFileUploadRecordId]");
            CreateIndexIfMissing(
                migrationBuilder, "JobOffers",
                "IX_JobOffers_TenantId_SignedOfferLetterFileUploadRecordId",
                "[TenantId], [SignedOfferLetterFileUploadRecordId]");

            // Backs the resend-verification cooldown on both portals.
            AddDateTimeColumnIfMissing(
                migrationBuilder, "CandidatePortalAccounts", "LastVerificationEmailSentAtUtc");
            AddDateTimeColumnIfMissing(
                migrationBuilder, "ConsultantClientPortalAccounts", "LastVerificationEmailSentAtUtc");

            CreatePublicCvUploadTickets(migrationBuilder);
            CreateHrLegacyFileMigrationEntries(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropTableIfExists(migrationBuilder, "HrLegacyFileMigrationEntries");
            DropTableIfExists(migrationBuilder, "PublicCvUploadTickets");

            DropColumnIfExists(
                migrationBuilder, "ConsultantClientPortalAccounts", "LastVerificationEmailSentAtUtc");
            DropColumnIfExists(
                migrationBuilder, "CandidatePortalAccounts", "LastVerificationEmailSentAtUtc");

            DropIndexIfExists(
                migrationBuilder, "JobOffers",
                "IX_JobOffers_TenantId_SignedOfferLetterFileUploadRecordId");
            DropIndexIfExists(
                migrationBuilder, "JobOffers",
                "IX_JobOffers_TenantId_OfferLetterFileUploadRecordId");
            DropColumnIfExists(migrationBuilder, "JobOffers", "SignedOfferLetterDocumentVersionId");
            DropColumnIfExists(migrationBuilder, "JobOffers", "SignedOfferLetterDocumentRecordId");
            DropColumnIfExists(migrationBuilder, "JobOffers", "SignedOfferLetterFileUploadRecordId");
            DropColumnIfExists(migrationBuilder, "JobOffers", "OfferLetterDocumentVersionId");
            DropColumnIfExists(migrationBuilder, "JobOffers", "OfferLetterDocumentRecordId");
            DropColumnIfExists(migrationBuilder, "JobOffers", "OfferLetterFileUploadRecordId");

            DropIndexIfExists(
                migrationBuilder, "JobCandidates",
                "IX_JobCandidates_TenantId_ProfilePhotoFileUploadRecordId");
            DropIndexIfExists(
                migrationBuilder, "JobCandidates",
                "IX_JobCandidates_TenantId_CvFileUploadRecordId");
            DropColumnIfExists(migrationBuilder, "JobCandidates", "ProfilePhotoFileUploadRecordId");
            DropColumnIfExists(migrationBuilder, "JobCandidates", "CvDocumentVersionId");
            DropColumnIfExists(migrationBuilder, "JobCandidates", "CvDocumentRecordId");
            DropColumnIfExists(migrationBuilder, "JobCandidates", "CvFileUploadRecordId");

            foreach (var table in DocumentLinkTables)
            {
                // Restore the single-column index before removing the composite that replaced it,
                // so the table is never left without a TenantId-leading index.
                CreateIndexIfMissing(
                    migrationBuilder, table, $"IX_{table}_TenantId", "[TenantId]");

                DropIndexIfExists(
                    migrationBuilder, table, $"IX_{table}_TenantId_FileUploadRecordId");
                DropColumnIfExists(migrationBuilder, table, "DocumentVersionId");
                DropColumnIfExists(migrationBuilder, table, "DocumentRecordId");
                DropColumnIfExists(migrationBuilder, table, "FileUploadRecordId");
            }
        }

        private static void CreatePublicCvUploadTickets(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[PublicCvUploadTickets]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PublicCvUploadTickets] (
        [Id] uniqueidentifier NOT NULL,
        [FileUploadRecordId] uniqueidentifier NOT NULL,
        [JobVacancyId] uniqueidentifier NOT NULL,
        [TokenHash] char(64) NOT NULL,
        [OriginalFileName] nvarchar(255) NOT NULL,
        [ContentType] nvarchar(200) NULL,
        [FileSize] bigint NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [ClaimedAtUtc] datetime2 NULL,
        [ClaimedByCandidateId] uniqueidentifier NULL,
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
        CONSTRAINT [PK_PublicCvUploadTickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PublicCvUploadTickets_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );

    -- Claiming looks a ticket up by tenant + hash; uniqueness makes a replayed token a
    -- lookup miss rather than an ambiguous match.
    CREATE UNIQUE INDEX [IX_PublicCvUploadTickets_TenantId_TokenHash]
        ON [dbo].[PublicCvUploadTickets] ([TenantId], [TokenHash]);
    CREATE INDEX [IX_PublicCvUploadTickets_TenantId_FileUploadRecordId]
        ON [dbo].[PublicCvUploadTickets] ([TenantId], [FileUploadRecordId]);
    -- Drives the sweeper's unclaimed-and-expired scan.
    CREATE INDEX [IX_PublicCvUploadTickets_ClaimedAtUtc_ExpiresAtUtc]
        ON [dbo].[PublicCvUploadTickets] ([ClaimedAtUtc], [ExpiresAtUtc]);
END");
        }

        private static void CreateHrLegacyFileMigrationEntries(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[HrLegacyFileMigrationEntries]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[HrLegacyFileMigrationEntries] (
        [Id] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(150) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [LegacyPath] nvarchar(1000) NOT NULL,
        [Status] int NOT NULL,
        [ErrorCode] nvarchar(100) NULL,
        [ErrorMessage] nvarchar(2000) NULL,
        [FileUploadRecordId] uniqueidentifier NULL,
        [ProcessedAtUtc] datetime2 NULL,
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
        CONSTRAINT [PK_HrLegacyFileMigrationEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HrLegacyFileMigrationEntries_Tenants_TenantId]
            FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );

    -- One ledger row per owning record keeps re-runs of the utility idempotent.
    CREATE UNIQUE INDEX [IX_HrLegacyFileMigrationEntries_TenantId_EntityType_EntityId]
        ON [dbo].[HrLegacyFileMigrationEntries] ([TenantId], [EntityType], [EntityId]);
    CREATE INDEX [IX_HrLegacyFileMigrationEntries_TenantId_Status]
        ON [dbo].[HrLegacyFileMigrationEntries] ([TenantId], [Status]);
END");
        }

        private static void AddGuidColumnIfMissing(
            MigrationBuilder migrationBuilder, string table, string column)
            => AddColumnIfMissing(migrationBuilder, table, column, "uniqueidentifier NULL");

        private static void AddDateTimeColumnIfMissing(
            MigrationBuilder migrationBuilder, string table, string column)
            => AddColumnIfMissing(migrationBuilder, table, column, "datetime2 NULL");

        private static void AddColumnIfMissing(
            MigrationBuilder migrationBuilder, string table, string column, string definition)
        {
            migrationBuilder.Sql($@"
IF OBJECT_ID(N'[dbo].[{table}]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[{table}]', N'{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};");
        }

        private static void DropColumnIfExists(
            MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.Sql($@"
IF OBJECT_ID(N'[dbo].[{table}]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[{table}]', N'{column}') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];");
        }

        private static void CreateIndexIfMissing(
            MigrationBuilder migrationBuilder, string table, string index, string columns)
        {
            migrationBuilder.Sql($@"
IF OBJECT_ID(N'[dbo].[{table}]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'{index}' AND object_id = OBJECT_ID(N'[dbo].[{table}]'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});");
        }

        private static void DropIndexIfExists(
            MigrationBuilder migrationBuilder, string table, string index)
        {
            migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = N'{index}' AND object_id = OBJECT_ID(N'[dbo].[{table}]'))
    DROP INDEX [{index}] ON [dbo].[{table}];");
        }

        private static void DropTableIfExists(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.Sql($@"
IF OBJECT_ID(N'[dbo].[{table}]', N'U') IS NOT NULL
    DROP TABLE [dbo].[{table}];");
        }
    }
}
