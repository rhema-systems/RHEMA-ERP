using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// The SHE inspection checklist builder (docs/HR/areas/she/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md §3):
    /// four template child tables (fields, sections, outcomes, signatories), two run tables (field
    /// values, signatures), lifecycle / scoring / print columns on <c>SheInspectionChecklists</c>,
    /// a section link on the items, computed-score and outcome columns on <c>SafetyInspections</c>,
    /// and <c>DisplayOrder</c> on the inspection items. The template's unique index moves from
    /// (TenantId, ChecklistNumber) to (TenantId, ChecklistNumber, Version) so versions share a number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b> — the same rewrite as
    /// <c>20260904032437_AddSheHazardReporter</c>. <c>rebuild-db</c> builds from the EF model rather
    /// than the migration chain, so a rebuilt database already has every table, column, index and
    /// foreign key below and a bare <c>CreateTable</c> / <c>AddColumn</c> fails on it.
    /// </para>
    /// <para>
    /// <b>Data guard — existing templates become Published.</b> The scaffold's <c>Status</c> default
    /// was 0 (no enum value) and <c>ScoringMode</c> 0 likewise. Here <c>Status</c> is added with default
    /// 1 (Draft) for rows created later, and the rows that existed at migration time are set to 2
    /// (Published) in the same guarded step: they were in use by inspections and must stay offered
    /// for new ones. <c>ScoringMode</c> defaults to 2 (CompliancePercentage), which is what the
    /// hand-typed <c>ComplianceScore</c> on their inspections already meant. Both updates run only
    /// when the column was actually added, so a model-built database (where the seeder wrote real
    /// values) is never touched.
    /// </para>
    /// <para>
    /// <b><c>Category</c> on the items goes nullable</b> (the builder defaults it to the section
    /// title); nothing is rewritten. <b>NO ACTION on every foreign key</b>, as the scaffold chose:
    /// the context defaults all relationships to Restrict, and services delete children explicitly.
    /// Decimal columns are <c>decimal(18,4)</c> because that is what the model snapshot carries for
    /// them; the entity's <c>decimal(5,2)</c> annotation is overridden by the context's precision
    /// convention.
    /// </para>
    /// </remarks>
    public partial class AddSheChecklistBuilder : Migration
    {
        private const string AuditColumns = @"
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,";

        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string CreateIndex(string table, string index, string columns, bool unique = false) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE {(unique ? "UNIQUE " : string.Empty)}INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string DropIndex(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        private static string AddForeignKey(string table, string fk, string column, string principalTable) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND OBJECT_ID('dbo.{principalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] WITH CHECK
        ADD CONSTRAINT [{fk}] FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([Id]) ON DELETE NO ACTION;";

        private static string DropForeignKey(string table, string fk) => $@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{fk}];";

        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @df sysname;
    SELECT @df = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @df IS NOT NULL EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @df + ']');
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Template header columns (each in its own batch: later statements name them) ──
            migrationBuilder.Sql(AddColumn("SheInspectionChecklists", "AllowPartialCompliance", "bit NOT NULL CONSTRAINT [DF_SheInspectionChecklists_AllowPartialCompliance] DEFAULT (0)"));
            migrationBuilder.Sql(AddColumn("SheInspectionChecklists", "CriticalSectionNote", "nvarchar(500) NULL"));
            migrationBuilder.Sql(AddColumn("SheInspectionChecklists", "Instructions", "nvarchar(4000) NULL"));
            migrationBuilder.Sql(AddColumn("SheInspectionChecklists", "PreviousVersionId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn("SheInspectionChecklists", "PrintSubtitle", "nvarchar(200) NULL"));
            migrationBuilder.Sql(AddColumn("SheInspectionChecklists", "PrintTitle", "nvarchar(200) NULL"));
            migrationBuilder.Sql(AddColumn("SheInspectionChecklists", "PublishedAt", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumn("SheInspectionChecklists", "PublishedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn("SheInspectionChecklists", "RetiredAt", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumn("SheInspectionChecklists", "ScoringMode", "int NOT NULL CONSTRAINT [DF_SheInspectionChecklists_ScoringMode] DEFAULT (2)"));

            // Status: Draft (1) for rows created from now on; the rows that already existed were live
            // templates and become Published (2) — but only on the path where the column was just added.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.SheInspectionChecklists', 'Status') IS NULL
BEGIN
    ALTER TABLE [dbo].[SheInspectionChecklists] ADD [Status] int NOT NULL CONSTRAINT [DF_SheInspectionChecklists_Status] DEFAULT (1);
    EXEC('UPDATE [dbo].[SheInspectionChecklists] SET [Status] = 2, [PublishedAt] = [CreatedAt] WHERE [Status] = 1');
END");

            // ── 2. Template items: nullable Category, section link ──
            migrationBuilder.Sql(@"
IF COLUMNPROPERTY(OBJECT_ID('dbo.SheInspectionChecklistItems'), 'Category', 'AllowsNull') = 0
    ALTER TABLE [dbo].[SheInspectionChecklistItems] ALTER COLUMN [Category] nvarchar(100) NULL;");
            migrationBuilder.Sql(AddColumn("SheInspectionChecklistItems", "SectionId", "uniqueidentifier NULL"));

            // ── 3. Inspection run columns ──
            migrationBuilder.Sql(AddColumn("SafetyInspections", "CompletedAt", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumn("SafetyInspections", "CompletedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn("SafetyInspections", "CompliancePercentage", "decimal(18,4) NULL"));
            migrationBuilder.Sql(AddColumn("SafetyInspections", "CriticalNonConformityCount", "int NULL"));
            migrationBuilder.Sql(AddColumn("SafetyInspections", "OutcomeId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn("SafetyInspections", "OutcomeOverrideReason", "nvarchar(500) NULL"));
            migrationBuilder.Sql(AddColumn("SafetyInspections", "RecommendedOutcomeId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn("SafetyInspections", "SubjectComments", "nvarchar(2000) NULL"));
            migrationBuilder.Sql(AddColumn("SafetyInspections", "TotalApplicableItems", "int NULL"));
            migrationBuilder.Sql(AddColumn("SafetyInspections", "TotalCompliantItems", "int NULL"));
            migrationBuilder.Sql(AddColumn("SafetyInspections", "TotalNonCompliantItems", "int NULL"));
            migrationBuilder.Sql(AddColumn("SafetyInspections", "TotalPartiallyCompliantItems", "int NULL"));
            migrationBuilder.Sql(AddColumn("SafetyInspectionItems", "DisplayOrder", "int NOT NULL CONSTRAINT [DF_SafetyInspectionItems_DisplayOrder] DEFAULT (0)"));

            // ── 4. Template child tables ──
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.SheInspectionChecklistFields', 'U') IS NULL
CREATE TABLE [dbo].[SheInspectionChecklistFields] (
    [Id] uniqueidentifier NOT NULL,
    [ChecklistId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Label] nvarchar(150) NOT NULL,
    [FieldType] int NOT NULL,
    [IsRequired] bit NOT NULL,
    [ChoiceOptions] nvarchar(1000) NULL,
    [HelpText] nvarchar(300) NULL,{AuditColumns}
    CONSTRAINT [PK_SheInspectionChecklistFields] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SheInspectionChecklistFields_SheInspectionChecklists_ChecklistId] FOREIGN KEY ([ChecklistId]) REFERENCES [dbo].[SheInspectionChecklists] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SheInspectionChecklistFields_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.SheInspectionChecklistSections', 'U') IS NULL
CREATE TABLE [dbo].[SheInspectionChecklistSections] (
    [Id] uniqueidentifier NOT NULL,
    [ChecklistId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Code] nvarchar(10) NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Kind] int NOT NULL,{AuditColumns}
    CONSTRAINT [PK_SheInspectionChecklistSections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SheInspectionChecklistSections_SheInspectionChecklists_ChecklistId] FOREIGN KEY ([ChecklistId]) REFERENCES [dbo].[SheInspectionChecklists] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SheInspectionChecklistSections_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.SheInspectionChecklistOutcomes', 'U') IS NULL
CREATE TABLE [dbo].[SheInspectionChecklistOutcomes] (
    [Id] uniqueidentifier NOT NULL,
    [ChecklistId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Label] nvarchar(150) NOT NULL,
    [Description] nvarchar(500) NULL,
    [MinPercent] decimal(18,4) NULL,
    [MaxPercent] decimal(18,4) NULL,
    [ReinspectionWithinDays] int NULL,
    [IsDisqualifying] bit NOT NULL,{AuditColumns}
    CONSTRAINT [PK_SheInspectionChecklistOutcomes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SheInspectionChecklistOutcomes_SheInspectionChecklists_ChecklistId] FOREIGN KEY ([ChecklistId]) REFERENCES [dbo].[SheInspectionChecklists] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SheInspectionChecklistOutcomes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.SheInspectionChecklistSignatories', 'U') IS NULL
CREATE TABLE [dbo].[SheInspectionChecklistSignatories] (
    [Id] uniqueidentifier NOT NULL,
    [ChecklistId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    [RoleLabel] nvarchar(150) NOT NULL,
    [Kind] int NOT NULL,
    [IsRequired] bit NOT NULL,{AuditColumns}
    CONSTRAINT [PK_SheInspectionChecklistSignatories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SheInspectionChecklistSignatories_SheInspectionChecklists_ChecklistId] FOREIGN KEY ([ChecklistId]) REFERENCES [dbo].[SheInspectionChecklists] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SheInspectionChecklistSignatories_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // ── 5. Run child tables ──
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.SafetyInspectionFieldValues', 'U') IS NULL
CREATE TABLE [dbo].[SafetyInspectionFieldValues] (
    [Id] uniqueidentifier NOT NULL,
    [InspectionId] uniqueidentifier NOT NULL,
    [ChecklistFieldId] uniqueidentifier NOT NULL,
    [ValueText] nvarchar(2000) NULL,
    [ValueReferenceId] uniqueidentifier NULL,{AuditColumns}
    CONSTRAINT [PK_SafetyInspectionFieldValues] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SafetyInspectionFieldValues_SafetyInspections_InspectionId] FOREIGN KEY ([InspectionId]) REFERENCES [dbo].[SafetyInspections] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SafetyInspectionFieldValues_SheInspectionChecklistFields_ChecklistFieldId] FOREIGN KEY ([ChecklistFieldId]) REFERENCES [dbo].[SheInspectionChecklistFields] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SafetyInspectionFieldValues_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.SafetyInspectionSignatures', 'U') IS NULL
CREATE TABLE [dbo].[SafetyInspectionSignatures] (
    [Id] uniqueidentifier NOT NULL,
    [InspectionId] uniqueidentifier NOT NULL,
    [ChecklistSignatoryId] uniqueidentifier NOT NULL,
    [RoleLabel] nvarchar(150) NOT NULL,
    [SignedByEmployeeId] uniqueidentifier NULL,
    [SignedName] nvarchar(200) NOT NULL,
    [SignedAt] datetime2 NOT NULL,
    [Notes] nvarchar(500) NULL,{AuditColumns}
    CONSTRAINT [PK_SafetyInspectionSignatures] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SafetyInspectionSignatures_Employees_SignedByEmployeeId] FOREIGN KEY ([SignedByEmployeeId]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SafetyInspectionSignatures_SafetyInspections_InspectionId] FOREIGN KEY ([InspectionId]) REFERENCES [dbo].[SafetyInspections] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SafetyInspectionSignatures_SheInspectionChecklistSignatories_ChecklistSignatoryId] FOREIGN KEY ([ChecklistSignatoryId]) REFERENCES [dbo].[SheInspectionChecklistSignatories] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SafetyInspectionSignatures_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // ── 6. Indexes: the number index moves to number + version ──
            migrationBuilder.Sql(DropIndex("SheInspectionChecklists", "IX_SheInspectionChecklists_TenantId_ChecklistNumber"));
            migrationBuilder.Sql(CreateIndex("SheInspectionChecklists", "IX_SheInspectionChecklists_TenantId_ChecklistNumber_Version", "[TenantId], [ChecklistNumber], [Version]", unique: true));
            migrationBuilder.Sql(CreateIndex("SheInspectionChecklists", "IX_SheInspectionChecklists_PreviousVersionId", "[PreviousVersionId]"));
            migrationBuilder.Sql(CreateIndex("SheInspectionChecklists", "IX_SheInspectionChecklists_PublishedById", "[PublishedById]"));
            migrationBuilder.Sql(CreateIndex("SheInspectionChecklists", "IX_SheInspectionChecklists_Status", "[Status]"));

            migrationBuilder.Sql(DropIndex("SheInspectionChecklistItems", "IX_SheInspectionChecklistItems_ChecklistId"));
            migrationBuilder.Sql(CreateIndex("SheInspectionChecklistItems", "IX_SheInspectionChecklistItems_ChecklistId_ItemOrder", "[ChecklistId], [ItemOrder]"));
            migrationBuilder.Sql(CreateIndex("SheInspectionChecklistItems", "IX_SheInspectionChecklistItems_SectionId", "[SectionId]"));

            migrationBuilder.Sql(CreateIndex("SafetyInspections", "IX_SafetyInspections_CompletedById", "[CompletedById]"));
            migrationBuilder.Sql(CreateIndex("SafetyInspections", "IX_SafetyInspections_OutcomeId", "[OutcomeId]"));
            migrationBuilder.Sql(CreateIndex("SafetyInspections", "IX_SafetyInspections_RecommendedOutcomeId", "[RecommendedOutcomeId]"));

            foreach (var table in new[] { "SheInspectionChecklistFields", "SheInspectionChecklistSections", "SheInspectionChecklistOutcomes", "SheInspectionChecklistSignatories" })
            {
                migrationBuilder.Sql(CreateIndex(table, $"IX_{table}_ChecklistId_DisplayOrder", "[ChecklistId], [DisplayOrder]"));
                migrationBuilder.Sql(CreateIndex(table, $"IX_{table}_TenantId", "[TenantId]"));
            }

            migrationBuilder.Sql(CreateIndex("SafetyInspectionFieldValues", "IX_SafetyInspectionFieldValues_ChecklistFieldId", "[ChecklistFieldId]"));
            migrationBuilder.Sql(CreateIndex("SafetyInspectionFieldValues", "IX_SafetyInspectionFieldValues_InspectionId_ChecklistFieldId", "[InspectionId], [ChecklistFieldId]", unique: true));
            migrationBuilder.Sql(CreateIndex("SafetyInspectionFieldValues", "IX_SafetyInspectionFieldValues_TenantId", "[TenantId]"));
            migrationBuilder.Sql(CreateIndex("SafetyInspectionSignatures", "IX_SafetyInspectionSignatures_ChecklistSignatoryId", "[ChecklistSignatoryId]"));
            migrationBuilder.Sql(CreateIndex("SafetyInspectionSignatures", "IX_SafetyInspectionSignatures_InspectionId_ChecklistSignatoryId", "[InspectionId], [ChecklistSignatoryId]", unique: true));
            migrationBuilder.Sql(CreateIndex("SafetyInspectionSignatures", "IX_SafetyInspectionSignatures_SignedByEmployeeId", "[SignedByEmployeeId]"));
            migrationBuilder.Sql(CreateIndex("SafetyInspectionSignatures", "IX_SafetyInspectionSignatures_TenantId", "[TenantId]"));

            // ── 7. Foreign keys on the altered tables ──
            migrationBuilder.Sql(AddForeignKey("SafetyInspections", "FK_SafetyInspections_Employees_CompletedById", "CompletedById", "Employees"));
            migrationBuilder.Sql(AddForeignKey("SafetyInspections", "FK_SafetyInspections_SheInspectionChecklistOutcomes_OutcomeId", "OutcomeId", "SheInspectionChecklistOutcomes"));
            migrationBuilder.Sql(AddForeignKey("SafetyInspections", "FK_SafetyInspections_SheInspectionChecklistOutcomes_RecommendedOutcomeId", "RecommendedOutcomeId", "SheInspectionChecklistOutcomes"));
            migrationBuilder.Sql(AddForeignKey("SheInspectionChecklistItems", "FK_SheInspectionChecklistItems_SheInspectionChecklistSections_SectionId", "SectionId", "SheInspectionChecklistSections"));
            migrationBuilder.Sql(AddForeignKey("SheInspectionChecklists", "FK_SheInspectionChecklists_Employees_PublishedById", "PublishedById", "Employees"));
            migrationBuilder.Sql(AddForeignKey("SheInspectionChecklists", "FK_SheInspectionChecklists_SheInspectionChecklists_PreviousVersionId", "PreviousVersionId", "SheInspectionChecklists"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible for the schema; what is lost is every template's structure beyond its flat
            // items, every run's header values, signatures and computed score, and the version link.
            migrationBuilder.Sql(DropForeignKey("SafetyInspections", "FK_SafetyInspections_Employees_CompletedById"));
            migrationBuilder.Sql(DropForeignKey("SafetyInspections", "FK_SafetyInspections_SheInspectionChecklistOutcomes_OutcomeId"));
            migrationBuilder.Sql(DropForeignKey("SafetyInspections", "FK_SafetyInspections_SheInspectionChecklistOutcomes_RecommendedOutcomeId"));
            migrationBuilder.Sql(DropForeignKey("SheInspectionChecklistItems", "FK_SheInspectionChecklistItems_SheInspectionChecklistSections_SectionId"));
            migrationBuilder.Sql(DropForeignKey("SheInspectionChecklists", "FK_SheInspectionChecklists_Employees_PublishedById"));
            migrationBuilder.Sql(DropForeignKey("SheInspectionChecklists", "FK_SheInspectionChecklists_SheInspectionChecklists_PreviousVersionId"));

            foreach (var table in new[] { "SafetyInspectionSignatures", "SafetyInspectionFieldValues", "SheInspectionChecklistSignatories", "SheInspectionChecklistOutcomes", "SheInspectionChecklistSections", "SheInspectionChecklistFields" })
                migrationBuilder.Sql($"IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL DROP TABLE [dbo].[{table}];");

            migrationBuilder.Sql(DropIndex("SafetyInspections", "IX_SafetyInspections_CompletedById"));
            migrationBuilder.Sql(DropIndex("SafetyInspections", "IX_SafetyInspections_OutcomeId"));
            migrationBuilder.Sql(DropIndex("SafetyInspections", "IX_SafetyInspections_RecommendedOutcomeId"));
            migrationBuilder.Sql(DropIndex("SheInspectionChecklistItems", "IX_SheInspectionChecklistItems_SectionId"));
            migrationBuilder.Sql(DropIndex("SheInspectionChecklistItems", "IX_SheInspectionChecklistItems_ChecklistId_ItemOrder"));
            migrationBuilder.Sql(CreateIndex("SheInspectionChecklistItems", "IX_SheInspectionChecklistItems_ChecklistId", "[ChecklistId]"));
            migrationBuilder.Sql(DropIndex("SheInspectionChecklists", "IX_SheInspectionChecklists_Status"));
            migrationBuilder.Sql(DropIndex("SheInspectionChecklists", "IX_SheInspectionChecklists_PublishedById"));
            migrationBuilder.Sql(DropIndex("SheInspectionChecklists", "IX_SheInspectionChecklists_PreviousVersionId"));
            migrationBuilder.Sql(DropIndex("SheInspectionChecklists", "IX_SheInspectionChecklists_TenantId_ChecklistNumber_Version"));

            foreach (var column in new[] { "CompletedAt", "CompletedById", "CompliancePercentage", "CriticalNonConformityCount", "OutcomeId", "OutcomeOverrideReason", "RecommendedOutcomeId", "SubjectComments", "TotalApplicableItems", "TotalCompliantItems", "TotalNonCompliantItems", "TotalPartiallyCompliantItems" })
                migrationBuilder.Sql(DropColumn("SafetyInspections", column));
            migrationBuilder.Sql(DropColumn("SafetyInspectionItems", "DisplayOrder"));
            migrationBuilder.Sql(DropColumn("SheInspectionChecklistItems", "SectionId"));

            // Category back to NOT NULL: blank what is null first, or the ALTER fails on those rows.
            migrationBuilder.Sql(@"
IF COLUMNPROPERTY(OBJECT_ID('dbo.SheInspectionChecklistItems'), 'Category', 'AllowsNull') = 1
BEGIN
    UPDATE [dbo].[SheInspectionChecklistItems] SET [Category] = N'General' WHERE [Category] IS NULL;
    ALTER TABLE [dbo].[SheInspectionChecklistItems] ALTER COLUMN [Category] nvarchar(100) NOT NULL;
END");

            foreach (var column in new[] { "Status", "ScoringMode", "RetiredAt", "PublishedById", "PublishedAt", "PrintTitle", "PrintSubtitle", "PreviousVersionId", "Instructions", "CriticalSectionNote", "AllowPartialCompliance" })
                migrationBuilder.Sql(DropColumn("SheInspectionChecklists", column));

            // Only one version per number can survive the old index; keep the highest.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.SheInspectionChecklists', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SheInspectionChecklists_TenantId_ChecklistNumber' AND object_id = OBJECT_ID('dbo.SheInspectionChecklists'))
BEGIN
    DELETE c FROM [dbo].[SheInspectionChecklists] c
    WHERE EXISTS (SELECT 1 FROM [dbo].[SheInspectionChecklists] o
                  WHERE o.TenantId = c.TenantId AND o.ChecklistNumber = c.ChecklistNumber AND o.Version > c.Version);
    CREATE UNIQUE INDEX [IX_SheInspectionChecklists_TenantId_ChecklistNumber] ON [dbo].[SheInspectionChecklists] ([TenantId], [ChecklistNumber]);
END");
        }
    }
}
