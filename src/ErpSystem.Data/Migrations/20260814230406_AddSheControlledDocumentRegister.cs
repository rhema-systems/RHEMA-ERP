using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 10 slice 16 — the SHE controlled document register
    /// (SheControlledDocuments; FR-SHE-246 version control, FR-SHE-170 filing
    /// and retrieval). Version rows live in the existing central DMS tables —
    /// this migration adds only the register itself.
    ///
    /// The scaffolded CreateTable/CreateIndex bodies are replaced with guarded
    /// SQL (repo convention): local dev DBs are built from the EF model by
    /// rebuild-db, so a DB can already carry these objects without this
    /// migration being stamped — every operation checks before it acts. The
    /// generated Designer and the regenerated snapshot are kept as scaffolded.
    /// </summary>
    public partial class AddSheControlledDocumentRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheControlledDocuments]', N'U') IS NULL
BEGIN
    CREATE TABLE [SheControlledDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentNumber] nvarchar(30) NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Category] int NOT NULL,
        [Status] int NOT NULL,
        [Keywords] nvarchar(500) NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [OrganizationUnitId] uniqueidentifier NULL,
        [LocationId] uniqueidentifier NULL,
        [DocumentRecordId] uniqueidentifier NULL,
        [CurrentVersionLabel] nvarchar(20) NULL,
        [EffectiveDate] datetime2 NULL,
        [ReviewFrequencyMonths] int NULL,
        [NextReviewDate] datetime2 NULL,
        [ApprovedById] uniqueidentifier NULL,
        [ApprovedDate] datetime2 NULL,
        [ArchivedById] uniqueidentifier NULL,
        [ArchivedDate] datetime2 NULL,
        [ArchiveReason] nvarchar(500) NULL,
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
        CONSTRAINT [PK_SheControlledDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SheControlledDocuments_CentralDocumentRecords_DocumentRecordId] FOREIGN KEY ([DocumentRecordId]) REFERENCES [CentralDocumentRecords] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheControlledDocuments_Employees_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheControlledDocuments_Employees_ArchivedById] FOREIGN KEY ([ArchivedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheControlledDocuments_Employees_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheControlledDocuments_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Locations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheControlledDocuments_OrganizationUnits_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SheControlledDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            foreach (var (index, table, columns, unique) in new (string, string, string, bool)[]
            {
                ("IX_SheControlledDocuments_ApprovedById", "SheControlledDocuments", "[ApprovedById]", false),
                ("IX_SheControlledDocuments_ArchivedById", "SheControlledDocuments", "[ArchivedById]", false),
                ("IX_SheControlledDocuments_DocumentRecordId", "SheControlledDocuments", "[DocumentRecordId]", false),
                ("IX_SheControlledDocuments_LocationId", "SheControlledDocuments", "[LocationId]", false),
                ("IX_SheControlledDocuments_OrganizationUnitId", "SheControlledDocuments", "[OrganizationUnitId]", false),
                ("IX_SheControlledDocuments_OwnerId", "SheControlledDocuments", "[OwnerId]", false),
                ("IX_SheControlledDocuments_Status", "SheControlledDocuments", "[Status]", false),
                ("IX_SheControlledDocuments_TenantId_DocumentNumber", "SheControlledDocuments", "[TenantId], [DocumentNumber]", true),
                ("IX_SheControlledDocuments_TenantId_NextReviewDate", "SheControlledDocuments", "[TenantId], [NextReviewDate]", false),
            })
            {
                migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'{index}' AND [object_id] = OBJECT_ID(N'[{table}]'))
    CREATE {(unique ? "UNIQUE " : "")}INDEX [{index}] ON [{table}] ({columns});
");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[SheControlledDocuments]', N'U') IS NOT NULL DROP TABLE [SheControlledDocuments];
");
        }
    }
}
