using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 15b slice 9 — the oath of secrecy register (FRD FR-HR-030, priority M).
    ///
    /// Note what the table does NOT have: a free-text file path. The signed scan is referenced by
    /// FileUploadRecordId (the virus-scanned controlled upload) plus the two central-DMS ids, the
    /// same shape StaffTravelRequestAttachment and the medical documents were each corrected to.
    /// A caller-supplied path is an injection sink, and an oath's scan is the evidence the whole
    /// record rests on.
    ///
    /// There is deliberately no unique index on (TenantId, EmployeeId): a rehire swears again, so
    /// several oaths per employee is normal and the current one is the latest by SwornOn.
    ///
    /// The scaffolded CreateTable/CreateIndex bodies are replaced with guarded SQL (repo
    /// convention): local dev DBs are built from the EF model by rebuild-db, so a database can
    /// already carry these objects without this migration being stamped — every operation checks
    /// before it acts. The generated Designer and the regenerated snapshot are kept as scaffolded.
    /// </summary>
    public partial class AddEmployeeOathOfSecrecy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[EmployeeOathsOfSecrecy]', N'U') IS NULL
BEGIN
    CREATE TABLE [EmployeeOathsOfSecrecy] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Method] int NOT NULL,
        [OathText] nvarchar(4000) NOT NULL,
        [SwornOn] date NOT NULL,
        [RecordedAt] datetime2 NOT NULL,
        [WitnessedById] uniqueidentifier NULL,
        [RecordedById] uniqueidentifier NOT NULL,
        [SignatureIpAddress] nvarchar(64) NULL,
        [SignatureHash] nvarchar(128) NULL,
        [FileUploadRecordId] uniqueidentifier NULL,
        [DocumentRecordId] uniqueidentifier NULL,
        [DocumentVersionId] uniqueidentifier NULL,
        [FileName] nvarchar(255) NULL,
        [MimeType] nvarchar(150) NULL,
        [FileSizeBytes] bigint NULL,
        [Notes] nvarchar(1000) NULL,
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
        CONSTRAINT [PK_EmployeeOathsOfSecrecy] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeOathsOfSecrecy_Employees_EmployeeId] FOREIGN KEY ([EmployeeId])
            REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeOathsOfSecrecy_Employees_RecordedById] FOREIGN KEY ([RecordedById])
            REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeOathsOfSecrecy_Employees_WitnessedById] FOREIGN KEY ([WitnessedById])
            REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeOathsOfSecrecy_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[EmployeeOathsOfSecrecy]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_EmployeeOathsOfSecrecy_EmployeeId'
                     AND object_id = OBJECT_ID(N'[EmployeeOathsOfSecrecy]'))
BEGIN
    CREATE INDEX [IX_EmployeeOathsOfSecrecy_EmployeeId]
        ON [EmployeeOathsOfSecrecy] ([EmployeeId]);
END;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[EmployeeOathsOfSecrecy]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_EmployeeOathsOfSecrecy_RecordedById'
                     AND object_id = OBJECT_ID(N'[EmployeeOathsOfSecrecy]'))
BEGIN
    CREATE INDEX [IX_EmployeeOathsOfSecrecy_RecordedById]
        ON [EmployeeOathsOfSecrecy] ([RecordedById]);
END;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[EmployeeOathsOfSecrecy]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_EmployeeOathsOfSecrecy_TenantId_EmployeeId_SwornOn'
                     AND object_id = OBJECT_ID(N'[EmployeeOathsOfSecrecy]'))
BEGIN
    CREATE INDEX [IX_EmployeeOathsOfSecrecy_TenantId_EmployeeId_SwornOn]
        ON [EmployeeOathsOfSecrecy] ([TenantId], [EmployeeId], [SwornOn]);
END;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[EmployeeOathsOfSecrecy]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_EmployeeOathsOfSecrecy_WitnessedById'
                     AND object_id = OBJECT_ID(N'[EmployeeOathsOfSecrecy]'))
BEGIN
    CREATE INDEX [IX_EmployeeOathsOfSecrecy_WitnessedById]
        ON [EmployeeOathsOfSecrecy] ([WitnessedById]);
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[EmployeeOathsOfSecrecy]', N'U') IS NOT NULL
    DROP TABLE [EmployeeOathsOfSecrecy];
");
        }
    }
}
