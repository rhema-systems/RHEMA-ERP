using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// The employee document file: a shared vocabulary (<c>EmployeeDocumentTypes</c>), the documents
    /// themselves (<c>EmployeeDocuments</c>), and what each position requires its holder to have on
    /// file (<c>PositionDocumentRequirements</c>). Finish plan, lane 3c.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> The controlled upload gate is wired into more than thirty HR controllers, and the
    /// one entity every other one hangs off could not hold a file at all — measured 2026-08-31, no
    /// employee-document table existed anywhere and <c>EmployeesController</c> had no upload route.
    /// The most-used record in the module could not carry a signed contract, an ID scan or a
    /// certificate.
    /// </para>
    /// <para>
    /// <b>The vocabulary is a TABLE, not an enum</b>, because two features have to speak it: an
    /// employee HOLDS documents and a position REQUIRES them. Every other HR document vocabulary in
    /// the codebase is an enum precisely because nothing else reads it; a requirement naming a
    /// compiled value would mean TDC could never add a document kind without a release.
    /// </para>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, the same rewrite as
    /// <c>20260827223812_AddEmployeeRelationsConcerns</c>. <c>rebuild-db</c> builds from the EF model
    /// rather than the migration chain, so a database rebuilt after the entity change already has
    /// all three tables and a bare <c>CreateTable</c> fails. Every step is guarded, so this is a
    /// no-op against a database already in the target shape and still does the work on one that is
    /// not.
    /// </para>
    /// <para>
    /// <b>No data operation.</b> The starting vocabulary is seeded by
    /// <c>POST api/hr/employee-documents/types/seed-defaults</c>, not by <c>HasData</c> — this is a
    /// tenant's editable list, and a migration seed would re-assert it on a tenant that had
    /// deliberately pruned it. It also avoids the trap the previous migration hit: <c>InsertData</c>
    /// resolves column types from the target model, which the fast EF build strips.
    /// </para>
    /// <para>
    /// <b>⚠ Both unique indexes are FILTERED on <c>IsDeleted = 0</c></b>, and
    /// <c>EmployeeDocumentService.AddRequirementAsync</c> revives rather than re-inserts. A soft
    /// delete does not release a unique index — this module has met that nine times — so an
    /// unfiltered index would make a removed requirement permanently un-re-addable.
    /// </para>
    /// </remarks>
    public partial class AddEmployeeDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeDocumentTypes', 'U') IS NULL
CREATE TABLE [dbo].[EmployeeDocumentTypes] (
    [Id]                     uniqueidentifier NOT NULL,
    [Name]                   nvarchar(150)    NOT NULL,
    [Code]                   nvarchar(30)     NULL,
    [Description]            nvarchar(500)    NULL,
    -- Whether the upload form asks for an expiry date. A property of the KIND of document: a
    -- passport expires and a signed contract does not.
    [HasExpiry]              bit              NOT NULL,
    -- ⚠ Stored, read by nothing yet, and deliberately not presented as an active setting. An
    -- expiry engine is owed here and on IdentificationType (finish plan lane 3b); one engine, not
    -- two. A lead time that chases nobody would be a control that does not control.
    [ExpiryReminderLeadDays] int              NULL,
    [IsActive]               bit              NOT NULL,
    [CreatedAt]              datetime2        NOT NULL,
    [UpdatedAt]              datetime2        NULL,
    [CreatedBy]              nvarchar(max)    NULL,
    [UpdatedBy]              nvarchar(max)    NULL,
    [CreatedById]            uniqueidentifier NULL,
    [LastModifiedById]       uniqueidentifier NULL,
    [IsDeleted]              bit              NOT NULL,
    [DeletedAt]              datetime2        NULL,
    [DeletedBy]              nvarchar(max)    NULL,
    [TenantId]               uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeDocumentTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeDocumentTypes_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeDocuments', 'U') IS NULL
CREATE TABLE [dbo].[EmployeeDocuments] (
    [Id]                 uniqueidentifier NOT NULL,
    [EmployeeId]         uniqueidentifier NOT NULL,
    [DocumentTypeId]     uniqueidentifier NOT NULL,
    [Title]              nvarchar(250)    NULL,
    [Description]        nvarchar(1000)   NULL,
    -- When the document was ISSUED, not when it was uploaded.
    [IssuedOn]           date             NULL,
    [ExpiresOn]          date             NULL,
    -- ⚠ Three ids and never a path. A caller-supplied file location is the injection sink area 16
    -- replaced and D-10, D-14 and D-39 each had to remove after it had already shipped; this table
    -- was built after the gate existed, so it never carries one.
    [FileUploadRecordId] uniqueidentifier NULL,
    [DocumentRecordId]   uniqueidentifier NULL,
    [DocumentVersionId]  uniqueidentifier NULL,
    [FileName]           nvarchar(255)    NULL,
    [MimeType]           nvarchar(150)    NULL,
    [FileSizeBytes]      bigint           NULL,
    -- Stamped from the token by the upload endpoint, never accepted from the request body.
    [UploadedById]       uniqueidentifier NULL,
    [CreatedAt]          datetime2        NOT NULL,
    [UpdatedAt]          datetime2        NULL,
    [CreatedBy]          nvarchar(max)    NULL,
    [UpdatedBy]          nvarchar(max)    NULL,
    [CreatedById]        uniqueidentifier NULL,
    [LastModifiedById]   uniqueidentifier NULL,
    [IsDeleted]          bit              NOT NULL,
    [DeletedAt]          datetime2        NULL,
    [DeletedBy]          nvarchar(max)    NULL,
    [TenantId]           uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeDocuments_EmployeeDocumentTypes_DocumentTypeId] FOREIGN KEY ([DocumentTypeId])
        REFERENCES [dbo].[EmployeeDocumentTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeDocuments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeDocuments_Employees_UploadedById] FOREIGN KEY ([UploadedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.PositionDocumentRequirements', 'U') IS NULL
CREATE TABLE [dbo].[PositionDocumentRequirements] (
    [Id]               uniqueidentifier NOT NULL,
    [PositionId]       uniqueidentifier NOT NULL,
    [DocumentTypeId]   uniqueidentifier NOT NULL,
    -- Both mandatory and expected requirements are reported; only mandatory ones count as a
    -- compliance failure. Nothing here BLOCKS: a missing document is a fact to chase, and refusing
    -- an appointment over one would stop the transaction that gets somebody able to supply it.
    [IsMandatory]      bit              NOT NULL,
    [Notes]            nvarchar(500)    NULL,
    [CreatedAt]        datetime2        NOT NULL,
    [UpdatedAt]        datetime2        NULL,
    [CreatedBy]        nvarchar(max)    NULL,
    [UpdatedBy]        nvarchar(max)    NULL,
    [CreatedById]      uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted]        bit              NOT NULL,
    [DeletedAt]        datetime2        NULL,
    [DeletedBy]        nvarchar(max)    NULL,
    [TenantId]         uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PositionDocumentRequirements] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PositionDocumentRequirements_EmployeeDocumentTypes_DocumentTypeId] FOREIGN KEY ([DocumentTypeId])
        REFERENCES [dbo].[EmployeeDocumentTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PositionDocumentRequirements_EmployeePositions_PositionId] FOREIGN KEY ([PositionId])
        REFERENCES [dbo].[EmployeePositions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PositionDocumentRequirements_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            foreach (var (name, table, columns, unique, filter) in new (string, string, string, bool, string)[]
            {
                ("IX_EmployeeDocuments_DocumentTypeId", "EmployeeDocuments", "[DocumentTypeId]", false, null),
                ("IX_EmployeeDocuments_EmployeeId", "EmployeeDocuments", "[EmployeeId]", false, null),
                ("IX_EmployeeDocuments_UploadedById", "EmployeeDocuments", "[UploadedById]", false, null),
                // The two reads this table serves: an employee's own file, and "who holds this type".
                ("IX_EmployeeDocuments_TenantId_EmployeeId_DocumentTypeId", "EmployeeDocuments",
                    "[TenantId], [EmployeeId], [DocumentTypeId]", false, null),
                // Ahead of the expiry sweep that is owed: a chase list ordered by expiry is the only
                // way this table is ever scanned across employees.
                ("IX_EmployeeDocuments_TenantId_ExpiresOn", "EmployeeDocuments",
                    "[TenantId], [ExpiresOn]", false, null),

                ("UX_EmployeeDocumentTypes_Tenant_Name", "EmployeeDocumentTypes",
                    "[TenantId], [Name]", true, "[IsDeleted] = 0"),

                ("IX_PositionDocumentRequirements_DocumentTypeId", "PositionDocumentRequirements",
                    "[DocumentTypeId]", false, null),
                ("IX_PositionDocumentRequirements_PositionId", "PositionDocumentRequirements",
                    "[PositionId]", false, null),
                ("UX_PositionDocumentRequirements_Position_Type", "PositionDocumentRequirements",
                    "[TenantId], [PositionId], [DocumentTypeId]", true, "[IsDeleted] = 0"),
            })
            {
                var kind = unique ? "CREATE UNIQUE INDEX" : "CREATE INDEX";
                var where = filter is null ? string.Empty : $" WHERE {filter}";
                migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.{table}'))
    {kind} [{name}] ON [dbo].[{table}] ({columns}){where};");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ This destroys every document filed against every employee — the contracts, the ID
            // scans, the permits. The FILES survive in the DMS and in the upload store, because the
            // gate registered them there and nothing in this migration touches either; what is lost
            // is which employee each one belonged to and what kind of document it was, which is most
            // of what makes them findable.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.PositionDocumentRequirements', 'U') IS NOT NULL
    DROP TABLE [dbo].[PositionDocumentRequirements];

IF OBJECT_ID('dbo.EmployeeDocuments', 'U') IS NOT NULL
    DROP TABLE [dbo].[EmployeeDocuments];

IF OBJECT_ID('dbo.EmployeeDocumentTypes', 'U') IS NOT NULL
    DROP TABLE [dbo].[EmployeeDocumentTypes];");
        }
    }
}
