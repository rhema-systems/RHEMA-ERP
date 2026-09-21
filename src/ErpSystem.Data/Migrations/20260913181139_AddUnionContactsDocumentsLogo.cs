using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 3, lane U: the people the company deals with at a union (<c>UnionContacts</c>; register
    /// row U-1; decision D-8), a union's files (<c>UnionDocuments</c>; register row U-2) and the six
    /// logo columns on <c>Unions</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database already has these tables, these columns and
    /// their keys, and a bare <c>CreateTable</c> or <c>AddColumn</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>Purely additive.</b> Two new tables with no rows to default, six nullable columns on
    /// <c>Unions</c>, seven indexes and seven foreign keys — every one <c>NO ACTION</c> (the model says
    /// Restrict) because the union delete is soft and the service refuses it while contacts' files
    /// remain. The union's legacy <c>ContactPerson/ContactEmail/ContactPhone</c> columns are untouched:
    /// they become a mirror of the primary contact row at the next contact save, and until then every
    /// old row keeps the text a person typed.
    /// </para>
    ///
    /// <para>
    /// <b>No default-value trap.</b> The only non-nullable bools (<c>IsPrimary</c>, <c>IsDeleted</c>)
    /// are in the NEW tables, which have no rows to default; all six columns added to <c>Unions</c>
    /// are nullable.
    /// </para>
    /// </remarks>
    public partial class AddUnionContactsDocumentsLogo : Migration
    {
        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string CreateIndex(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string AddForeignKey(
            string table, string fk, string column, string principalTable, string onDelete = "NO ACTION") => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND OBJECT_ID('dbo.{principalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] WITH CHECK
        ADD CONSTRAINT [{fk}] FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([Id]) ON DELETE {onDelete};";

        /// <remarks>
        /// ⚠ The default constraint is looked up rather than named: on a model-built database the
        /// name is server-generated, so guessing it would leave the column undroppable and the
        /// <c>Down</c> broken.
        /// </remarks>
        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @df_{table}_{column} sysname;
    SELECT @df_{table}_{column} = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @df_{table}_{column} IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @df_{table}_{column} + ']');
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";

        private const string Unions = "Unions";
        private const string Contacts = "UnionContacts";
        private const string Documents = "UnionDocuments";

        private static readonly string[] LogoColumns =
        {
            "LogoDocumentRecordId", "LogoDocumentVersionId", "LogoFileName",
            "LogoFileSizeBytes", "LogoFileUploadRecordId", "LogoMimeType",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The logo columns on Unions — each in its own batch ────────────────
            migrationBuilder.Sql(AddColumn(Unions, "LogoDocumentRecordId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Unions, "LogoDocumentVersionId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Unions, "LogoFileName", "nvarchar(255) NULL"));
            migrationBuilder.Sql(AddColumn(Unions, "LogoFileSizeBytes", "bigint NULL"));
            migrationBuilder.Sql(AddColumn(Unions, "LogoFileUploadRecordId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Unions, "LogoMimeType", "nvarchar(100) NULL"));

            // ── 2. UnionContacts ─────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.UnionContacts', 'U') IS NULL
CREATE TABLE [dbo].[UnionContacts] (
    [Id] uniqueidentifier NOT NULL,
    [UnionId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NULL,
    [ExternalName] nvarchar(200) NULL,
    [Email] nvarchar(150) NULL,
    [Phone] nvarchar(50) NULL,
    [Role] nvarchar(100) NOT NULL,
    [IsPrimary] bit NOT NULL,
    [Notes] nvarchar(500) NULL,
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
    CONSTRAINT [PK_UnionContacts] PRIMARY KEY ([Id])
);");

            // ── 3. UnionDocuments ────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.UnionDocuments', 'U') IS NULL
CREATE TABLE [dbo].[UnionDocuments] (
    [Id] uniqueidentifier NOT NULL,
    [UnionId] uniqueidentifier NOT NULL,
    [AgreementId] uniqueidentifier NULL,
    [Kind] int NOT NULL,
    [FileName] nvarchar(255) NOT NULL,
    [FileSize] bigint NULL,
    [Description] nvarchar(500) NULL,
    [UploadDate] datetime2 NOT NULL,
    [UploadedById] uniqueidentifier NOT NULL,
    [FileUploadRecordId] uniqueidentifier NULL,
    [DocumentRecordId] uniqueidentifier NULL,
    [DocumentVersionId] uniqueidentifier NULL,
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
    CONSTRAINT [PK_UnionDocuments] PRIMARY KEY ([Id])
);");

            // ── 4. Indexes ───────────────────────────────────────────────────────────
            migrationBuilder.Sql(CreateIndex(Contacts, "IX_UnionContact_EmployeeId", "[EmployeeId]"));
            migrationBuilder.Sql(CreateIndex(Contacts, "IX_UnionContact_UnionId", "[UnionId]"));
            migrationBuilder.Sql(CreateIndex(Contacts, "IX_UnionContacts_TenantId", "[TenantId]"));
            migrationBuilder.Sql(CreateIndex(Documents, "IX_UnionDocument_AgreementId", "[AgreementId]"));
            migrationBuilder.Sql(CreateIndex(Documents, "IX_UnionDocument_UnionId", "[UnionId]"));
            migrationBuilder.Sql(CreateIndex(Documents, "IX_UnionDocuments_TenantId", "[TenantId]"));
            migrationBuilder.Sql(CreateIndex(Documents, "IX_UnionDocuments_UploadedById", "[UploadedById]"));

            // ── 5. Foreign keys ──────────────────────────────────────────────────────
            migrationBuilder.Sql(AddForeignKey(Contacts, "FK_UnionContacts_Employees_EmployeeId", "EmployeeId", "Employees"));
            migrationBuilder.Sql(AddForeignKey(Contacts, "FK_UnionContacts_Tenants_TenantId", "TenantId", "Tenants"));
            migrationBuilder.Sql(AddForeignKey(Contacts, "FK_UnionContacts_Unions_UnionId", "UnionId", "Unions"));
            migrationBuilder.Sql(AddForeignKey(Documents, "FK_UnionDocuments_CollectiveBargainingAgreements_AgreementId", "AgreementId", "CollectiveBargainingAgreements"));
            migrationBuilder.Sql(AddForeignKey(Documents, "FK_UnionDocuments_Employees_UploadedById", "UploadedById", "Employees"));
            migrationBuilder.Sql(AddForeignKey(Documents, "FK_UnionDocuments_Tenants_TenantId", "TenantId", "Tenants"));
            migrationBuilder.Sql(AddForeignKey(Documents, "FK_UnionDocuments_Unions_UnionId", "UnionId", "Unions"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping a table drops its own foreign keys and indexes with it.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.UnionDocuments', 'U') IS NOT NULL
    DROP TABLE [dbo].[UnionDocuments];");
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.UnionContacts', 'U') IS NOT NULL
    DROP TABLE [dbo].[UnionContacts];");
            foreach (var column in LogoColumns)
                migrationBuilder.Sql(DropColumn(Unions, column));
        }
    }
}
