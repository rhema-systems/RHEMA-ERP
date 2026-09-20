using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 16 slice 10. The exit clearance form learns to read the HR Assets register —
    /// <b>FR-HR-183</b>, closing area 9b's decision <b>D4</b>. One flag on the catalogue and three
    /// columns on the form itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>SourcesFromAssetRegister</c> is a flag rather than a rule on <c>Kind</c>, and that is a
    /// money decision.</b> Decision D8 said every <c>CompanyProperty</c> <i>and</i>
    /// <c>OfficeEquipment</c> line would be fed from the register — which sounds right and cannot be
    /// built: a <c>CompanyAsset</c> is classified by <c>AssetType</c>, a free-text per-tenant lookup
    /// with no notion of "this one is office equipment and that one is not". Sourcing both kinds
    /// would list <i>every</i> asset twice, once under each heading, and the FR-HR-184 settlement
    /// would deduct each surcharge twice with it. So the tenant names one line, and
    /// <c>SeparationService.RequireSoleAssetSourceAsync</c> refuses a second.
    /// </para>
    /// <para>
    /// ⚠ <b>Deliberately no foreign keys on <c>SourceAssignmentId</c> or <c>SourceSurchargeId</c>.</b>
    /// Every delete in HR Assets is a <i>soft</i> delete, so an FK here would never fire — it would
    /// only tie a separation migration to the shape of the assets tables. And a clearance form is
    /// evidence about a particular exit: a line whose assignment has been removed must stay
    /// answerable and must still read as the thing somebody signed, which is the same reason its
    /// <c>Name</c> is snapshotted from the asset rather than read through to it. Slice 9's
    /// <c>CompanyAssets.MaintenanceAssetId</c> <b>is</b> an FK because it names a <i>master</i>;
    /// this names a transaction. EF scaffolded it correctly — four columns, no relationship inferred
    /// from the property names.
    /// </para>
    /// <para>
    /// <b><c>OutstandingCurrencyCode</c> exists because a surcharge carries its own currency and a
    /// settlement carries another.</b> Null means "the settlement's own", which is what every
    /// hand-entered amount has always meant and what the loan and advance lines still mean. A figure
    /// in a currency the statement is not stated in is never converted here — it reaches the
    /// statement as an uncomputed line with the amount in its text, the same treatment the travel
    /// advances get, because Finance owns conversion and its stored rates are known to be inverted
    /// (cross-module defect 2).
    /// </para>
    /// <para>
    /// <b>No data is written by this migration, on purpose.</b> A tenant that seeded its clearance
    /// form before the register could feed it needs the flag turning on, and that repair lives in
    /// <c>SeedDefaultClearanceTemplatesAsync</c> — which adopts an existing company-property line
    /// when no line claims the register yet. The endpoint that owns the catalogue can be run again;
    /// a migration that writes rows is a decision taken once, invisibly, that cannot be re-taken.
    /// </para>
    /// <para>
    /// ⚠ <b>Every add is guarded and every drop is conditional</b>, so this is safe to re-run against
    /// a database at either state — including one built from the EF model rather than from the
    /// chain, which is how this repo's <c>rebuild-db</c> works.
    /// </para>
    /// </remarks>
    public partial class AddClearanceAssetSourcing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'SourcesFromAssetRegister' AND object_id = OBJECT_ID('dbo.SeparationClearanceTemplates'))
    ALTER TABLE [dbo].[SeparationClearanceTemplates] ADD [SourcesFromAssetRegister] bit NOT NULL CONSTRAINT [DF_SeparationClearanceTemplates_SourcesFromAssetRegister] DEFAULT (0);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'SourceAssignmentId' AND object_id = OBJECT_ID('dbo.SeparationClearanceItems'))
    ALTER TABLE [dbo].[SeparationClearanceItems] ADD [SourceAssignmentId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'SourceSurchargeId' AND object_id = OBJECT_ID('dbo.SeparationClearanceItems'))
    ALTER TABLE [dbo].[SeparationClearanceItems] ADD [SourceSurchargeId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'OutstandingCurrencyCode' AND object_id = OBJECT_ID('dbo.SeparationClearanceItems'))
    ALTER TABLE [dbo].[SeparationClearanceItems] ADD [OutstandingCurrencyCode] nvarchar(3) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ Reversing this does more than drop four columns. It severs every clearance line's
            // link to the custody it was raised about, so a line reading "Dell Latitude (AST-0117)"
            // survives with nothing saying which asset that was, whether it came back, or which
            // surcharge the amount beside it came from — and the gate that refuses to clear a line
            // while the asset is still out stops firing, silently, because the column it asks about
            // is gone. Any exit mid-clearance would then close over unreturned property.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_SeparationClearanceTemplates_SourcesFromAssetRegister')
    ALTER TABLE [dbo].[SeparationClearanceTemplates] DROP CONSTRAINT [DF_SeparationClearanceTemplates_SourcesFromAssetRegister];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'SourcesFromAssetRegister' AND object_id = OBJECT_ID('dbo.SeparationClearanceTemplates'))
    ALTER TABLE [dbo].[SeparationClearanceTemplates] DROP COLUMN [SourcesFromAssetRegister];");

            foreach (var column in new[]
            {
                "OutstandingCurrencyCode",
                "SourceSurchargeId",
                "SourceAssignmentId",
            })
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = '{column}' AND object_id = OBJECT_ID('dbo.SeparationClearanceItems'))
    ALTER TABLE [dbo].[SeparationClearanceItems] DROP COLUMN [{column}];");
            }
        }
    }
}
