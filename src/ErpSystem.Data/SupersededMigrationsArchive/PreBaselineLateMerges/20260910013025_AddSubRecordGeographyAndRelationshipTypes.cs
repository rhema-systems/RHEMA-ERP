using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Lane D2: the employee's sub-records get an address on the administrative-geography tree, and
    /// how one person is tied to another becomes a catalogue instead of four free-text columns
    /// (plan § 6.8, § 6.9; register rows E-3, E-6, E-11a, E-11b; finding X-5).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> builds from the EF model, so
    /// a rebuilt database already has every column, index and constraint below, and a bare
    /// <c>AddColumn</c> or <c>CreateTable</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ Each new <c>GeoAreaId</c> needs an <c>IGeoAreaConsumer</c> probe, and the database
    /// cannot supply it.</b> Geography deletes are SOFT, so these four foreign keys are never
    /// consulted: without a probe, an area someone still uses deletes cleanly, vanishes from every
    /// read, and takes the address with it while leaving a dangling id. All four probes are in
    /// <c>GeoAreaConsumers.cs</c> and registered in <c>ServiceCollectionExtensions</c>. That is not
    /// a hypothetical — it is how a seeded community and an employee's address were lost on
    /// 2026-09-03.
    /// </para>
    ///
    /// <para>
    /// <b>The one statement here that touches existing data</b> is the widening of
    /// <c>EmployeeEmergencyContacts.Relationship</c> from 50 to 100. It was the narrowest of the
    /// four columns spelling out the same idea (referee 200, guarantor 100, candidate 100), its
    /// create DTO carried no length at all, and the screen's schema allowed 100 — so a 51-character
    /// relationship reached SQL Server and failed as a truncation 500. It also has to hold a
    /// <c>RelationshipType.Name</c>, which is 100. Widening loses nothing: no value can fail to fit
    /// a larger column, and no index sits on it.
    /// </para>
    ///
    /// <para>
    /// <b>No default-value trap.</b> EF scaffolds <c>defaultValue: 0</c> for a non-nullable enum or
    /// bool added to an EXISTING table, ignoring the property initialiser — the trap
    /// <c>AddEmployeePayBasis</c> and <c>AddProbationSourceAndContractKind</c> both hit. Every
    /// column added to an existing table here is <b>nullable</b>, and the two non-nullable enum
    /// columns (<c>Category</c>, <c>MapsToDependentRelationship</c>) are in a NEW table with no
    /// rows to default. Nothing to repair.
    /// </para>
    ///
    /// <para>
    /// <b>Why four <c>IX_*_TenantId</c> indexes are dropped.</b> Not a deliberate change: EF's
    /// foreign-key index convention skips a column already covered as the LEADING column of another
    /// index, and each of the four new <c>(TenantId, GeoAreaId)</c> composites does exactly that.
    /// The composite answers every query the single-column index answered, so nothing is lost — but
    /// the drops are guarded, because a model-built database never had them.
    /// </para>
    ///
    /// <para>
    /// <b>Delete behaviour.</b> Every reference here is <c>NO ACTION</c> — an area, a country or a
    /// relationship value that a record is standing on cannot be deleted out from under it. The
    /// services turn that into a sentence naming how many records hold it, rather than letting the
    /// database raise a constraint error at a user.
    /// </para>
    /// </remarks>
    public partial class AddSubRecordGeographyAndRelationshipTypes : Migration
    {
        /// <summary>The four employee sub-records that gained an address on the tree.</summary>
        private static readonly string[] GeoTables =
        {
            "EmployeeContacts", "EmployeeEmergencyContacts", "EmployeeGuarantors", "EmployeeWorkHistories",
        };

        /// <summary>The four tables that gained a link to the relationship catalogue.</summary>
        private static readonly string[] RelationshipTables =
        {
            "EmployeeReferees", "EmployeeGuarantors", "EmployeeEmergencyContacts", "JobCandidateReferees",
        };

        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string CreateIndex(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string CreateUniqueIndex(string table, string index, string columns, string filter = null)
        {
            var where = string.IsNullOrEmpty(filter) ? string.Empty : " WHERE " + filter;
            return $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE UNIQUE INDEX [{index}] ON [dbo].[{table}] ({columns}){where};";
        }

        private static string DropIndex(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        private static string AddForeignKey(
            string table, string fk, string column, string principalTable, string onDelete = "NO ACTION") => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND OBJECT_ID('dbo.{principalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] WITH CHECK
        ADD CONSTRAINT [{fk}] FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([Id]) ON DELETE {onDelete};";

        private static string DropForeignKey(string table, string fk) => $@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{fk}];";

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

        /// <summary>
        /// Each geography table with the composite index that supersedes its <c>IX_*_TenantId</c>.
        /// </summary>
        /// <remarks>
        /// ⚠ The composite names are SINGULAR by the house convention (<c>IX_EmployeeContact_…</c>)
        /// while the table names are plural, which is why they cannot be derived from each other.
        /// </remarks>
        private static readonly (string Table, string Composite)[] GeoIndexes =
        {
            ("EmployeeContacts", "IX_EmployeeContact_Tenant_GeoArea"),
            ("EmployeeEmergencyContacts", "IX_EmployeeEmergencyContact_Tenant_GeoArea"),
            ("EmployeeGuarantors", "IX_EmployeeGuarantor_Tenant_GeoArea"),
            ("EmployeeWorkHistories", "IX_EmployeeWorkHistory_Tenant_GeoArea"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The relationship catalogue ────────────────────────────────────────
            // How one PERSON is tied to another. ⚠ Not the same idea as the enum formerly called
            // RelationshipType, which described how far outside the organisation a job
            // description's working relationships reach and was renamed WorkingRelationshipScope
            // in this lane — it had no typed consumer anywhere and no column carries it.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.RelationshipTypes', 'U') IS NULL
CREATE TABLE [dbo].[RelationshipTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NULL,
    [Category] int NOT NULL,
    [Description] nvarchar(500) NULL,
    [MapsToDependentRelationship] int NULL,
    [SortOrder] int NOT NULL,
    [IsActive] bit NOT NULL,
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
    CONSTRAINT [PK_RelationshipTypes] PRIMARY KEY ([Id])
);");

            migrationBuilder.Sql(CreateUniqueIndex(
                "RelationshipTypes", "IX_RelationshipType_Tenant_Name", "[TenantId], [Name]"));
            // ⚠ Filtered: the code is optional, and an unfiltered unique index would allow exactly
            // one row without one.
            migrationBuilder.Sql(CreateUniqueIndex(
                "RelationshipTypes", "IX_RelationshipType_Tenant_Code", "[TenantId], [Code]", "[Code] IS NOT NULL"));
            // Every picker reads "active, of these categories".
            migrationBuilder.Sql(CreateIndex(
                "RelationshipTypes", "IX_RelationshipType_Tenant_Category_Active", "[TenantId], [Category], [IsActive]"));
            migrationBuilder.Sql(AddForeignKey(
                "RelationshipTypes", "FK_RelationshipTypes_Tenants_TenantId", "TenantId", "Tenants"));

            // ── 2. The widening ──────────────────────────────────────────────────────
            // ⚠ The only statement here that touches existing data. COL_LENGTH reports BYTES for
            // nvarchar, so nvarchar(50) is 100 and nvarchar(100) is 200 — the guard is < 200 rather
            // than = 100 so it is a no-op on a model-built database and on a re-run alike.
            // NOT NULL is restated because ALTER COLUMN drops nullability that is not repeated.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.EmployeeEmergencyContacts', 'Relationship') < 200
    ALTER TABLE [dbo].[EmployeeEmergencyContacts] ALTER COLUMN [Relationship] nvarchar(100) NOT NULL;");

            // ── 3. The address columns ───────────────────────────────────────────────
            // Each in its own batch: SQL Server compiles a batch before running it, so adding a
            // column and then indexing it in one batch fails with "Invalid column name".
            foreach (var table in GeoTables)
                migrationBuilder.Sql(AddColumn(table, "GeoAreaId", "uniqueidentifier NULL"));

            // Region where the table had only a City. EmployeeContacts already had both.
            foreach (var table in new[] { "EmployeeEmergencyContacts", "EmployeeGuarantors", "EmployeeWorkHistories" })
                migrationBuilder.Sql(AddColumn(table, "Region", "nvarchar(100) NULL"));

            // The work history had NO structured address at all — no country, no city, no geography
            // (register row E-6). One free-text line was the whole of it.
            migrationBuilder.Sql(AddColumn("EmployeeWorkHistories", "City", "nvarchar(100) NULL"));
            migrationBuilder.Sql(AddColumn("EmployeeWorkHistories", "CountryId", "uniqueidentifier NULL"));

            // ── 4. The catalogue links ───────────────────────────────────────────────
            foreach (var table in RelationshipTables)
                migrationBuilder.Sql(AddColumn(table, "RelationshipTypeId", "uniqueidentifier NULL"));

            // ── 5. Indexes ───────────────────────────────────────────────────────────
            foreach (var (table, composite) in GeoIndexes)
            {
                // The composite is the reporting index — "every guarantor in the Ashanti Region".
                // The single-column one is what the foreign key's own delete-check uses, and is
                // what the EF model declares; dropping it would make the next scaffold produce a diff.
                migrationBuilder.Sql(CreateIndex(table, composite, "[TenantId], [GeoAreaId]"));
                migrationBuilder.Sql(CreateIndex(table, $"IX_{table}_GeoAreaId", "[GeoAreaId]"));
            }

            migrationBuilder.Sql(CreateIndex("EmployeeWorkHistories", "IX_EmployeeWorkHistories_CountryId", "[CountryId]"));

            migrationBuilder.Sql(CreateIndex("EmployeeReferees", "IX_EmployeeReferees_RelationshipTypeId", "[RelationshipTypeId]"));
            migrationBuilder.Sql(CreateIndex("EmployeeGuarantors", "IX_EmployeeGuarantors_RelationshipTypeId", "[RelationshipTypeId]"));
            migrationBuilder.Sql(CreateIndex("EmployeeEmergencyContacts", "IX_EmployeeEmergencyContacts_RelationshipTypeId", "[RelationshipTypeId]"));
            migrationBuilder.Sql(CreateIndex("JobCandidateReferees", "IX_CandidateReferee_RelationshipTypeId", "[RelationshipTypeId]"));

            // ⚠ Dropped only AFTER the composites exist, so the tenant column is never left
            // unindexed on a live database, not even between two statements of one migration.
            foreach (var (table, _) in GeoIndexes)
                migrationBuilder.Sql(DropIndex(table, $"IX_{table}_TenantId"));

            // ── 6. Foreign keys ──────────────────────────────────────────────────────
            // NO ACTION on all of them. Deleting an area, a country or a relationship value out
            // from under the records standing on it must fail loudly; each service refuses it first
            // with a message that counts them, and this is the backstop for anything that bypasses
            // the service. For the areas it is ONLY a backstop — those deletes are soft, so the
            // constraint never fires and the IGeoAreaConsumer probes are the real guard.
            foreach (var table in GeoTables)
                migrationBuilder.Sql(AddForeignKey(table, $"FK_{table}_GeoAreas_GeoAreaId", "GeoAreaId", "GeoAreas"));

            migrationBuilder.Sql(AddForeignKey(
                "EmployeeWorkHistories", "FK_EmployeeWorkHistories_Countries_CountryId", "CountryId", "Countries"));

            foreach (var table in RelationshipTables)
                migrationBuilder.Sql(AddForeignKey(
                    table, $"FK_{table}_RelationshipTypes_RelationshipTypeId", "RelationshipTypeId", "RelationshipTypes"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible without losing anything a person typed: City and Region still hold each
            // address, because this lane never stopped writing them. What is lost is which area
            // each record stood in, and which catalogue row each relationship came from — the free
            // text stands, because the service mirrored it there on every save.
            foreach (var table in RelationshipTables)
                migrationBuilder.Sql(DropForeignKey(table, $"FK_{table}_RelationshipTypes_RelationshipTypeId"));

            migrationBuilder.Sql(DropForeignKey(
                "EmployeeWorkHistories", "FK_EmployeeWorkHistories_Countries_CountryId"));

            foreach (var table in GeoTables)
                migrationBuilder.Sql(DropForeignKey(table, $"FK_{table}_GeoAreas_GeoAreaId"));

            // Restored BEFORE the composites go, so the tenant column is never left unindexed.
            foreach (var (table, _) in GeoIndexes)
                migrationBuilder.Sql(CreateIndex(table, $"IX_{table}_TenantId", "[TenantId]"));

            foreach (var (table, composite) in GeoIndexes)
            {
                migrationBuilder.Sql(DropIndex(table, composite));
                migrationBuilder.Sql(DropIndex(table, $"IX_{table}_GeoAreaId"));
            }

            migrationBuilder.Sql(DropIndex("EmployeeWorkHistories", "IX_EmployeeWorkHistories_CountryId"));
            migrationBuilder.Sql(DropIndex("EmployeeReferees", "IX_EmployeeReferees_RelationshipTypeId"));
            migrationBuilder.Sql(DropIndex("EmployeeGuarantors", "IX_EmployeeGuarantors_RelationshipTypeId"));
            migrationBuilder.Sql(DropIndex("EmployeeEmergencyContacts", "IX_EmployeeEmergencyContacts_RelationshipTypeId"));
            migrationBuilder.Sql(DropIndex("JobCandidateReferees", "IX_CandidateReferee_RelationshipTypeId"));

            foreach (var table in RelationshipTables)
                migrationBuilder.Sql(DropColumn(table, "RelationshipTypeId"));

            migrationBuilder.Sql(DropColumn("EmployeeWorkHistories", "CountryId"));
            migrationBuilder.Sql(DropColumn("EmployeeWorkHistories", "City"));

            foreach (var table in new[] { "EmployeeEmergencyContacts", "EmployeeGuarantors", "EmployeeWorkHistories" })
                migrationBuilder.Sql(DropColumn(table, "Region"));

            foreach (var table in GeoTables)
                migrationBuilder.Sql(DropColumn(table, "GeoAreaId"));

            // ⚠ NARROWING, and the one statement in this Down that can lose data: any relationship
            // longer than 50 characters written while the column was wide would be truncated. It is
            // guarded to run only when the column is actually wide, and refuses outright if any row
            // would not fit — a Down that silently shortens what somebody typed is worse than a
            // Down that stops and says so.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.EmployeeEmergencyContacts', 'Relationship') > 100
BEGIN
    IF EXISTS (SELECT 1 FROM [dbo].[EmployeeEmergencyContacts] WHERE LEN([Relationship]) > 50)
        ;THROW 50000, 'Cannot narrow EmployeeEmergencyContacts.Relationship to 50: rows hold longer values. Shorten them first.', 1;
    ALTER TABLE [dbo].[EmployeeEmergencyContacts] ALTER COLUMN [Relationship] nvarchar(50) NOT NULL;
END");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.RelationshipTypes', 'U') IS NOT NULL DROP TABLE [dbo].[RelationshipTypes];");
        }
    }
}
